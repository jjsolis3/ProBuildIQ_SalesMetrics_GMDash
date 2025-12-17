using System.Data;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SalesMetrics.Services.Erp
{
    public class HttpErpDataClient : IErpDataClient
    {
        private readonly ILogger<HttpErpDataClient> _logger;

        public HttpErpDataClient(ILogger<HttpErpDataClient> logger)
        {
            _logger = logger;
        }

        public Task<DataTable> QueryAsync(string queryName, string sql, object parameters, ErpContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("HTTP ERP client is not yet implemented. Query {QueryName} was not executed.", queryName);
            return Task.FromResult(new DataTable(queryName));
        }
    }
}
