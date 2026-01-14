using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// ViewModel for filter configuration step
    /// </summary>
    public class FilterConfigurationViewModel
    {
        public List<FilterDefinition> Filters { get; set; } = new();
        public List<ColumnOption> AvailableColumns { get; set; } = new();
        public List<string> AvailableOperators { get; set; } = new();
        public List<string> AvailableParameterTypes { get; set; } = new()
        {
            "text", "number", "date", "select", "multiselect"
        };
    }

    public class ColumnOption
    {
        public string TableName { get; set; } = string.Empty;
        public string TableAlias { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string FullExpression => $"{TableAlias}.{ColumnName}";
    }
}
