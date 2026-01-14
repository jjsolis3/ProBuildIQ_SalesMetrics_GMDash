namespace SalesMetrics.Models.Reports.QueryBuilder
{
    /// <summary>
    /// Defines the structure of a query (source-agnostic)
    /// </summary>
    public class QueryDefinition
    {
        public string ReportId { get; set; } = string.Empty;
        public int Version { get; set; } = 1;

        public DataSourceInfo DataSource { get; set; } = new();
        public List<TableReference> Tables { get; set; } = new();
        public List<ColumnDefinition> Columns { get; set; } = new();
        public List<FilterDefinition> Filters { get; set; } = new();
        public List<string> GroupBy { get; set; } = new();
        public List<OrderByDefinition> OrderBy { get; set; } = new();
    }

    public class DataSourceInfo
    {
        public string Type { get; set; } = "SQL";
        public string ConfigName { get; set; } = "CompUFloor";
        public List<string> FallbackSources { get; set; } = new();
    }

    public class TableReference
    {
        public int TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public bool IsBaseTable { get; set; } = false;
        public string? JoinType { get; set; }
        public string? JoinCondition { get; set; }
        public string SourceType { get; set; } = "SQL";
    }

    public class ColumnDefinition
    {
        public string Expression { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DataType { get; set; } = "text";
        public string? FormatString { get; set; }
        public bool IsVisible { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;
        public string? AggregateFunction { get; set; }
        public List<ConditionalFormat>? ConditionalFormatting { get; set; }
    }

    public class FilterDefinition
    {
        public string ColumnName { get; set; } = string.Empty;
        public string Operator { get; set; } = "=";
        public string? Value { get; set; }
        public bool IsParameter { get; set; } = false;
        public string? ParameterName { get; set; }
        public string? ParameterType { get; set; }
        public bool Required { get; set; } = false;
        public int GroupLevel { get; set; } = 0;
        public string? LogicalOperator { get; set; }
    }

    public class OrderByDefinition
    {
        public string ColumnName { get; set; } = string.Empty;
        public string Direction { get; set; } = "ASC";
    }

    public class ConditionalFormat
    {
        public string Condition { get; set; } = string.Empty;
        public string CssClass { get; set; } = string.Empty;
    }
}
