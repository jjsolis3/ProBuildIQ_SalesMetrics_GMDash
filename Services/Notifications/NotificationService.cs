using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Hubs;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;

namespace SalesMetrics.Services.Notifications
{
    public class NotificationService : INotificationService
    {
        private readonly SalesMetricsDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(SalesMetricsDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false, int limit = 50)
        {
            var query = _context.NotificationRecipients
                .Include(nr => nr.Notification)
                .Where(nr => nr.UserId == userId && !nr.IsDeleted && nr.Notification != null);

            if (unreadOnly)
            {
                query = query.Where(nr => !nr.IsRead);
            }

            var recipients = await query
                .OrderByDescending(nr => nr.Notification.CreatedDate)
                .Take(limit)
                .ToListAsync();

            return recipients
                .Where(nr => nr.Notification != null) // Extra safety check
                .Select(nr => new Notification
                {
                    NotificationId = nr.Notification.NotificationId,
                    NotificationType = nr.Notification.NotificationType,
                    Title = nr.Notification.Title,
                    Message = nr.Notification.Message,
                    ActionUrl = nr.Notification.ActionUrl,
                    RelatedTaskId = nr.Notification.RelatedTaskId,
                    RelatedEnvelopeId = nr.Notification.RelatedEnvelopeId,
                    BroadcastMessageId = nr.Notification.BroadcastMessageId,
                    CreatedByUserId = nr.Notification.CreatedByUserId,
                    CreatedDate = nr.Notification.CreatedDate,
                    IsSystemGenerated = nr.Notification.IsSystemGenerated,
                    IsRead = nr.IsRead,
                    ReadDate = nr.ReadDate
                }).ToList();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.NotificationRecipients
                .CountAsync(nr => nr.UserId == userId && !nr.IsRead && !nr.IsDeleted);
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            var recipient = await _context.NotificationRecipients
                .FirstOrDefaultAsync(nr => nr.NotificationId == notificationId && nr.UserId == userId);

            if (recipient != null && !recipient.IsRead)
            {
                recipient.IsRead = true;
                recipient.ReadDate = DateTime.Now;
                await _context.SaveChangesAsync();

                // Notify via SignalR
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("NotificationRead", notificationId);
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var recipients = await _context.NotificationRecipients
                .Where(nr => nr.UserId == userId && !nr.IsRead && !nr.IsDeleted)
                .ToListAsync();

            foreach (var recipient in recipients)
            {
                recipient.IsRead = true;
                recipient.ReadDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // Notify via SignalR
            await _hubContext.Clients.Group($"User_{userId}").SendAsync("AllNotificationsRead");
        }

        public async Task DeleteNotificationAsync(int notificationId, int userId)
        {
            var recipient = await _context.NotificationRecipients
                .FirstOrDefaultAsync(nr => nr.NotificationId == notificationId && nr.UserId == userId);

            if (recipient != null)
            {
                recipient.IsDeleted = true;
                recipient.DeletedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                // Notify via SignalR
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("NotificationDeleted", notificationId);
            }
        }

        public async Task SendNotificationToUserAsync(int userId, string type, string title, string message, string? actionUrl = null, int? relatedTaskId = null, int? relatedEnvelopeId = null)
        {
            await SendNotificationToUsersAsync(new List<int> { userId }, type, title, message, actionUrl, relatedTaskId, relatedEnvelopeId);
        }

        public async Task SendNotificationToUsersAsync(List<int> userIds, string type, string title, string message, string? actionUrl = null, int? relatedTaskId = null, int? relatedEnvelopeId = null)
        {
            // Create notification
            var notification = new NotificationEntity
            {
                NotificationType = type,
                Title = title,
                Message = message,
                ActionUrl = actionUrl,
                RelatedTaskId = relatedTaskId,
                RelatedEnvelopeId = relatedEnvelopeId,
                CreatedDate = DateTime.Now,
                IsSystemGenerated = true
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Create recipients
            foreach (var userId in userIds)
            {
                var recipient = new NotificationRecipientEntity
                {
                    NotificationId = notification.NotificationId,
                    UserId = userId,
                    IsRead = false,
                    IsDeleted = false
                };
                _context.NotificationRecipients.Add(recipient);
            }

            await _context.SaveChangesAsync();

            // Send real-time notification via SignalR
            var notificationDto = new
            {
                notificationId = notification.NotificationId,
                type = notification.NotificationType,
                title = notification.Title,
                message = notification.Message,
                actionUrl = notification.ActionUrl,
                createdDate = notification.CreatedDate
            };

            foreach (var userId in userIds)
            {
                await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveNotification", notificationDto);
            }
        }

        public async Task NotifyTaskAssignedAsync(int taskId, int assignedToUserId, int assignedByUserId, string taskTitle)
        {
            var assignedByUser = await _context.Users.FindAsync(assignedByUserId);
            var assignedByName = assignedByUser != null ? $"{assignedByUser.FirstName} {assignedByUser.LastName}" : "Someone";

            await SendNotificationToUserAsync(
                assignedToUserId,
                "TaskAssigned",
                "New Task Assigned",
                $"{assignedByName} assigned you a task: {taskTitle}",
                $"/Tasks/Task?taskId={taskId}",
                taskId
            );
        }

        public async Task NotifyNoteAddedAsync(int taskId, int taskOwnerId, int noteAddedByUserId, string taskTitle)
        {
            // Don't notify if the user added a note to their own task
            if (taskOwnerId == noteAddedByUserId)
                return;

            var noteAddedByUser = await _context.Users.FindAsync(noteAddedByUserId);
            var noteAddedByName = noteAddedByUser != null ? $"{noteAddedByUser.FirstName} {noteAddedByUser.LastName}" : "Someone";

            await SendNotificationToUserAsync(
                taskOwnerId,
                "NoteAdded",
                "New Note on Your Task",
                $"{noteAddedByName} added a note to your task: {taskTitle}",
                $"/Tasks/Task?taskId={taskId}",
                taskId
            );
        }

        public async Task NotifyTaskStatusChangedAsync(int taskId, int assignedToUserId, string taskTitle, string newStatus)
        {
            await SendNotificationToUserAsync(
                assignedToUserId,
                "StatusChanged",
                "Task Status Updated",
                $"Task status changed to {newStatus}: {taskTitle}",
                $"/Tasks/Task?taskId={taskId}",
                taskId
            );
        }

        public async Task NotifyTaskDueSoonAsync(int taskId, int assignedToUserId, string taskTitle, DateTime dueDate)
        {
            var daysUntilDue = (dueDate - DateTime.Now).Days;
            var dueMessage = daysUntilDue == 0 ? "today" : daysUntilDue == 1 ? "tomorrow" : $"in {daysUntilDue} days";

            await SendNotificationToUserAsync(
                assignedToUserId,
                "DueDateReminder",
                "Task Due Soon",
                $"Task due {dueMessage}: {taskTitle}",
                $"/Tasks/Task?taskId={taskId}",
                taskId
            );
        }

        public async Task SendBroadcastMessageAsync(BroadcastMessageCreateViewModel model, int createdByUserId)
        {
            // Create broadcast message record
            var broadcastMessage = new BroadcastMessageEntity
            {
                Title = model.Title,
                Message = model.Message,
                TargetType = model.TargetType,
                TargetLocationId = model.TargetLocationId,
                TargetRoleId = model.TargetRoleId,
                CreatedByUserId = createdByUserId,
                CreatedDate = DateTime.Now,
                ExpiresDate = model.ExpiresDate,
                IsActive = true,
                Priority = model.Priority
            };

            _context.BroadcastMessages.Add(broadcastMessage);
            await _context.SaveChangesAsync();

            // Determine target users
            List<int> targetUserIds = new List<int>();

            if (model.TargetType == "AllUsers")
            {
                targetUserIds = await _context.Users
                    .Where(u => u.IsActive)
                    .Select(u => u.Users_ID)
                    .ToListAsync();
            }
            else if (model.TargetType == "Location" && model.TargetLocationId.HasValue)
            {
                targetUserIds = await _context.Users
                    .Where(u => u.IsActive && u.Location == model.TargetLocationId.Value)
                    .Select(u => u.Users_ID)
                    .ToListAsync();
            }
            else if (model.TargetType == "Role" && model.TargetRoleId.HasValue)
            {
                targetUserIds = await _context.Users
                    .Where(u => u.IsActive && u.RoleId == model.TargetRoleId.Value)
                    .Select(u => u.Users_ID)
                    .ToListAsync();
            }

            // Create notification
            var notification = new NotificationEntity
            {
                NotificationType = "BroadcastMessage",
                Title = model.Title,
                Message = model.Message,
                BroadcastMessageId = broadcastMessage.BroadcastMessageId,
                CreatedByUserId = createdByUserId,
                CreatedDate = DateTime.Now,
                IsSystemGenerated = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Create recipients for all target users
            foreach (var userId in targetUserIds)
            {
                var recipient = new NotificationRecipientEntity
                {
                    NotificationId = notification.NotificationId,
                    UserId = userId,
                    IsRead = false,
                    IsDeleted = false
                };
                _context.NotificationRecipients.Add(recipient);
            }

            await _context.SaveChangesAsync();

            // Send real-time notification via SignalR to appropriate group
            var notificationDto = new
            {
                notificationId = notification.NotificationId,
                type = "BroadcastMessage",
                title = model.Title,
                message = model.Message,
                priority = model.Priority,
                createdDate = DateTime.Now
            };

            if (model.TargetType == "AllUsers")
            {
                await _hubContext.Clients.Group("AllUsers").SendAsync("ReceiveNotification", notificationDto);
            }
            else if (model.TargetType == "Location" && model.TargetLocationId.HasValue)
            {
                await _hubContext.Clients.Group($"Location_{model.TargetLocationId}").SendAsync("ReceiveNotification", notificationDto);
            }
            else if (model.TargetType == "Role" && model.TargetRoleId.HasValue)
            {
                await _hubContext.Clients.Group($"Role_{model.TargetRoleId}").SendAsync("ReceiveNotification", notificationDto);
            }
        }

        public async Task<List<BroadcastMessage>> GetActiveBroadcastMessagesAsync()
        {
            var messages = await _context.BroadcastMessages
                .Where(bm => bm.IsActive && (bm.ExpiresDate == null || bm.ExpiresDate > DateTime.Now))
                .OrderByDescending(bm => bm.CreatedDate)
                .ToListAsync();

            return messages.Select(bm => new BroadcastMessage
            {
                BroadcastMessageId = bm.BroadcastMessageId,
                Title = bm.Title,
                Message = bm.Message,
                TargetType = bm.TargetType,
                TargetLocationId = bm.TargetLocationId,
                TargetRoleId = bm.TargetRoleId,
                CreatedByUserId = bm.CreatedByUserId,
                CreatedDate = bm.CreatedDate,
                ExpiresDate = bm.ExpiresDate,
                IsActive = bm.IsActive,
                Priority = bm.Priority
            }).ToList();
        }

        public async Task<List<BroadcastMessage>> GetBroadcastMessagesAsync(int? locationId = null, int? roleId = null)
        {
            var query = _context.BroadcastMessages
                .Where(bm => bm.IsActive && (bm.ExpiresDate == null || bm.ExpiresDate > DateTime.Now));

            if (locationId.HasValue)
            {
                query = query.Where(bm => bm.TargetType == "AllUsers" ||
                                         (bm.TargetType == "Location" && bm.TargetLocationId == locationId.Value));
            }

            if (roleId.HasValue)
            {
                query = query.Where(bm => bm.TargetType == "AllUsers" ||
                                         (bm.TargetType == "Role" && bm.TargetRoleId == roleId.Value));
            }

            var messages = await query
                .OrderByDescending(bm => bm.CreatedDate)
                .ToListAsync();

            return messages.Select(bm => new BroadcastMessage
            {
                BroadcastMessageId = bm.BroadcastMessageId,
                Title = bm.Title,
                Message = bm.Message,
                TargetType = bm.TargetType,
                TargetLocationId = bm.TargetLocationId,
                TargetRoleId = bm.TargetRoleId,
                CreatedByUserId = bm.CreatedByUserId,
                CreatedDate = bm.CreatedDate,
                ExpiresDate = bm.ExpiresDate,
                IsActive = bm.IsActive,
                Priority = bm.Priority
            }).ToList();
        }
    }
}
