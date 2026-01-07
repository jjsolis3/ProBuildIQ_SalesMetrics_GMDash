using System.Data;
using System.Threading;
using SalesMetrics.Services.Erp.Models;

namespace SalesMetrics.Services.Erp
{
    /// <summary>
    /// Abstraction layer for ERP data access - supports multiple ERP providers (CompUFloor, Kudu, etc.)
    /// </summary>
    public interface IErpDataClient
    {
        // ============================================================
        // LEGACY METHOD - Keep for backward compatibility during migration
        // ============================================================
        Task<DataTable> QueryAsync(string queryName, string sql, object parameters, ErpContext context, CancellationToken cancellationToken = default);

        // ============================================================
        // PROPERTIES / CUSTOMERS
        // ============================================================

        /// <summary>
        /// Search for properties by name or customer number
        /// </summary>
        Task<PagedResult<ErpPropertyReference>> SearchPropertiesAsync(
            string searchTerm,
            ErpContext context,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get property by customer ID
        /// </summary>
        Task<ErpProperty?> GetPropertyByIdAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get detailed property information including AR and sales data
        /// </summary>
        Task<ErpPropertyDetails?> GetPropertyDetailsAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all properties for a location
        /// </summary>
        Task<List<ErpProperty>> GetAllPropertiesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get properties grouped by management company
        /// </summary>
        Task<Dictionary<string, List<ErpProperty>>> GetPropertiesByManagementAsync(
            ErpContext context,
            CancellationToken cancellationToken = default);

        // ============================================================
        // ORDERS / WORK ORDERS
        // ============================================================

        /// <summary>
        /// Get orders for a specific property
        /// </summary>
        Task<PagedResult<ErpOrder>> GetOrdersForPropertyAsync(
            int customerId,
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get detailed order information including line items
        /// </summary>
        Task<ErpOrderDetails?> GetOrderDetailAsync(
            int orderId,
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get orders for a date range
        /// </summary>
        Task<List<ErpOrder>> GetOrdersByDateRangeAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default);

        // ============================================================
        // INVOICES & AR
        // ============================================================

        /// <summary>
        /// Get invoices for a specific property
        /// </summary>
        Task<List<ErpInvoice>> GetInvoicesForPropertyAsync(
            int customerId,
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get detailed invoice information
        /// </summary>
        Task<ErpInvoiceDetails?> GetInvoiceDetailAsync(
            string invoiceNumber,
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get AR balance for a specific property
        /// </summary>
        Task<decimal> GetPropertyARBalanceAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get AR aging report
        /// </summary>
        Task<ErpARAgingSummary> GetARAgingSummaryAsync(
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get overdue invoices
        /// </summary>
        Task<List<ErpOverdueInvoice>> GetOverdueInvoicesAsync(
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get monthly invoice summaries for a property
        /// </summary>
        Task<List<ErpMonthlyInvoiceSummary>> GetMonthlyInvoiceSummariesAsync(
            int customerId,
            ErpContext context,
            int months = 12,
            CancellationToken cancellationToken = default);

        // ============================================================
        // SALES METRICS & REPORTING
        // ============================================================

        /// <summary>
        /// Get aggregated sales metrics for a date range
        /// </summary>
        Task<ErpSalesMetrics> GetSalesMetricsAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get daily order counts for a date range
        /// </summary>
        Task<List<ErpDailyOrderCount>> GetDailyOrderCountsAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get salesman performance metrics
        /// </summary>
        Task<List<ErpSalesmanMetrics>> GetSalesmanMetricsAsync(
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default);

        // ============================================================
        // REFERENCE DATA
        // ============================================================

        /// <summary>
        /// Get all salesmen for a location
        /// </summary>
        Task<List<ErpSalesman>> GetSalesmenAsync(
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all price codes
        /// </summary>
        Task<List<ErpPriceCode>> GetPriceCodesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all warehouses for a location
        /// </summary>
        Task<List<ErpWarehouse>> GetWarehousesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default);

        // ============================================================
        // INVENTORY ADJUSTMENTS (RTJ)
        // ============================================================

        /// <summary>
        /// Get Receive-Transfer-Journal (RTJ) inventory adjustment entries for a date range
        /// </summary>
        Task<List<ErpRTJEntry>> GetRTJEntriesAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            CancellationToken cancellationToken = default);
    }
}
