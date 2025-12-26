using System;

namespace SalesMetrics.Models.EFCore;

public partial class BroadcastMessageEntity
{
    public int BroadcastMessageId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string TargetType { get; set; } = null!; // AllUsers, Location, Role

    public int? TargetLocationId { get; set; }

    public int? TargetRoleId { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ExpiresDate { get; set; } // Optional expiration date

    public bool IsActive { get; set; }

    public string? Priority { get; set; } // Normal, High, Urgent
}
