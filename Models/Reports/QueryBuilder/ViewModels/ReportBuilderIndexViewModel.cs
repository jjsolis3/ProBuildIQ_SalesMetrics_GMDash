using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// Main ViewModel for Query Builder index page (list of custom reports)
    /// </summary>
    public class ReportBuilderIndexViewModel
    {
        public List<ReportSummaryViewModel> Reports { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public List<DataSourceInfo> AvailableDataSources { get; set; } = new();
        public UserPermissions Permissions { get; set; } = new();
    }

    public class ReportSummaryViewModel
    {
        public int ReportDefinitionId { get; set; }
        public string ReportId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string DataSourceType { get; set; } = "SQL";
        public string DataSourceName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int Version { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? LastExecutedDate { get; set; }
        public int ExecutionCount { get; set; }
        public bool IsFavorite { get; set; }
    }

    public class UserPermissions
    {
        public bool CanAccess { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanShare { get; set; }
        public bool IsAdmin { get; set; }
    }
}
