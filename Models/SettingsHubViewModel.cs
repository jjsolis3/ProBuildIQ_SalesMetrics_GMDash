namespace SalesMetrics.Models
{
    public class SettingsHubViewModel
    {
        public bool CanAccessAnnouncements { get; set; }
        public bool CanAccessSystemSettings { get; set; }
        public bool CanAccessAccessControl { get; set; }
        public bool CanAccessYardiUpload { get; set; }
        public bool CanAccessErrorLogs { get; set; }
        public bool CanAccessBulkPermissions { get; set; }
    }
}
