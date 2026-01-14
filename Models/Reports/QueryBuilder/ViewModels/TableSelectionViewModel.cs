using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// ViewModel for table selection step
    /// </summary>
    public class TableSelectionViewModel
    {
        public string DataSourceType { get; set; } = "SQL";
        public List<TableMetadata> AvailableTables { get; set; } = new();
        public List<TableMetadata> SelectedTables { get; set; } = new();
        public List<RelationshipMetadata> AvailableRelationships { get; set; } = new();
        public List<string> Categories { get; set; } = new(); // For filtering tables
    }
}
