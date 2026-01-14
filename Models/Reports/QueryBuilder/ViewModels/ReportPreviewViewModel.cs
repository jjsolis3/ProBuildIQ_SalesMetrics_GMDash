using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// ViewModel for preview results page
    /// </summary>
    public class ReportPreviewViewModel
    {
        public string ReportId { get; set; } = string.Empty;
        public string ReportName { get; set; } = string.Empty;
        public QueryDefinition QueryDefinition { get; set; } = new();
        public string GeneratedSql { get; set; } = string.Empty;

        // Preview Results
        public List<Dictionary<string, object?>> PreviewData { get; set; } = new();
        public List<string> ColumnNames { get; set; } = new();
        public int TotalRows { get; set; }
        public int PreviewRowCount { get; set; } = 100; // Show first 100 rows

        // Execution Metrics
        public int ExecutionTimeMs { get; set; }
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }

        // Validation Results
        public ValidationResult ValidationResult { get; set; } = ValidationResult.Success();
    }
}
