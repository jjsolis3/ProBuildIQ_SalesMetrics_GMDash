using System.Data;
using SalesMetrics.Models.Reports;
using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Abstraction layer for different data sources (SQL, API, Kudu, etc.)
    /// This interface allows the Query Builder to work with any data source
    /// </summary>
    public interface IDataSourceAdapter
    {
        /// <summary>
        /// Type of data source (SQL, API, Kudu)
        /// </summary>
        string SourceType { get; }

        /// <summary>
        /// Friendly name of the data source
        /// </summary>
        string SourceName { get; }

        /// <summary>
        /// Check if the data source is currently reachable
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Get list of available tables/entities from the data source
        /// </summary>
        Task<List<TableMetadata>> GetAvailableTablesAsync(DataSourceContext context);

        /// <summary>
        /// Get columns/fields for a specific table/entity
        /// </summary>
        Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context);

        /// <summary>
        /// Get relationship metadata for auto-suggesting joins
        /// </summary>
        Task<List<RelationshipMetadata>> GetTableRelationshipsAsync(string tableName, DataSourceContext context);

        /// <summary>
        /// Execute a query definition and return results as DataTable
        /// </summary>
        Task<DataTable> ExecuteQueryAsync(QueryDefinition queryDef, ReportParameters parameters, DataSourceContext context);

        /// <summary>
        /// Validate that a query definition is valid for this data source
        /// </summary>
        Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context);

        /// <summary>
        /// Get capabilities of this data source (what features it supports)
        /// </summary>
        SourceCapabilities GetCapabilities();
    }
}
