using System;

namespace SalesMetrics.Models.EFCore;

public partial class NotificationRecipientEntity
{
    public int NotificationRecipientId { get; set; }

    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadDate { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedDate { get; set; }

    public virtual NotificationEntity Notification { get; set; } = null!;
}
