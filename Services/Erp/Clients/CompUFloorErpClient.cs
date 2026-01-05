using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SalesMetrics.Services.Erp.Configuration;
using SalesMetrics.Services.Erp.Models;
using Dapper;

namespace SalesMetrics.Services.Erp.Clients
{
    /// <summary>
    /// CompUFloor ERP client implementation using direct SQL queries
    /// </summary>
    public class CompUFloorErpClient : IErpDataClient
    {
        private readonly IConfiguration _configuration;
        private readonly ErpSettings _erpSettings;
        private readonly ILogger<CompUFloorErpClient> _logger;

        public CompUFloorErpClient(
            IConfiguration configuration,
            IOptions<ErpSettings> erpSettings,
            ILogger<CompUFloorErpClient> logger)
        {
            _configuration = configuration;
            _erpSettings = erpSettings.Value;
            _logger = logger;
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private string GetConnectionString(ErpContext context)
        {
            var connectionName = context.ConnectionName ?? context.LocationCode ?? "SalesMetrics";
            var connectionString = _configuration.GetConnectionString(connectionName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{connectionName}' was not found.");
            }

            return connectionString;
        }

        private int[] GetWarehouseIds(ErpContext context)
        {
            var locationCode = context.LocationCode ?? ResolveLocationCode(context.LocationId);
            return _erpSettings.GetWarehouseIds(locationCode);
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

        // ============================================================
        // LEGACY METHOD - For backward compatibility
        // ============================================================

        public async Task<DataTable> QueryAsync(
            string queryName,
            string sql,
            object parameters,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            _logger.LogInformation(
                "Executing CompUFloor query {QueryName} against {LocationCode}",
                queryName, context.LocationCode);

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
        // PROPERTIES / CUSTOMERS
        // ============================================================

        public async Task<PagedResult<ErpPropertyReference>> SearchPropertiesAsync(
            string searchTerm,
            ErpContext context,
            int skip = 0,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    CUM_CUMMAS_ID as Id,
                    CUM_CUSTOMER_NAME as Name,
                    CUM_CUSTOMER_NUMBER as CustomerNumber
                FROM CUSTOMER_MASTER C
                WHERE C.CUM_CUSTOMER_NAME LIKE @SearchTerm
                ORDER BY C.CUM_CUSTOMER_NAME
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            await using var connection = new SqlConnection(connectionString);
            var results = await connection.QueryAsync<ErpPropertyReference>(
                sql,
                new { SearchTerm = $"%{searchTerm}%", Skip = skip, Take = take });

            return new PagedResult<ErpPropertyReference>
            {
                Items = results.ToList(),
                TotalCount = results.Count(),
                PageSize = take,
                CurrentPage = (skip / take) + 1
            };
        }

        public async Task<ErpProperty?> GetPropertyByIdAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    C.CUM_CUMMAS_ID as CustomerId,
                    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
                    C.CUM_CUSTOMER_NAME as CustomerName,
                    C.CUM_ADDRESS_1 as Address1,
                    C.CUM_ADDRESS_2 as Address2,
                    C.CUM_ADDRESS_3 as Address3,
                    C.CUM_CITY as City,
                    C.CUM_STATE as State,
                    C.CUM_ZIP as Zip,
                    C.CUM_PHONE_NUMBER as PhoneNumber,
                    C.CUM_EMAIL as Email,
                    C.CUM_CREDIT_LIMIT as CreditLimit,
                    C.CUM_AR_BALANCE as ARBalance,
                    C.CUM_CREDIT_HOLD_FLAG as CreditHoldFlag,
                    C.CUM_PRICE_CODE as PriceCode,
                    C.CUM_SMNMAS_ID as SalesmanId,
                    S.SMN_SALESMAN_NAME as SalesmanName,
                    C.CUM_DATE_ESTABLISHED as EstablishedDate,
                    C.CUM_ATTENTION_TO as AttentionTo,
                    C.CUM_PO_NUMBER_REQUIRED as PONumberRequired
                FROM CUSTOMER_MASTER C
                LEFT JOIN SALESMAN_MASTER S ON C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID
                WHERE C.CUM_CUMMAS_ID = @CustomerId";

            await using var connection = new SqlConnection(connectionString);
            return await connection.QueryFirstOrDefaultAsync<ErpProperty>(sql, new { CustomerId = customerId });
        }

        public Task<ErpPropertyDetails?> GetPropertyDetailsAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement - combine property data with AR and sales metrics
            throw new NotImplementedException("GetPropertyDetailsAsync will be implemented in next phase");
        }

        public async Task<List<ErpProperty>> GetAllPropertiesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);
            var warehouseIds = GetWarehouseIds(context);

            var sql = @"
                SELECT
                    C.CUM_CUMMAS_ID as CustomerId,
                    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
                    C.CUM_CUSTOMER_NAME as CustomerName,
                    C.CUM_ADDRESS_1 as Address1,
                    C.CUM_CITY as City,
                    C.CUM_STATE as State,
                    C.CUM_ZIP as Zip,
                    C.CUM_CREDIT_LIMIT as CreditLimit,
                    C.CUM_AR_BALANCE as ARBalance,
                    C.CUM_CREDIT_HOLD_FLAG as CreditHoldFlag,
                    C.CUM_PRICE_CODE as PriceCode,
                    C.CUM_SMNMAS_ID as SalesmanId,
                    S.SMN_SALESMAN_NAME as SalesmanName
                FROM CUSTOMER_MASTER C
                LEFT JOIN SALESMAN_MASTER S ON C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID
                ORDER BY C.CUM_CUSTOMER_NAME";

