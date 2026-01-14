using System.Data;
using Microsoft.Extensions.Logging;
using SalesMetrics.Models.Reports;
using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Data source adapter for REST/GraphQL APIs (stub for future implementation)
    /// </summary>
    public class ApiDataSourceAdapter : IDataSourceAdapter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApiDataSourceAdapter> _logger;

        public string SourceType => "API";
        public string SourceName => "External API (REST/GraphQL)";
        public bool IsAvailable => false; // Not yet configured

        public ApiDataSourceAdapter(
            IHttpClientFactory httpClientFactory,
            ILogger<ApiDataSourceAdapter> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Task<List<TableMetadata>> GetAvailableTablesAsync(DataSourceContext context)
        {
            _logger.LogWarning("API adapter not yet implemented");
            throw new NotImplementedException(
                "API data source adapter is not yet implemented. " +
                "This will be available when API-based ERP systems are integrated.");
        }

        public Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context)
        {
            throw new NotImplementedException("API adapter not yet implemented");
        }

        public Task<List<RelationshipMetadata>> GetTableRelationshipsAsync(string tableName, DataSourceContext context)
        {
            throw new NotImplementedException("API adapter not yet implemented");
        }

        public Task<DataTable> ExecuteQueryAsync(
            QueryDefinition queryDef,
            ReportParameters parameters,
            DataSourceContext context)
        {
            throw new NotImplementedException("API adapter not yet implemented");
        }

        public Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context)
        {
            throw new NotImplementedException("API adapter not yet implemented");
        }

        public SourceCapabilities GetCapabilities()
        {
            return new SourceCapabilities
            {
                SupportsJoins = false, // Most APIs don't support server-side joins
                SupportsAggregates = true,
                SupportsSubqueries = false,
                SupportsComplexFilters = false,
                MaxRowsPerQuery = 1000,
                QueryTimeoutSeconds = 60,
                SupportedAggregates = new List<string> { "SUM", "AVG", "COUNT", "MIN", "MAX" },
                SupportedOperators = new List<string> { "=", "!=", ">", "<", ">=", "<=", "IN" }
            };
        }

        #region Future Implementation Notes
        /*
         * When implementing this adapter, you'll need to:
         *
         * 1. Configure API endpoint in appsettings.json:
         *    "ExternalApi": {
         *      "BaseUrl": "https://api.example.com",
         *      "ApiKey": "your-api-key"
         *    }
         *
         * 2. Implement GetAvailableTablesAsync():
         *    - Call GET /api/v1/metadata/tables
         *    - Parse response into TableMetadata list
         *
         * 3. Implement ExecuteQueryAsync():
         *    - Convert QueryDefinition to API query format (OData, GraphQL, etc.)
         *    - POST to /api/v1/query
         *    - Convert JSON response to DataTable
         *
         * 4. Handle pagination, rate limiting, auth tokens
         */
        #endregion
    }
}
