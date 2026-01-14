using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// ViewModel for column configuration step
    /// </summary>
    public class ColumnConfigurationViewModel
    {
        public List<TableWithColumns> TablesWithColumns { get; set; } = new();
        public List<ColumnDefinition> SelectedColumns { get; set; } = new();
        public List<string> AvailableAggregates { get; set; } = new();
        public List<string> AvailableFormats { get; set; } = new()
        {
            "Default",
            "Currency ($#,##0.00)",
            "Percentage (0.00%)",
            "Date (MM/DD/YYYY)",
            "Date (YYYY-MM-DD)",
            "DateTime (MM/DD/YYYY HH:mm)",
            "Number (#,##0)",
            "Number (#,##0.00)"
        };
    }

    public class TableWithColumns
    {
        public string TableName { get; set; } = string.Empty;
        public string TableAlias { get; set; } = string.Empty;
        public List<ColumnMetadata> Columns { get; set; } = new();
    }
}
