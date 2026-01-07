using System.Data;
using System.Threading;
using Microsoft.Extensions.Logging;
using SalesMetrics.Services.Erp.Models;

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

        // ============================================================
        // STUB IMPLEMENTATIONS - To be implemented as needed
        // ============================================================

        public Task<PagedResult<ErpPropertyReference>> SearchPropertiesAsync(string searchTerm, ErpContext context, int skip = 0, int take = 20, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("SearchPropertiesAsync not yet implemented in HttpErpDataClient");
        }

        public Task<ErpProperty?> GetPropertyByIdAsync(int customerId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertyByIdAsync not yet implemented in HttpErpDataClient");
        }

        public Task<ErpPropertyDetails?> GetPropertyDetailsAsync(int customerId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertyDetailsAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpProperty>> GetAllPropertiesAsync(ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetAllPropertiesAsync not yet implemented in HttpErpDataClient");
        }

        public Task<Dictionary<string, List<ErpProperty>>> GetPropertiesByManagementAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertiesByManagementAsync not yet implemented in HttpErpDataClient");
        }

        public Task<PagedResult<ErpOrder>> GetOrdersForPropertyAsync(int customerId, ErpContext context, DateTime? startDate = null, DateTime? endDate = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOrdersForPropertyAsync not yet implemented in HttpErpDataClient");
        }

        public Task<ErpOrderDetails?> GetOrderDetailAsync(int orderId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOrderDetailAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpOrder>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate, ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOrdersByDateRangeAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpInvoice>> GetInvoicesForPropertyAsync(int customerId, ErpContext context, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetInvoicesForPropertyAsync not yet implemented in HttpErpDataClient");
        }

        public Task<ErpInvoiceDetails?> GetInvoiceDetailAsync(string invoiceNumber, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetInvoiceDetailAsync not yet implemented in HttpErpDataClient");
        }

        public Task<decimal> GetPropertyARBalanceAsync(int customerId, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPropertyARBalanceAsync not yet implemented in HttpErpDataClient");
        }

        public Task<ErpARAgingSummary> GetARAgingSummaryAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetARAgingSummaryAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpOverdueInvoice>> GetOverdueInvoicesAsync(ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetOverdueInvoicesAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpMonthlyInvoiceSummary>> GetMonthlyInvoiceSummariesAsync(int customerId, ErpContext context, int months = 12, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetMonthlyInvoiceSummariesAsync not yet implemented in HttpErpDataClient");
        }

        public Task<ErpSalesMetrics> GetSalesMetricsAsync(DateTime startDate, DateTime endDate, ErpContext context, int? salesmanId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetSalesMetricsAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpDailyOrderCount>> GetDailyOrderCountsAsync(DateTime startDate, DateTime endDate, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetDailyOrderCountsAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpSalesmanMetrics>> GetSalesmanMetricsAsync(ErpContext context, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetSalesmanMetricsAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpSalesman>> GetSalesmenAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetSalesmenAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpPriceCode>> GetPriceCodesAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetPriceCodesAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpWarehouse>> GetWarehousesAsync(ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetWarehousesAsync not yet implemented in HttpErpDataClient");
        }

        public Task<List<ErpRTJEntry>> GetRTJEntriesAsync(DateTime startDate, DateTime endDate, ErpContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GetRTJEntriesAsync not yet implemented in HttpErpDataClient");
        }
    }
}
