using System.Data;
using Microsoft.Extensions.Logging;
using SalesMetrics.Models.Reports;
using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Data source adapter for Kudu ERP (stub for future implementation)
    /// </summary>
    public class KuduDataSourceAdapter : IDataSourceAdapter
    {
        private readonly ILogger<KuduDataSourceAdapter> _logger;
        // TODO: Add IKuduApiClient when Kudu is ready

        public string SourceType => "Kudu";
        public string SourceName => "Kudu ERP";
        public bool IsAvailable => false; // Kudu not yet deployed

        public KuduDataSourceAdapter(ILogger<KuduDataSourceAdapter> logger)
        {
            _logger = logger;
            // TODO: Inject IKuduApiClient when available
        }

        public Task<List<TableMetadata>> GetAvailableTablesAsync(DataSourceContext context)
        {
            _logger.LogWarning("Kudu adapter not yet implemented");
            throw new NotImplementedException(
                "Kudu ERP data source adapter is not yet implemented. " +
                "This will be available when Kudu ERP is deployed.");
        }

        public Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context)
        {
            throw new NotImplementedException("Kudu adapter not yet implemented");
        }

        public Task<List<RelationshipMetadata>> GetTableRelationshipsAsync(string tableName, DataSourceContext context)
        {
            throw new NotImplementedException("Kudu adapter not yet implemented");
        }

        public Task<DataTable> ExecuteQueryAsync(
            QueryDefinition queryDef,
            ReportParameters parameters,
            DataSourceContext context)
        {
            throw new NotImplementedException("Kudu adapter not yet implemented");
        }

        public Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context)
        {
            throw new NotImplementedException("Kudu adapter not yet implemented");
        }

        public SourceCapabilities GetCapabilities()
        {
            // Return expected capabilities (adjust when Kudu specs are known)
            return new SourceCapabilities
            {
                SupportsJoins = true, // Assuming Kudu will support joins
                SupportsAggregates = true,
                SupportsSubqueries = false,
                SupportsComplexFilters = true,
                MaxRowsPerQuery = 5000,
                QueryTimeoutSeconds = 45,
                SupportedAggregates = new List<string> { "SUM", "AVG", "COUNT", "MIN", "MAX" },
                SupportedOperators = new List<string>
                {
                    "=", "!=", ">", "<", ">=", "<=", "LIKE", "IN", "BETWEEN"
                }
            };
        }

        #region Future Implementation Notes
        /*
         * When Kudu ERP is deployed, implement this adapter by:
         *
         * 1. Create IKuduApiClient interface:
         *    public interface IKuduApiClient
         *    {
         *        Task<List<KuduEntity>> GetEntitiesAsync();
         *        Task<List<KuduField>> GetEntityFieldsAsync(string entityName);
         *        Task<KuduQueryResult> ExecuteQueryAsync(KuduQuery query);
         *    }
         *
         * 2. Implement KuduApiClient with HttpClient
         *
         * 3. Convert QueryDefinition to Kudu's native query format
         *
         * 4. Handle Kudu-specific authentication and authorization
         *
         * 5. Map Kudu data types to normalized types
         *
         * 6. Implement proper error handling and logging
         *
         * Migration Strategy:
         * - Keep SqlDataSourceAdapter running in parallel
         * - Allow reports to specify fallback sources
         * - Gradually migrate reports one by one
         * - Compare results between sources during transition
         */
        #endregion
    }
}
