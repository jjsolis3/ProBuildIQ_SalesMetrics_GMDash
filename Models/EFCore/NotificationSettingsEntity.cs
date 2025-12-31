using System;

namespace SalesMetrics.Models.EFCore;

public partial class NotificationSettingsEntity
{
    public int NotificationSettingsId { get; set; }

    public string CategoryName { get; set; } = null!; // TaskAssigned, StatusChanged, NoteAdded, DueDateReminder, DocumentSigning

    public bool IsEnabled { get; set; } // Global enable/disable toggle

    public bool NotifyAssignee { get; set; } // Notify the task assignee

    public bool NotifyManager { get; set; } // Notify the manager/supervisor

    public bool NotifyTaskOwner { get; set; } // Notify the task creator/owner

    public bool EnableInAppNotification { get; set; } // In-app notifications

    public bool EnableEmailNotification { get; set; } // Email notifications (future)

    public int? ReminderHoursBefore { get; set; } // Hours before due date for reminders (for DueDateReminder)

    public string? SpecificStatuses { get; set; } // Comma-separated status IDs (for StatusChanged notifications)

    public DateTime LastModifiedDate { get; set; }

    public int LastModifiedByUserId { get; set; }
}
