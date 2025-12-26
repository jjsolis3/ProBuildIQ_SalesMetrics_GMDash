using System;

namespace SalesMetrics.Models.EFCore;

public partial class NotificationEntity
{
    public int NotificationId { get; set; }

    public string NotificationType { get; set; } = null!; // TaskAssigned, NoteAdded, StatusChanged, DueDateReminder, BroadcastMessage, DocumentSigning

    public string Title { get; set; } = null!;

    public string? Message { get; set; }

    public string? ActionUrl { get; set; } // URL to navigate when clicked

    public int? RelatedTaskId { get; set; }

    public int? RelatedEnvelopeId { get; set; }

    public int? BroadcastMessageId { get; set; }

    public int? CreatedByUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public bool IsSystemGenerated { get; set; } // true for auto-generated, false for manual broadcasts

    public virtual ICollection<NotificationRecipientEntity> Recipients { get; set; } = new List<NotificationRecipientEntity>();
}
