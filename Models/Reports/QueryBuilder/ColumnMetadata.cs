namespace SalesMetrics.Models.Reports.QueryBuilder
{
    /// <summary>
    /// Metadata about a column/field in a table
    /// </summary>
    public class ColumnMetadata
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Normalized data type: string, number, date, boolean
        /// </summary>
        public string DataType { get; set; } = "string";

        /// <summary>
        /// Source-specific data type: varchar(50), int, datetime, etc.
        /// </summary>
        public string? NativeDataType { get; set; }

        public string? Description { get; set; }
        public bool IsFilterable { get; set; } = true;
        public bool IsSortable { get; set; } = true;
        public bool IsAggregatable { get; set; } = false;
        public bool IsSensitive { get; set; } = false;
    }
}
