using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using SalesMetrics.Services.Notifications;

namespace SalesMetrics.Services.Announcements
{
    public class AnnouncementService : IAnnouncementService
    {
        private readonly SalesMetricsDbContext _context;
        private readonly INotificationService _notificationService;

        public AnnouncementService(SalesMetricsDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<AnnouncementDashboardViewModel> GetDashboardDataAsync()
        {
            var allAnnouncements = await GetAllAnnouncementsInternalAsync();

            var now = DateTime.Now;
            var active = allAnnouncements.Where(a => !a.IsTemplate && a.IsSent && (!a.ExpiresDate.HasValue || a.ExpiresDate.Value >= now)).ToList();
            var scheduled = allAnnouncements.Where(a => !a.IsTemplate && !a.IsSent && a.ScheduledDate.HasValue).ToList();
            var expired = allAnnouncements.Where(a => !a.IsTemplate && a.IsSent && a.ExpiresDate.HasValue && a.ExpiresDate.Value < now).ToList();
            var templates = allAnnouncements.Where(a => a.IsTemplate).ToList();

            var totalSent = allAnnouncements.Count(a => !a.IsTemplate && a.IsSent);
            var avgReadRate = totalSent > 0 ? allAnnouncements.Where(a => !a.IsTemplate && a.IsSent).Average(a => a.ReadPercentage) : 0;

            return new AnnouncementDashboardViewModel
            {
                ActiveAnnouncements = active,
                ScheduledAnnouncements = scheduled,
                ExpiredAnnouncements = expired,
                Templates = templates,
                TotalSent = totalSent,
                TotalActive = active.Count,
                TotalScheduled = scheduled.Count,
                AverageReadRate = Math.Round(avgReadRate, 2)
            };
        }

        public async Task<List<AnnouncementViewModel>> GetAllAnnouncementsAsync()
        {
            return await GetAllAnnouncementsInternalAsync();
        }

        public async Task<List<AnnouncementViewModel>> GetActiveAnnouncementsAsync()
        {
            var now = DateTime.Now;
            var announcements = await GetAllAnnouncementsInternalAsync();
            return announcements.Where(a => !a.IsTemplate && a.IsSent && (!a.ExpiresDate.HasValue || a.ExpiresDate.Value >= now)).ToList();
        }

        public async Task<List<AnnouncementViewModel>> GetScheduledAnnouncementsAsync()
        {
            var announcements = await GetAllAnnouncementsInternalAsync();
            return announcements.Where(a => !a.IsTemplate && !a.IsSent && a.ScheduledDate.HasValue).ToList();
        }

        public async Task<List<AnnouncementViewModel>> GetExpiredAnnouncementsAsync()
        {
            var now = DateTime.Now;
            var announcements = await GetAllAnnouncementsInternalAsync();
            return announcements.Where(a => !a.IsTemplate && a.IsSent && a.ExpiresDate.HasValue && a.ExpiresDate.Value < now).ToList();
        }

        public async Task<List<AnnouncementViewModel>> GetTemplatesAsync()
        {
            var announcements = await GetAllAnnouncementsInternalAsync();
            return announcements.Where(a => a.IsTemplate).ToList();
        }

        public async Task<AnnouncementViewModel?> GetAnnouncementByIdAsync(int id)
        {
            var announcement = await _context.BroadcastMessages
                .Where(b => b.BroadcastMessageId == id)
                .Select(b => new AnnouncementViewModel
                {
                    BroadcastMessageId = b.BroadcastMessageId,
                    Title = b.Title,
                    Message = b.Message,
                    TargetType = b.TargetType,
                    TargetLocationId = b.TargetLocationId,
                    TargetRoleId = b.TargetRoleId,
                    TargetName = b.TargetType == "Location"
                        ? _context.Locations.Where(l => l.LocationId == b.TargetLocationId).Select(l => l.LocationName).FirstOrDefault()
                        : b.TargetType == "Role"
                        ? _context.Roles.Where(r => r.RoleId == b.TargetRoleId).Select(r => r.RoleName).FirstOrDefault()
                        : "All Users",
                    CreatedByUserId = b.CreatedByUserId,
                    CreatedByName = _context.Users.Where(u => u.Users_ID == b.CreatedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                    CreatedDate = b.CreatedDate,
                    ExpiresDate = b.ExpiresDate,
                    IsActive = b.IsActive,
                    Priority = b.Priority,
                    ScheduledDate = b.ScheduledDate,
                    IsSent = b.IsSent,
                    SentDate = b.SentDate,
                    SentCount = b.SentCount,
                    ReadCount = b.ReadCount,
                    IsTemplate = b.IsTemplate,
                    TemplateCategory = b.TemplateCategory
                })
                .FirstOrDefaultAsync();

            return announcement;
        }

        public async Task<int> CreateAnnouncementAsync(AnnouncementCreateEditViewModel model, int createdByUserId)
        {
            var entity = new BroadcastMessageEntity
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
                Priority = model.Priority,
                ScheduledDate = model.SendImmediately ? null : model.ScheduledDate,
                IsSent = false,
                SentCount = 0,
                ReadCount = 0,
                IsTemplate = model.IsTemplate,
                TemplateCategory = model.TemplateCategory
            };

            _context.BroadcastMessages.Add(entity);
            await _context.SaveChangesAsync();

            // If send immediately, send it now
            if (model.SendImmediately && !model.IsTemplate)
            {
                await SendAnnouncementAsync(entity.BroadcastMessageId, createdByUserId);
            }

            return entity.BroadcastMessageId;
        }

        public async Task UpdateAnnouncementAsync(int id, AnnouncementCreateEditViewModel model, int modifiedByUserId)
        {
            var entity = await _context.BroadcastMessages.FindAsync(id);
            if (entity == null)
                throw new Exception("Announcement not found");

            // Don't allow updating sent announcements
            if (entity.IsSent)
                throw new Exception("Cannot update an announcement that has already been sent");

            entity.Title = model.Title;
            entity.Message = model.Message;
            entity.TargetType = model.TargetType;
            entity.TargetLocationId = model.TargetLocationId;
            entity.TargetRoleId = model.TargetRoleId;
            entity.ExpiresDate = model.ExpiresDate;
            entity.Priority = model.Priority;
            entity.ScheduledDate = model.SendImmediately ? null : model.ScheduledDate;
            entity.IsTemplate = model.IsTemplate;
            entity.TemplateCategory = model.TemplateCategory;

            await _context.SaveChangesAsync();

            // If send immediately flag is set, send it now
            if (model.SendImmediately && !model.IsTemplate && !entity.IsSent)
            {
                await SendAnnouncementAsync(id, modifiedByUserId);
            }
        }

        public async Task DeleteAnnouncementAsync(int id)
        {
            var entity = await _context.BroadcastMessages.FindAsync(id);
            if (entity == null)
                throw new Exception("Announcement not found");

            _context.BroadcastMessages.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public async Task SendAnnouncementAsync(int id, int sentByUserId)
        {
            var entity = await _context.BroadcastMessages.FindAsync(id);
            if (entity == null)
                throw new Exception("Announcement not found");

            if (entity.IsSent)
                throw new Exception("Announcement has already been sent");

            if (entity.IsTemplate)
                throw new Exception("Cannot send a template. Create an announcement from the template first.");

            // Send via NotificationService
            var broadcastModel = new BroadcastMessageCreateViewModel
            {
                Title = entity.Title,
                Message = entity.Message,
                TargetType = entity.TargetType,
                TargetLocationId = entity.TargetLocationId,
                TargetRoleId = entity.TargetRoleId,
                Priority = entity.Priority ?? "Normal",
                ExpiresDate = entity.ExpiresDate
            };

            await _notificationService.SendBroadcastMessageAsync(broadcastModel, sentByUserId);

            // Update the entity
            entity.IsSent = true;
            entity.SentDate = DateTime.Now;

            // Calculate sent count based on target
            entity.SentCount = await CalculateSentCountAsync(entity);

            await _context.SaveChangesAsync();
        }

        public async Task<bool> ProcessScheduledAnnouncementsAsync()
        {
            var now = DateTime.Now;
            var scheduledAnnouncements = await _context.BroadcastMessages
                .Where(b => !b.IsSent && !b.IsTemplate && b.ScheduledDate.HasValue && b.ScheduledDate.Value <= now)
                .ToListAsync();

            foreach (var announcement in scheduledAnnouncements)
            {
                try
                {
                    await SendAnnouncementAsync(announcement.BroadcastMessageId, announcement.CreatedByUserId);
                }
                catch (Exception ex)
                {
                    // Log the error but continue processing other announcements
                    Console.WriteLine($"Error sending scheduled announcement {announcement.BroadcastMessageId}: {ex.Message}");
                }
            }

            return scheduledAnnouncements.Any();
        }

        public async Task<int> CreateTemplateAsync(AnnouncementCreateEditViewModel model, int createdByUserId)
        {
            model.IsTemplate = true;
            model.SendImmediately = false;
            return await CreateAnnouncementAsync(model, createdByUserId);
        }

        public async Task<AnnouncementViewModel?> GetTemplateByIdAsync(int id)
        {
            var template = await GetAnnouncementByIdAsync(id);
            if (template != null && !template.IsTemplate)
                return null;
            return template;
        }

        public async Task UpdateReadCountAsync(int broadcastMessageId)
        {
            // Count how many notification recipients have read notifications related to this broadcast message
            var readCount = await _context.NotificationRecipients
                .Where(nr => nr.Notification.BroadcastMessageId == broadcastMessageId && nr.IsRead)
                .CountAsync();

            var entity = await _context.BroadcastMessages.FindAsync(broadcastMessageId);
            if (entity != null)
            {
                entity.ReadCount = readCount;
                await _context.SaveChangesAsync();
            }
        }

        // Private helper methods
        private async Task<List<AnnouncementViewModel>> GetAllAnnouncementsInternalAsync()
        {
            return await _context.BroadcastMessages
                .Select(b => new AnnouncementViewModel
                {
                    BroadcastMessageId = b.BroadcastMessageId,
                    Title = b.Title,
                    Message = b.Message,
                    TargetType = b.TargetType,
                    TargetLocationId = b.TargetLocationId,
                    TargetRoleId = b.TargetRoleId,
                    TargetName = b.TargetType == "Location"
                        ? _context.Locations.Where(l => l.LocationId == b.TargetLocationId).Select(l => l.LocationName).FirstOrDefault()
                        : b.TargetType == "Role"
                        ? _context.Roles.Where(r => r.RoleId == b.TargetRoleId).Select(r => r.RoleName).FirstOrDefault()
                        : "All Users",
                    CreatedByUserId = b.CreatedByUserId,
                    CreatedByName = _context.Users.Where(u => u.Users_ID == b.CreatedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                    CreatedDate = b.CreatedDate,
                    ExpiresDate = b.ExpiresDate,
                    IsActive = b.IsActive,
                    Priority = b.Priority,
                    ScheduledDate = b.ScheduledDate,
                    IsSent = b.IsSent,
                    SentDate = b.SentDate,
                    SentCount = b.SentCount,
                    ReadCount = b.ReadCount,
                    IsTemplate = b.IsTemplate,
                    TemplateCategory = b.TemplateCategory
                })
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();
        }

        private async Task<int> CalculateSentCountAsync(BroadcastMessageEntity entity)
        {
            if (entity.TargetType == "AllUsers")
            {
                return await _context.Users.CountAsync();
            }
            else if (entity.TargetType == "Location" && entity.TargetLocationId.HasValue)
            {
                return await _context.Users.Where(u => u.Location == entity.TargetLocationId.Value).CountAsync();
            }
            else if (entity.TargetType == "Role" && entity.TargetRoleId.HasValue)
            {
                return await _context.Users.Where(u => u.RoleId == entity.TargetRoleId.Value).CountAsync();
            }
            return 0;
        }
    }
}