            await using var connection = new SqlConnection(connectionString);
            var results = await connection.QueryAsync<ErpProperty>(sql);
            return results.ToList();
        }

        public Task<Dictionary<string, List<ErpProperty>>> GetPropertiesByManagementAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement - group properties by management company
            throw new NotImplementedException("GetPropertiesByManagementAsync will be implemented in next phase");
        }

        // ============================================================
        // ORDERS
        // ============================================================

        public async Task<PagedResult<ErpOrder>> GetOrdersForPropertyAsync(
            int customerId,
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    S.SOH_SALOHD_ID as OrderId,
                    S.SOH_NUMBER as OrderNumber,
                    S.SOH_CUMMAS_ID as CustomerId,
                    C.CUM_CUSTOMER_NAME as CustomerName,
                    S.SOH_ORDER_DATE as OrderDate,
                    S.SOH_DELIVERY_DATE as DeliveryDate,
                    S.SOH_TOTAL_AMOUNT as TotalAmount,
                    S.SOH_WHSMAS_ID as WarehouseId,
                    S.SOH_OPERATOR as Operator,
                    CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END as IsOnlineOrder,
                    S.SOH_CANCELED_DATE as CanceledDate,
                    CASE WHEN S.SOH_CANCELED_DATE IS NOT NULL THEN 1 ELSE 0 END as IsCanceled
                FROM SALES_HEADER S
                INNER JOIN CUSTOMER_MASTER C ON S.SOH_CUMMAS_ID = C.CUM_CUMMAS_ID
                WHERE S.SOH_CUMMAS_ID = @CustomerId
                    AND (@StartDate IS NULL OR S.SOH_ORDER_DATE >= @StartDate)
                    AND (@EndDate IS NULL OR S.SOH_ORDER_DATE <= @EndDate)
                ORDER BY S.SOH_ORDER_DATE DESC
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            await using var connection = new SqlConnection(connectionString);
            var results = await connection.QueryAsync<ErpOrder>(
                sql,
                new { CustomerId = customerId, StartDate = startDate, EndDate = endDate, Skip = skip, Take = take });

            return new PagedResult<ErpOrder>
            {
                Items = results.ToList(),
                TotalCount = results.Count(),
                PageSize = take,
                CurrentPage = (skip / take) + 1
            };
        }

        public Task<ErpOrderDetails?> GetOrderDetailAsync(
            int orderId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement - get order with line items and notes
            throw new NotImplementedException("GetOrderDetailAsync will be implemented in next phase");
        }

        public Task<List<ErpOrder>> GetOrdersByDateRangeAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetOrdersByDateRangeAsync will be implemented in next phase");
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
            // TODO: Implement
            throw new NotImplementedException("GetInvoicesForPropertyAsync will be implemented in next phase");
        }

        public Task<ErpInvoiceDetails?> GetInvoiceDetailAsync(
            string invoiceNumber,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetInvoiceDetailAsync will be implemented in next phase");
        }

        public Task<decimal> GetPropertyARBalanceAsync(
            int customerId,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetPropertyARBalanceAsync will be implemented in next phase");
        }

        public Task<ErpARAgingSummary> GetARAgingSummaryAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetARAgingSummaryAsync will be implemented in next phase");
        }

        public Task<List<ErpOverdueInvoice>> GetOverdueInvoicesAsync(
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetOverdueInvoicesAsync will be implemented in next phase");
        }

        public Task<List<ErpMonthlyInvoiceSummary>> GetMonthlyInvoiceSummariesAsync(
            int customerId,
            ErpContext context,
            int months = 12,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetMonthlyInvoiceSummariesAsync will be implemented in next phase");
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
            // TODO: Implement - critical for dashboard
            throw new NotImplementedException("GetSalesMetricsAsync will be implemented in next phase");
        }

        public Task<List<ErpDailyOrderCount>> GetDailyOrderCountsAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetDailyOrderCountsAsync will be implemented in next phase");
        }

        public Task<List<ErpSalesmanMetrics>> GetSalesmanMetricsAsync(
            ErpContext context,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement
            throw new NotImplementedException("GetSalesmanMetricsAsync will be implemented in next phase");
        }

        // ============================================================
        // REFERENCE DATA
        // ============================================================

        public async Task<List<ErpSalesman>> GetSalesmenAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    SMN_SMNMAS_ID as SalesmanId,
                    SMN_SALESMAN_NAME as SalesmanName,
                    SMN_SALESMAN_NUMBER as SalesmanNumber,
                    1 as IsActive
                FROM SALESMAN_MASTER
                ORDER BY SMN_SALESMAN_NAME";

            await using var connection = new SqlConnection(connectionString);
            var results = await connection.QueryAsync<ErpSalesman>(sql);
            return results.ToList();
        }

        public async Task<List<ErpPriceCode>> GetPriceCodesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    IPC_PRICE_CODE as PriceCode,
                    IPC_DESCRIPTION as Description
                FROM PRICE_CODES
                ORDER BY IPC_PRICE_CODE";

            await using var connection = new SqlConnection(connectionString);
            var results = await connection.QueryAsync<ErpPriceCode>(sql);
            return results.ToList();
        }

        public async Task<List<ErpWarehouse>> GetWarehousesAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    WHS_WHSMAS_ID as WarehouseId,
                    WHS_WAREHOUSE_NUMBER as WarehouseNumber,
                    WHS_WAREHOUSE_NAME as WarehouseName
                FROM WAREHOUSE_MASTER
                ORDER BY WHS_WAREHOUSE_NUMBER";

            await using var connection = new SqlConnection(connectionString);
            var results = await connection.QueryAsync<ErpWarehouse>(sql);
            return results.ToList();
        }
    }
}
