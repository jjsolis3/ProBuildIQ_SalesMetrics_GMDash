namespace SalesMetrics.Models.EFCore;

public class EnvelopeNotificationSettingsEntity
{
    public int EnvelopeNotificationSettingsId { get; set; }

    /// <summary>
    /// NULL = company-wide notification (sent for every envelope regardless of branch).
    /// A value like "LAX", "LSV", "CHN", "PHX", "SND" = branch-specific notification.
    /// </summary>
    public string? LocationCode { get; set; }

    /// <summary>
    /// Human-readable label, e.g. "Company (All Branches)" or "Los Angeles".
    /// </summary>
    public string LocationName { get; set; } = default!;

    /// <summary>
    /// Email address to receive the completion notification.
    /// NULL or empty means no notification is sent for this row.
    /// </summary>
    public string? NotificationEmail { get; set; }

    public bool IsEnabled { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public int LastModifiedByUserId { get; set; }
}
