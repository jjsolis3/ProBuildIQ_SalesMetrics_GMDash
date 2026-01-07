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
                    C.CUM_ATTENTION_TO as AttentionTo,
                    C.CUM_SMNMAS_ID as SalesmanId,
                    S.SMN_SALESMAN_NAME as SalesmanName,
                    C.CUM_PO_NUMBER_REQUIRED as PONumberRequired,
                    P.IPC_DESCRIPTION as ManagementCompany,
                    COALESCE(
                        (SELECT TOP 1 SOH_WHSMAS_ID
                         FROM SALES_HEADER
                         WHERE SOH_CUMMAS_ID = C.CUM_CUMMAS_ID
                           AND SOH_WHSMAS_ID IS NOT NULL
                           AND SOH_WHSMAS_ID <> 1
                         ORDER BY SOH_NUMBER DESC),
                        (SELECT TOP 1 SOH_WHSMAS_ID
                         FROM SALES_HEADER
                         WHERE SOH_CUMMAS_ID = C.CUM_CUMMAS_ID
                           AND SOH_WHSMAS_ID IS NOT NULL
                         ORDER BY SOH_NUMBER DESC)
                    ) as WarehouseId
                FROM CUSTOMER_MASTER C
                LEFT JOIN SALESMAN_MASTER S ON C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID
                LEFT JOIN PRICE_CODES P ON C.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                WHERE C.CUM_CUSTOMER_NUMBER NOT IN (
                    SELECT CAST(IM.INS_INSTALLER_NUMBER AS NVARCHAR(50))
                    FROM INSTALLER_MASTER IM
                    WHERE ISNUMERIC(IM.INS_INSTALLER_NUMBER) = 1
                )
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

        public async Task<ErpARAgingSummary> GetARAgingSummaryAsync(
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                -- Pending Invoices Count
                SELECT
                    COUNT(*) AS PendingInvoices,
                    ISNULL(SUM(A.ARO_INVOICE_BALANCE_DUE), 0) AS PendingInvoicesAmount
                FROM AR_OPEN_ITEM AS A
                    LEFT JOIN INVOICE_HEADER as I on A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                WHERE ARO_INVOICE_BALANCE_DUE > 0
                    AND ARO_DATE_PAID_IN_FULL IS NULL;

                -- 30-60 Days
                SELECT
                    COUNT(*) AS Due30to60,
                    ISNULL(SUM(A.ARO_INVOICE_BALANCE_DUE), 0) AS Due30to60Amount
                FROM AR_OPEN_ITEM AS A
                    LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                WHERE A.ARO_INVOICE_BALANCE_DUE > 0
                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59;

                -- 60-90 Days
                SELECT
                    COUNT(*) AS Due60to90,
                    ISNULL(SUM(A.ARO_INVOICE_BALANCE_DUE), 0) AS Due60to90Amount
                FROM AR_OPEN_ITEM AS A
                    LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                WHERE A.ARO_INVOICE_BALANCE_DUE > 0
                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89;

                -- 90-120 Days
                SELECT
                    COUNT(*) AS Due90to120,
                    ISNULL(SUM(A.ARO_INVOICE_BALANCE_DUE), 0) AS Due90to120Amount
                FROM AR_OPEN_ITEM AS A
                    LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                WHERE A.ARO_INVOICE_BALANCE_DUE > 0
                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 119;

                -- Over 120 Days
                SELECT
                    COUNT(*) AS DueOver120,
                    ISNULL(SUM(A.ARO_INVOICE_BALANCE_DUE), 0) AS DueOver120Amount
                FROM AR_OPEN_ITEM AS A
                    LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                WHERE A.ARO_INVOICE_BALANCE_DUE > 0
                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) >= 120;

                -- Under 30 Days
                SELECT
                    COUNT(*) AS DueUnder30,
                    ISNULL(SUM(A.ARO_INVOICE_BALANCE_DUE), 0) AS DueUnder30Amount
                FROM AR_OPEN_ITEM AS A
                    LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                WHERE A.ARO_INVOICE_BALANCE_DUE > 0
                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) < 30;

                -- Top Delinquent Customers
                SELECT TOP 10
                    C.CUM_CUSTOMER_NAME as CustomerName,
                    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
                    C.CUM_CUMMAS_ID as CustomerId,
                    SUM(A.ARO_INVOICE_BALANCE_DUE) as OutstandingAmount
                FROM AR_OPEN_ITEM A
                    LEFT JOIN CUSTOMER_MASTER C ON A.ARO_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
                WHERE A.ARO_INVOICE_BALANCE_DUE > 0
                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                GROUP BY C.CUM_CUSTOMER_NAME, C.CUM_CUSTOMER_NUMBER, C.CUM_CUMMAS_ID
                ORDER BY OutstandingAmount DESC";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var summary = new ErpARAgingSummary();

            // Read pending invoices
            if (await reader.ReadAsync())
            {
                summary.PendingInvoices = reader.GetInt32(0);
                summary.PendingInvoicesAmount = Convert.ToDecimal(reader.GetDouble(1));
            }

            // Read 30-60 days
            await reader.NextResultAsync(cancellationToken);
            if (await reader.ReadAsync())
            {
                summary.Due30to60 = reader.GetInt32(0);
                summary.Due30to60Amount = Convert.ToDecimal(reader.GetDouble(1));
            }

            // Read 60-90 days
            await reader.NextResultAsync(cancellationToken);
            if (await reader.ReadAsync())
            {
                summary.Due60to90 = reader.GetInt32(0);
                summary.Due60to90Amount = Convert.ToDecimal(reader.GetDouble(1));
            }

            // Read 90-120 days
            await reader.NextResultAsync(cancellationToken);
            if (await reader.ReadAsync())
            {
                summary.Due90to120 = reader.GetInt32(0);
                summary.Due90to120Amount = Convert.ToDecimal(reader.GetDouble(1));
            }

            // Read over 120 days
            await reader.NextResultAsync(cancellationToken);
            if (await reader.ReadAsync())
            {
                summary.DueOver120 = reader.GetInt32(0);
                summary.DueOver120Amount = Convert.ToDecimal(reader.GetDouble(1));
            }

            // Read under 30 days
            await reader.NextResultAsync(cancellationToken);
            if (await reader.ReadAsync())
            {
                summary.DueUnder30 = reader.GetInt32(0);
                summary.DueUnder30Amount = Convert.ToDecimal(reader.GetDouble(1));
            }

            // Read top delinquent customers
            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync())
            {
                var customerNumberStr = reader.IsDBNull(1) ? "0" : reader.GetString(1);
                int.TryParse(customerNumberStr, out var customerNumber);

                summary.TopDelinquentCustomers.Add(new ErpCustomerOutstanding
                {
                    CustomerName = reader.GetString(0),
                    CustomerNumber = customerNumber,
                    CustomerId = reader.GetInt32(2),
                    OutstandingAmount = Convert.ToDecimal(reader.GetDouble(3)),
                    BalanceDue = Convert.ToDecimal(reader.GetDouble(3))
                });
            }

            return summary;
        }

        public async Task<List<ErpOverdueInvoice>> GetOverdueInvoicesAsync(
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    I.IHF_INVOICE_NUMBER AS InvoiceNumber,
                    C.CUM_CUMMAS_ID AS CustomerId,
                    I.IHF_BILLTO_NAME AS CustomerName,
                    P.IPC_DESCRIPTION as ManagementCompany,
                    CAST(A.ARO_INVOICE_BALANCE_DUE AS DECIMAL(18, 2)) AS OutstandingAmount,
                    CAST(A.ARO_DUE_DATE as DATE) as DueDate,
                    DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) as DaysPastDue,
                    CASE
                        WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) < 30 THEN 'Under 30 Days'
                        WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59 THEN '30 to 60 Days'
                        WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89 THEN '60 to 90 Days'
                        WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 119 THEN '90 to 120 Days'
                        WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) >= 120 THEN 'Over 120 Days'
                        ELSE ''
                    END as InvoiceAging,
                    SM.SMN_SMNMAS_ID as SalesmanId,
                    SM.SMN_SALESMAN_NAME as SalesmanName
                FROM AR_OPEN_ITEM A
                    LEFT JOIN INVOICE_HEADER I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    LEFT JOIN CUSTOMER_MASTER C on A.ARO_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
                    LEFT JOIN SALESMAN_MASTER AS SM ON I.IHF_SMNMAS_ORDER = SM.SMN_SMNMAS_ID
                    LEFT JOIN PRICE_CODES as P on I.IHF_PRICE_CODE = P.IPC_PRICE_CODE
                WHERE A.ARO_INVOICE_BALANCE_DUE > 0
                    AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) > 30
                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    AND I.IHF_INVOICE_NUMBER IS NOT NULL";

            if (salesmanId.HasValue && salesmanId.Value > 0)
            {
                sql += " AND I.IHF_SMNMAS_ORDER = @SalesmanId";
            }

            sql += " ORDER BY DaysPastDue DESC";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);

            if (salesmanId.HasValue && salesmanId.Value > 0)
            {
                command.Parameters.AddWithValue("@SalesmanId", salesmanId.Value);
            }

            var results = new List<ErpOverdueInvoice>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync())
            {
                results.Add(new ErpOverdueInvoice
                {
                    InvoiceNumber = reader.GetInt32(0).ToString(),
                    CustomerId = reader.GetInt32(1),
                    CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ManagementCompany = reader.IsDBNull(3) ? null : reader.GetString(3),
                    OutstandingAmount = reader.GetDecimal(4),
                    DueDate = reader.GetDateTime(5),
                    DaysPastDue = reader.GetInt32(6),
                    InvoiceAging = reader.GetString(7),
                    SalesmanId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    SalesmanName = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return results;
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

        public async Task<ErpSalesMetrics> GetSalesMetricsAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            int? salesmanId = null,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);
            var warehouseIds = GetWarehouseIds(context);

            var sql = @"
                SELECT
                    SUM(AR.ARO_INVOICE_AMOUNT) AS TotalRevenue,
                    SUM(AR.ARO_INVOICE_BALANCE_DUE) AS AmountDue,
                    COUNT(DISTINCT I.IHF_CUSTOMER_NUMBER) AS PropertyCount,
                    COUNT(I.IHF_INVOICE_NUMBER) AS TotalInvoices,
                    SUM(CASE WHEN AR.ARO_INVOICE_BALANCE_DUE = 0 THEN 1 ELSE 0 END) AS PaidInvoices,
                    SUM(CASE WHEN AR.ARO_DUE_DATE < GETDATE() AND AR.ARO_INVOICE_BALANCE_DUE > 0 THEN 1 ELSE 0 END) AS OverdueInvoices,
                    AVG(AR.ARO_INVOICE_AMOUNT) AS AvgInvoiceAmount,
                    MAX(AR.ARO_INVOICE_AMOUNT) AS MaxInvoiceAmount,
                    MIN(AR.ARO_INVOICE_AMOUNT) AS MinInvoiceAmount,
                    COUNT(DISTINCT CASE WHEN I.IHF_OPERATOR = 'Online' THEN I.IHF_INVOICE_NUMBER END) AS OnlineOrders
                FROM AR_OPEN_ITEM AS AR
                    LEFT JOIN INVOICE_HEADER AS I ON AR.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    LEFT JOIN SALESMAN_MASTER AS SM ON I.IHF_SMNMAS_ORDER = SM.SMN_SMNMAS_ID
                WHERE I.IHF_INVOICE_DATE >= @StartDate
                    AND I.IHF_INVOICE_DATE <= @EndDate
                    AND I.IHF_CANCELED_DATE IS NULL";

            if (salesmanId.HasValue && salesmanId.Value > 0)
            {
                sql += " AND SM.SMN_SMNMAS_ID = @SalesmanId";
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@StartDate", startDate);
            command.Parameters.AddWithValue("@EndDate", endDate);

            if (salesmanId.HasValue && salesmanId.Value > 0)
            {
                command.Parameters.AddWithValue("@SalesmanId", salesmanId.Value);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync())
            {
                var totalRevenue = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetDouble(0));
                var amountDue = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetDouble(1));
                var propertyCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                var totalInvoices = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                var paidInvoices = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                var overdueInvoices = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                var avgInvoiceAmount = reader.IsDBNull(6) ? 0m : Convert.ToDecimal(reader.GetDouble(6));
                var maxInvoiceAmount = reader.IsDBNull(7) ? 0m : Convert.ToDecimal(reader.GetDouble(7));
                var minInvoiceAmount = reader.IsDBNull(8) ? 0m : Convert.ToDecimal(reader.GetDouble(8));
                var onlineOrders = reader.IsDBNull(9) ? 0 : reader.GetInt32(9);

                return new ErpSalesMetrics
                {
                    TotalRevenue = totalRevenue,
                    TotalInvoices = totalInvoices,
                    TotalOrders = totalInvoices,
                    OnlineOrders = onlineOrders,
                    InStoreOrders = totalInvoices - onlineOrders,
                    TotalOrderAmount = totalRevenue,
                    AverageOrderValue = avgInvoiceAmount,
                    AmountDue = amountDue,
                    PropertyCount = propertyCount,
                    PropertyNameCount = propertyCount, // Same as PropertyCount for CompUFloor
                    PaidInvoices = paidInvoices,
                    OverdueInvoices = overdueInvoices,
                    MaxInvoiceAmount = maxInvoiceAmount,
                    MinInvoiceAmount = minInvoiceAmount,
                    StartDate = startDate,
                    EndDate = endDate,
                    LocationCode = context.LocationCode
                };
            }

            return new ErpSalesMetrics
            {
                StartDate = startDate,
                EndDate = endDate,
                LocationCode = context.LocationCode
            };
        }

        public async Task<List<ErpDailyOrderCount>> GetDailyOrderCountsAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    CAST(SH.SOH_DELIVERY_DATE AS DATE) AS OrderDate,
                    DATENAME(WEEKDAY, SH.SOH_DELIVERY_DATE) AS WeekdayName,
                    COUNT(DISTINCT SH.SOH_NUMBER) AS OrdersCount,
                    SUM(CASE
                        WHEN SH.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NOT NULL THEN AR.ARO_INVOICE_AMOUNT
                        WHEN SH.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NULL THEN 0
                        ELSE SH.SOH_TOTAL_AMOUNT
                    END) AS TotalOrderAmount
                FROM SALES_HEADER AS SH
                LEFT JOIN AR_OPEN_ITEM AR ON SH.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                WHERE SH.SOH_DELIVERY_DATE BETWEEN @StartDate AND @EndDate
                    AND SH.SOH_CURRENT_STATUS <> 4
                    AND SH.SOH_CANCELED_DATE IS NULL
                GROUP BY
                    CAST(SH.SOH_DELIVERY_DATE AS DATE),
                    DATENAME(WEEKDAY, SH.SOH_DELIVERY_DATE),
                    DATEPART(WEEKDAY, SH.SOH_DELIVERY_DATE)
                ORDER BY CAST(SH.SOH_DELIVERY_DATE AS DATE)";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@StartDate", startDate);
            command.Parameters.AddWithValue("@EndDate", endDate);

            var results = new List<ErpDailyOrderCount>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync())
            {
                results.Add(new ErpDailyOrderCount
                {
                    Date = reader.GetDateTime(0),
                    WeekdayName = reader.GetString(1),
                    OrdersCount = reader.GetInt32(2),
                    TotalOrderAmount = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetDouble(3))
                });
            }

            return results;
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

        // ============================================================
        // INVENTORY ADJUSTMENTS (RTJ)
        // ============================================================

        public async Task<List<ErpRTJEntry>> GetRTJEntriesAsync(
            DateTime startDate,
            DateTime endDate,
            ErpContext context,
            CancellationToken cancellationToken = default)
        {
            var connectionString = GetConnectionString(context);

            var sql = @"
                SELECT
                    G.GLJ_REFERENCE_NUMBER as AdjustmentComments,
                    G.GLJ_JOURNAL_NUMBER as JournalNumber,
                    G.GLJ_CREDIT_AMOUNT as Credit,
                    G.GLJ_DEBIT_AMOUNT as Debit,
                    CAST(G.GLJ_TRANSACTION_DATE as DATE) as Date,
                    G.GLJ_WAREHOUSE_NUMBER as WarehouseNumber
                FROM GL_JOURNAL G
                WHERE G.GLJ_ACCOUNT_NUMBER = 12970
                    AND G.GLJ_TRANSACTION_DATE BETWEEN @StartDate AND @EndDate
                    AND G.GLJ_WAREHOUSE_NUMBER IN (1, 2, 3, 6, 11, 86, 90)
                    AND G.GLJ_REFERENCE_NUMBER LIKE '%RTJ%'
                    AND G.GLJ_REFERENCE_NUMBER NOT LIKE '%1226%'
                    AND G.GLJ_REFERENCE_NUMBER NOT LIKE '%12to 6%'
                    AND G.GLJ_REFERENCE_NUMBER NOT LIKE '%12 to 6%'
                ORDER BY G.GLJ_DEBIT_AMOUNT DESC, G.GLJ_CREDIT_AMOUNT ASC";

            await using var connection = new SqlConnection(connectionString);
            var results = await connection.QueryAsync<ErpRTJEntry>(
                sql,
                new { StartDate = startDate, EndDate = endDate },
                cancellationToken: cancellationToken);

            // Set location for each entry
            var locationCode = context.LocationCode ?? "Unknown";
            var entries = results.ToList();
            foreach (var entry in entries)
            {
                entry.Location = locationCode;
            }

            return entries;
        }
    }
}
