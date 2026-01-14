namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// API response models for AJAX calls
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class TableListResponse
    {
        public List<TableMetadata> Tables { get; set; } = new();
        public string DataSourceType { get; set; } = string.Empty;
    }

    public class ColumnListResponse
    {
        public string TableName { get; set; } = string.Empty;
        public List<ColumnMetadata> Columns { get; set; } = new();
    }

    public class RelationshipListResponse
    {
        public string TableName { get; set; } = string.Empty;
        public List<RelationshipMetadata> Relationships { get; set; } = new();
    }

    public class QueryValidationResponse
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? GeneratedSql { get; set; }
    }

    public class ReportSaveResponse
    {
        public int ReportDefinitionId { get; set; }
        public string ReportId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
