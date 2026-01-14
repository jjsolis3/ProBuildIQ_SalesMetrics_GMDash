namespace SalesMetrics.Models.Reports.QueryBuilder
{
    /// <summary>
    /// Metadata about a table/entity in a data source
    /// </summary>
    public class TableMetadata
    {
        public string TableName { get; set; } = string.Empty;
        public string SchemaName { get; set; } = "dbo";
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Category { get; set; }

        /// <summary>
        /// TRUE for API endpoints that act like tables
        /// </summary>
        public bool IsVirtual { get; set; } = false;

        /// <summary>
        /// Source type: Database, API, View, Function
        /// </summary>
        public string SourceType { get; set; } = "Database";
    }
}
