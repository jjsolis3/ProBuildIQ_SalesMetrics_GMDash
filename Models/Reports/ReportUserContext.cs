namespace SalesMetrics.Models.Reports
{
    public class ReportUserContext
    {
        public string UserId { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string OfficeLocation { get; set; } = string.Empty;
        public int LocationId { get; set; }
    }
}
