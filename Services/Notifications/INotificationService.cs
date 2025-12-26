using SalesMetrics.Models;

namespace SalesMetrics.Services.Notifications
{
    public interface INotificationService
    {
        // Get notifications for a user
        Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false, int limit = 50);

        // Get unread count for a user
        Task<int> GetUnreadCountAsync(int userId);

        // Mark notification as read
        Task MarkAsReadAsync(int notificationId, int userId);

        // Mark all notifications as read
        Task MarkAllAsReadAsync(int userId);

        // Delete notification for a user
        Task DeleteNotificationAsync(int notificationId, int userId);

        // Send notification to specific user
        Task SendNotificationToUserAsync(int userId, string type, string title, string message, string? actionUrl = null, int? relatedTaskId = null, int? relatedEnvelopeId = null);

        // Send notification to multiple users
        Task SendNotificationToUsersAsync(List<int> userIds, string type, string title, string message, string? actionUrl = null, int? relatedTaskId = null, int? relatedEnvelopeId = null);

        // Task-related notifications
        Task NotifyTaskAssignedAsync(int taskId, int assignedToUserId, int assignedByUserId, string taskTitle);
        Task NotifyNoteAddedAsync(int taskId, int taskOwnerId, int noteAddedByUserId, string taskTitle);
        Task NotifyTaskStatusChangedAsync(int taskId, int assignedToUserId, string taskTitle, string newStatus);
        Task NotifyTaskDueSoonAsync(int taskId, int assignedToUserId, string taskTitle, DateTime dueDate);

        // Broadcast messages
        Task SendBroadcastMessageAsync(BroadcastMessageCreateViewModel model, int createdByUserId);
        Task<List<BroadcastMessage>> GetActiveBroadcastMessagesAsync();
        Task<List<BroadcastMessage>> GetBroadcastMessagesAsync(int? locationId = null, int? roleId = null);
    }
}
