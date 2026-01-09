using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using SalesMetrics.Services.Erp.Models;

namespace SalesMetrics.Services.Erp
{
    public class SqlServerErpDataClient : IErpDataClient
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SqlServerErpDataClient> _logger;

        public SqlServerErpDataClient(IConfiguration configuration, ILogger<SqlServerErpDataClient> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DataTable> QueryAsync(string queryName, string sql, object parameters, ErpContext context, CancellationToken cancellationToken = default)
        {
            var connectionName = context.ConnectionName ?? context.LocationCode ?? "SalesMetrics";
            var connectionString = _configuration.GetConnectionString(connectionName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{connectionName}' was not found.");
            }

            _logger.LogInformation("Executing ERP query {QueryName} against connection {ConnectionName}", queryName, connectionName);

            await using var connection = new SqlConnection(connectionString);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            foreach (var property in parameters.GetType().GetProperties())
            {
                var value = property.GetValue(parameters) ?? DBNull.Value;
                command.Parameters.Add(new SqlParameter($"@{property.Name}", value));
            }

            await connection.OpenAsync(cancellationToken);
            var table = new DataTable(queryName);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            table.Load(reader);
            return table;
        }

        // ============================================================
        // STUB IMPLEMENTATIONS - To be implemented as needed
        // ============================================================

        public Task<PagedResult<ErpPropertyReference>> SearchPropertiesAsync(string searchTerm, ErpContext context, int skip = 0, int take = 20, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("SearchPropertiesAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<ErpProperty?> GetPropertyByIdAsync(int customerId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertyByIdAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<ErpPropertyDetails?> GetPropertyDetailsAsync(int customerId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertyDetailsAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpProperty>> GetAllPropertiesAsync(ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetAllPropertiesAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<Dictionary<string, List<ErpProperty>>> GetPropertiesByManagementAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertiesByManagementAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<PagedResult<ErpOrder>> GetOrdersForPropertyAsync(int customerId, ErpContext context, DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOrdersForPropertyAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<ErpOrderDetails?> GetOrderDetailAsync(int orderId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOrderDetailAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpOrder>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate, ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOrdersByDateRangeAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpInvoice>> GetInvoicesForPropertyAsync(int customerId, ErpContext context, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetInvoicesForPropertyAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<ErpInvoiceDetails?> GetInvoiceDetailAsync(string invoiceNumber, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetInvoiceDetailAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<decimal> GetPropertyARBalanceAsync(int customerId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertyARBalanceAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<ErpARAgingSummary> GetARAgingSummaryAsync(ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetARAgingSummaryAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpOverdueInvoice>> GetOverdueInvoicesAsync(ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOverdueInvoicesAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpMonthlyInvoiceSummary>> GetMonthlyInvoiceSummariesAsync(int customerId, ErpContext context, int months = 12, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetMonthlyInvoiceSummariesAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<ErpSalesMetrics> GetSalesMetricsAsync(DateTime startDate, DateTime endDate, ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetSalesMetricsAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpDailyOrderCount>> GetDailyOrderCountsAsync(DateTime startDate, DateTime endDate, ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetDailyOrderCountsAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpSalesmanMetrics>> GetSalesmanMetricsAsync(ErpContext context, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetSalesmanMetricsAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpSalesman>> GetSalesmenAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetSalesmenAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpPriceCode>> GetPriceCodesAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPriceCodesAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpWarehouse>> GetWarehousesAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetWarehousesAsync not yet implemented in SqlServerErpDataClient");
        }

        public Task<List<ErpRTJEntry>> GetRTJEntriesAsync(DateTime startDate, DateTime endDate, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetRTJEntriesAsync not yet implemented in SqlServerErpDataClient");
        }
    }
}
