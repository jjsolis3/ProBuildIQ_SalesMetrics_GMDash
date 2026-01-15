namespace SalesMetrics.Models.QueryBuilder
{
    /// <summary>
    /// ViewModel for Query Builder Index page (list of reports)
    /// </summary>
    public class ReportBuilderIndexViewModel
    {
        public List<ReportSummaryDto> Reports { get; set; } = new();

        // User permissions
        public bool CanCreateReports { get; set; }
        public bool CanEditReports { get; set; }
        public bool CanDeleteReports { get; set; }
        public bool IsAdmin { get; set; }
    }

    /// <summary>
    /// Summary DTO for displaying reports in list view
    /// </summary>
    public class ReportSummaryDto
    {
        public int ReportId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DataSourceType { get; set; } = "SQL";
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public bool IsShared { get; set; }
        public int ExecutionCount { get; set; }
    }
}
