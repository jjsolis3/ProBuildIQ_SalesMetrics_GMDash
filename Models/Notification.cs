using System;
using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public string NotificationType { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Message { get; set; }
        public string? ActionUrl { get; set; }
        public int? RelatedTaskId { get; set; }
        public int? RelatedEnvelopeId { get; set; }
        public int? BroadcastMessageId { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsSystemGenerated { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadDate { get; set; }
        public string? CreatedByName { get; set; } // For display
    }

    public class NotificationListViewModel
    {
        public List<Notification> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class BroadcastMessageCreateViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Message is required.")]
        public string Message { get; set; } = null!;

        [Required(ErrorMessage = "Target type is required.")]
        public string TargetType { get; set; } = null!; // AllUsers, Location, Role

        public int? TargetLocationId { get; set; }

        public int? TargetRoleId { get; set; }

        public DateTime? ExpiresDate { get; set; }

        public string Priority { get; set; } = "Normal"; // Normal, High, Urgent
    }

    public class BroadcastMessage
    {
        public int BroadcastMessageId { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string TargetType { get; set; } = null!;
        public int? TargetLocationId { get; set; }
        public int? TargetRoleId { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiresDate { get; set; }
        public bool IsActive { get; set; }
        public string? Priority { get; set; }
        public string? CreatedByName { get; set; }
        public string? TargetName { get; set; } // Display name of target (location or role name)
    }

    public class NotificationMarkReadRequest
    {
        public int NotificationId { get; set; }
        public bool IsRead { get; set; }
    }

    public class NotificationDeleteRequest
    {
        public int NotificationId { get; set; }
    }

    // ======================================================================
    // Announcements Hub View Models
    // ======================================================================

    public class AnnouncementViewModel
    {
        public int BroadcastMessageId { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string TargetType { get; set; } = null!;
        public int? TargetLocationId { get; set; }
        public int? TargetRoleId { get; set; }
        public string? TargetName { get; set; }
        public int CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpiresDate { get; set; }
        public bool IsActive { get; set; }
        public string? Priority { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public bool IsSent { get; set; }
        public DateTime? SentDate { get; set; }
        public int SentCount { get; set; }
        public int ReadCount { get; set; }
        public bool IsTemplate { get; set; }
        public string? TemplateCategory { get; set; }

        // Computed properties
        public string StatusBadge => GetStatusBadge();
        public double ReadPercentage => SentCount > 0 ? (ReadCount * 100.0 / SentCount) : 0;

        private string GetStatusBadge()
        {
            if (IsTemplate) return "Template";
            if (!IsSent && ScheduledDate.HasValue) return "Scheduled";
            if (!IsSent) return "Draft";
            if (IsSent && ExpiresDate.HasValue && ExpiresDate.Value < DateTime.Now) return "Expired";
            if (IsSent) return "Sent";
            return "Unknown";
        }
    }

    public class AnnouncementDashboardViewModel
    {
        public List<AnnouncementViewModel> ActiveAnnouncements { get; set; } = new();
        public List<AnnouncementViewModel> ScheduledAnnouncements { get; set; } = new();
        public List<AnnouncementViewModel> ExpiredAnnouncements { get; set; } = new();
        public List<AnnouncementViewModel> Templates { get; set; } = new();

        // Statistics
        public int TotalSent { get; set; }
        public int TotalActive { get; set; }
        public int TotalScheduled { get; set; }
        public double AverageReadRate { get; set; }
    }

    public class AnnouncementCreateEditViewModel
    {
        public int? BroadcastMessageId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Message is required.")]
        [StringLength(2000, ErrorMessage = "Message cannot exceed 2000 characters.")]
        public string Message { get; set; } = null!;

        [Required(ErrorMessage = "Target type is required.")]
        public string TargetType { get; set; } = "AllUsers";

        public int? TargetLocationId { get; set; }

        public int? TargetRoleId { get; set; }

        public DateTime? ExpiresDate { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        public string Priority { get; set; } = "Normal";

        public DateTime? ScheduledDate { get; set; }

        public bool SendImmediately { get; set; } = true;

        public bool IsTemplate { get; set; } = false;

        public string? TemplateCategory { get; set; }
    }

    // ======================================================================
    // Settings View Models
    // ======================================================================

    public class NotificationSettingsViewModel
    {
        public int NotificationSettingsId { get; set; }
        public string CategoryName { get; set; } = null!;
        public string CategoryDisplayName { get; set; } = null!;
        public string CategoryDescription { get; set; } = null!;
        public bool IsEnabled { get; set; }
        public bool NotifyAssignee { get; set; }
        public bool NotifyManager { get; set; }
        public bool NotifyTaskOwner { get; set; }
        public bool EnableInAppNotification { get; set; }
        public bool EnableEmailNotification { get; set; }
        public int? ReminderHoursBefore { get; set; }
        public List<int>? SpecificStatusIds { get; set; }
    }

    public class SettingsDashboardViewModel
    {
        public List<NotificationSettingsViewModel> NotificationSettings { get; set; } = new();
        public List<SecuritySettingViewModel> SecuritySettings { get; set; } = new();
        public List<EnvelopeNotificationSettingViewModel> EnvelopeNotificationSettings { get; set; } = new();
        public string ActiveTab { get; set; } = "notifications";
    }

    public class SecuritySettingViewModel
    {
        public int SecuritySettingsId { get; set; }
        public string SettingKey { get; set; } = null!;
        public string SettingValue { get; set; } = null!;
        public string? Description { get; set; }
        public string Category { get; set; } = null!;
        public string InputType { get; set; } = "text"; // text, number, boolean, select
        public List<string>? AvailableOptions { get; set; } // For select inputs
    }

    public class UpdateNotificationSettingRequest
    {
        public int NotificationSettingsId { get; set; }
        public bool IsEnabled { get; set; }
        public bool NotifyAssignee { get; set; }
        public bool NotifyManager { get; set; }
        public bool NotifyTaskOwner { get; set; }
        public bool EnableInAppNotification { get; set; }
        public bool EnableEmailNotification { get; set; }
        public int? ReminderHoursBefore { get; set; }
        public List<int>? SpecificStatusIds { get; set; }
    }

    public class UpdateSecuritySettingRequest
    {
        public int SecuritySettingsId { get; set; }
        public string SettingValue { get; set; } = null!;
    }

    // ======================================================================
    // Envelope Notification Settings View Models
    // ======================================================================

    public class EnvelopeNotificationSettingViewModel
    {
        public int EnvelopeNotificationSettingsId { get; set; }
        /// <summary>NULL = company-wide; otherwise the branch code, e.g. "LAX".</summary>
        public string? LocationCode { get; set; }
        public string LocationName { get; set; } = null!;
        public string? NotificationEmail { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class SaveEnvelopeNotificationSettingRequest
    {
        public int EnvelopeNotificationSettingsId { get; set; }
        public string? NotificationEmail { get; set; }
        public bool IsEnabled { get; set; }
    }
}
