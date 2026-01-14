using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// ViewModel for creating/editing a report in Query Builder
    /// </summary>
    public class ReportBuilderCreateViewModel
    {
        // Report Basic Info
        public int? ReportDefinitionId { get; set; } // Null for new, set for edit
        public string ReportId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }

        // Data Source Selection
        public string DataSourceType { get; set; } = "SQL";
        public List<DataSourceInfo> AvailableDataSources { get; set; } = new();

        // Available Tables (based on selected data source)
        public List<TableMetadata> AvailableTables { get; set; } = new();

        // Selected Tables & Columns
        public QueryDefinition QueryDefinition { get; set; } = new();

        // Auto-suggested relationships
        public List<RelationshipMetadata> SuggestedJoins { get; set; } = new();

        // Available aggregate functions
        public List<string> AvailableAggregates { get; set; } = new()
        {
            "SUM", "AVG", "COUNT", "MIN", "MAX", "COUNT_DISTINCT"
        };

        // Available operators
        public List<string> AvailableOperators { get; set; } = new()
        {
            "=", "!=", ">", "<", ">=", "<=", "LIKE", "IN", "BETWEEN", "IS NULL", "IS NOT NULL"
        };

        // Authorization
        public List<int> AllowedRoleIds { get; set; } = new();
        public List<string> AllowedLocationCodes { get; set; } = new();

        // UI State
        public string CurrentStep { get; set; } = "DataSource"; // DataSource, Tables, Columns, Filters, Review
        public bool IsEditMode { get; set; } = false;

        // Validation
        public List<string> ValidationErrors { get; set; } = new();
    }
}
