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

    // Scheduling & Analytics Fields
    public DateTime? ScheduledDate { get; set; } // When to send if scheduled

    public bool IsSent { get; set; } // Whether it has been sent

    public DateTime? SentDate { get; set; } // When it was actually sent

    public int SentCount { get; set; } // How many users received it

    public int ReadCount { get; set; } // How many users read it

    public bool IsTemplate { get; set; } // Whether it's a template

    public string? TemplateCategory { get; set; } // Category if template (e.g., Welcome, Policy, Alert)
}
