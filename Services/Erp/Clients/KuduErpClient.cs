using System.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SalesMetrics.Services.Erp.Configuration;
using SalesMetrics.Services.Erp.Models;

namespace SalesMetrics.Services.Erp.Clients
{
    /// <summary>
    /// Kudu Pro ERP client implementation using REST API
    /// </summary>
    public class KuduErpClient : IErpDataClient
    {
        private readonly HttpClient _httpClient;
        private readonly ErpSettings _erpSettings;
        private readonly ILogger<KuduErpClient> _logger;

        public KuduErpClient(
            HttpClient httpClient,
            IOptions<ErpSettings> erpSettings,
            ILogger<KuduErpClient> logger)
        {
            _httpClient = httpClient;
            _erpSettings = erpSettings.Value;
            _logger = logger;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private ErpApiSettings GetApiSettings(ErpContext context)
        {
            var locationCode = context.LocationCode ?? ResolveLocationCode(context.LocationId);

            if (!_erpSettings.Locations.TryGetValue(locationCode, out var locationSettings))
            {
                throw new InvalidOperationException(
                    $"No Kudu API settings found for location {locationCode}");
            }

            if (locationSettings.ApiSettings == null)
            {
                throw new InvalidOperationException(
                    $"Kudu API settings are missing for location {locationCode}");
            }

            return locationSettings.ApiSettings;
        }

        private string ResolveLocationCode(int locationId)
        {
            return locationId switch
            {
                1 => "LAX",
                2 => "LSV",
                3 => "CHN",
                4 => "PHX",
                5 => "SND",
                _ => throw new ArgumentException($"Invalid location ID: {locationId}", nameof(locationId))
            };
        }

        private void ConfigureHttpClient(ErpApiSettings apiSettings)
        {
            _httpClient.BaseAddress = new Uri(apiSettings.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(apiSettings.TimeoutSeconds);

            // Add API key authentication
            if (!string.IsNullOrEmpty(apiSettings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiSettings.ApiKey);
            }

            // Add tenant ID header if provided
            if (!string.IsNullOrEmpty(apiSettings.TenantId))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", apiSettings.TenantId);
            }
        }

        // ============================================================
        // LEGACY METHOD - Not supported for API-based providers
        // ============================================================

        public Task<DataTable> QueryAsync(
            string queryName,
            string sql,
            object parameters,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Raw SQL queries are not supported for Kudu API client. " +
                "Please use the domain-specific methods instead.");
        }

        // ============================================================
        // PROPERTIES / CUSTOMERS
        // ============================================================

        public async Task<PagedResult<ErpPropertyReference>> SearchPropertiesAsync(
            string searchTerm,
            ErpContext context,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var apiSettings = GetApiSettings(context);
            ConfigureHttpClient(apiSettings);

            _logger.LogInformation(
                "Searching Kudu properties with term: {SearchTerm}",
                searchTerm);

            // TODO: Replace with actual Kudu API endpoint
            // Example: GET /api/v1/customers/search?q={term}&skip={skip}&take={take}
            var response = await _httpClient.GetAsync(
                $"/api/v1/customers/search?q={Uri.EscapeDataString(searchTerm)}&skip={skip}&take={take}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PagedResult<ErpPropertyReference>>(
                cancellationToken: cancellationToken);

            return result ?? new PagedResult<ErpPropertyReference>();
        }

        public async Task<ErpProperty?> GetPropertyByIdAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var apiSettings = GetApiSettings(context);
            ConfigureHttpClient(apiSettings);

            _logger.LogInformation(
                "Fetching Kudu property with ID: {CustomerId}",
                customerId);

            // TODO: Replace with actual Kudu API endpoint
            // Example: GET /api/v1/customers/{id}
            var response = await _httpClient.GetAsync(
                $"/api/v1/customers/{customerId}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ErpProperty>(
                cancellationToken: cancellationToken);
        }

        public Task<ErpPropertyDetails?> GetPropertyDetailsAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetPropertyDetailsAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpProperty>> GetAllPropertiesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetAllPropertiesAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<Dictionary<string, List<ErpProperty>>> GetPropertiesByManagementAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetPropertiesByManagementAsync for Kudu will be implemented once API documentation is available");
        }

        // ============================================================
        // ORDERS
        // ============================================================

        public Task<PagedResult<ErpOrder>> GetOrdersForPropertyAsync(
            int customerId,
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetOrdersForPropertyAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<ErpOrderDetails?> GetOrderDetailAsync(
            int orderId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetOrderDetailAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpOrder>> GetOrdersByDateRangeAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetOrdersByDateRangeAsync for Kudu will be implemented once API documentation is available");
        }

        // ============================================================
        // INVOICES & AR
        // ============================================================

        public Task<List<ErpInvoice>> GetInvoicesForPropertyAsync(
            int customerId,
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetInvoicesForPropertyAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<ErpInvoiceDetails?> GetInvoiceDetailAsync(
            string invoiceNumber,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetInvoiceDetailAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<decimal> GetPropertyARBalanceAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetPropertyARBalanceAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<ErpARAgingSummary> GetARAgingSummaryAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetARAgingSummaryAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpOverdueInvoice>> GetOverdueInvoicesAsync(
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetOverdueInvoicesAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpMonthlyInvoiceSummary>> GetMonthlyInvoiceSummariesAsync(
            int customerId,
            ErpContext context,
            int months = 12,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetMonthlyInvoiceSummariesAsync for Kudu will be implemented once API documentation is available");
        }

        // ============================================================
        // SALES METRICS
        // ============================================================

        public Task<ErpSalesMetrics> GetSalesMetricsAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetSalesMetricsAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpDailyOrderCount>> GetDailyOrderCountsAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetDailyOrderCountsAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpSalesmanMetrics>> GetSalesmanMetricsAsync(
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetSalesmanMetricsAsync for Kudu will be implemented once API documentation is available");
        }

        // ============================================================
        // REFERENCE DATA
        // ============================================================

        public Task<List<ErpSalesman>> GetSalesmenAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetSalesmenAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpPriceCode>> GetPriceCodesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetPriceCodesAsync for Kudu will be implemented once API documentation is available");
        }

        public Task<List<ErpWarehouse>> GetWarehousesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement when Kudu API is available
            throw new NotImplementedException(
                "GetWarehousesAsync for Kudu will be implemented once API documentation is available");
        }
    }
}
