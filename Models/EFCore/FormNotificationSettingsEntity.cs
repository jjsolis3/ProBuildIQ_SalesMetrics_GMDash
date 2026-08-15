namespace SalesMetrics.Models.EFCore;

public class FormNotificationSettingsEntity
{
    public int FormNotificationSettingsId { get; set; }
    public string? LocationCode { get; set; }
    public string LocationName { get; set; } = default!;
    public string? NotificationEmail { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime LastModifiedDate { get; set; }
    public int LastModifiedByUserId { get; set; }
}
