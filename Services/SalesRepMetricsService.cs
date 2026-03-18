using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models;
using SalesMetrics.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SalesMetrics.Services
{
    /// <summary>
    /// Shared service that provides Sales Rep metrics (New Accounts, Performance KPI,
    /// Lost &amp; Returning Customers).  Used by both SalespersonController and
    /// AccountsController so the logic lives in one place.
    /// </summary>
    public class SalesRepMetricsService
    {
        private readonly IConfiguration _config;

        public SalesRepMetricsService(IConfiguration config)
        {
            _config = config;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        public SalesRepMetricsViewModel GetSalesMetrics(
            int salesmanId, int locationId, DateTime startDate, DateTime endDate)
        {
            var metrics = new SalesRepMetricsViewModel
            {
                PropertyDetails = new List<SalesRepNewAccountSummaryViewModel>()
            };

            string salesmanName = GetSalespersonFullName(salesmanId, locationId);
            if (string.IsNullOrEmpty(salesmanName))
                return metrics;

            string officeLocation = LocationHelper.GetLocationCode(locationId);
            string connectionString = _config.GetConnectionString(officeLocation);

            using var conn = new SqlConnection(connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
                WITH NewAccounts AS (
                    SELECT
                        C.CUM_CUMMAS_ID as PropertyID,
                        C.CUM_CUSTOMER_NAME AS Property,
                        PC.IPC_PRICE_CODE AS [MgmtCoID],
                        PC.IPC_DESCRIPTION AS [MgmtCo],
                        SM.SMN_SALESMAN_NAME AS Salesperson,
                        SH.SOH_NUMBER AS [Order#],
                        IH.IHF_INVOICE_NUMBER AS [Invoice#],
                        CAST(C.CUM_ESTABLISHED_DATE AS DATE) AS [Date Created],
                        CAST(SH.SOH_DELIVERY_DATE AS DATE) AS [Date Installed],
                        ISNULL(SOH_TOTAL_AMOUNT, 0) AS OrderAmount,
                        ISNULL(IHF_TOTAL_AMOUNT, 0) AS InvoiceAmount
                    FROM CUSTOMER_MASTER C
                    LEFT JOIN SALESMAN_MASTER SM ON C.CUM_SMNMAS_ID = SM.SMN_SMNMAS_ID
                    LEFT JOIN PRICE_CODES PC ON C.CUM_PRICE_CODE = PC.IPC_PRICE_CODE
                    LEFT JOIN SALES_HEADER SH ON C.CUM_CUSTOMER_NUMBER = SH.SOH_CUSTOMER_NUMBER
                    LEFT JOIN INVOICE_HEADER IH ON SH.SOH_NUMBER = IH.IHF_ORDER_NUMBER
                    WHERE
                        C.CUM_ESTABLISHED_DATE >= @startDate
                        AND C.CUM_ESTABLISHED_DATE < @endDate
                        AND SH.SOH_CANCELED_DATE IS NULL
                        AND SH.SOH_WHSMAS_ID = 1
                        AND SH.SOH_SMNMAS_ID = @salespersonId
                        AND SM.SMN_SALESMAN_NAME = @salesperson
                )
                SELECT
                    MAX(PropertyID) as PropertyId,
                    Property,
                    [MgmtCo] AS ManagementCompany,
                    MAX([MgmtCoID]) as ManagementCoId,
                    MAX([Date Created]) as Established,
                    COUNT(DISTINCT [Order#]) AS Orders,
                    SUM(OrderAmount) AS TotalSalesAmount,
                    COUNT(DISTINCT [Invoice#]) AS Invoices,
                    SUM(InvoiceAmount) AS TotalInvoiceAmount
                FROM NewAccounts
                GROUP BY Property, [MgmtCo]
                ORDER BY Property;
            ", conn);

            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            cmd.Parameters.AddWithValue("@salespersonId", salesmanId);
            cmd.Parameters.AddWithValue("@salesperson", salesmanName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                metrics.PropertyDetails.Add(new SalesRepNewAccountSummaryViewModel
                {
                    PropertyId = Convert.ToInt32(reader["PropertyId"]),
                    Property = reader["Property"].ToString(),
                    ManagmentCoID = Convert.ToInt32(reader["ManagementCoId"]),
                    ManagementCompany = reader["ManagementCompany"].ToString(),
                    EstablishedDate = Convert.ToDateTime(reader["Established"]),
                    Orders = Convert.ToInt32(reader["Orders"]),
                    TotalSalesAmount = Convert.ToDecimal(reader["TotalSalesAmount"]),
                    Invoices = Convert.ToInt32(reader["Invoices"]),
                    TotalInvoiceAmount = Convert.ToDecimal(reader["TotalInvoiceAmount"])
                });
            }

            metrics.NewAccounts = metrics.PropertyDetails.Count;
            metrics.OrdersCount = metrics.PropertyDetails.Sum(x => x.Orders);
            metrics.InvoicesCount = metrics.PropertyDetails.Sum(x => x.Invoices);
            metrics.TotalSalesAmount = metrics.PropertyDetails.Sum(x => x.TotalSalesAmount);
            metrics.TotalInvoiceAmount = metrics.PropertyDetails.Sum(x => x.TotalInvoiceAmount);
            metrics.WithoutOrders = metrics.PropertyDetails.Count(x => x.Orders == 0);

            metrics.PerformanceKPI = GetPerformanceKPI(salesmanId, locationId, startDate, endDate);
            metrics.LostAndReturningCustomers = GetLostAndReturningCustomers(salesmanId, locationId, startDate, endDate);

            return metrics;
        }

        public string GetSalespersonFullName(int salesmanId, int locationId)
        {
            using var conn = new SqlConnection(_config.GetConnectionString("SalesMetrics"));
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT FirstName + ' ' + LastName AS FullName
                FROM Users
                WHERE SalesmanID = @SalesmanID
                    AND Location = @LocationID
            ", conn);
            cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
            cmd.Parameters.AddWithValue("@LocationID", locationId);

            return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private SalesRepPerformanceViewModel GetPerformanceKPI(
            int salesmanId, int locationId, DateTime startDate, DateTime endDate)
        {
            var performance = new SalesRepPerformanceViewModel();
            var monthData = new List<MonthlyPerformanceTrend>();

            string officeLocation = LocationHelper.GetLocationCode(locationId);
            string connectionString = _config.GetConnectionString(officeLocation);

            decimal sixMonthGoal = 0;
            decimal avgOrderAmount = 0;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // 1. Six-month rolling average → goal
                var goalCmd = new SqlCommand(@"
                    SELECT
                        FORMAT(IHF_INVOICE_DATE, 'yyyy-MM') AS MonthKey,
                        SUM(IHF_TOTAL_AMOUNT) AS TotalAmount
                    FROM INVOICE_HEADER
                    WHERE IHF_SMNMAS_ORDER = @SalesmanID
                      AND IHF_INVOICE_DATE >= DATEADD(MONTH, -6, GETDATE())
                      AND IHF_INVOICE_DATE <= DATEADD(DAY, -DAY(GETDATE()), GETDATE())
                      AND IHF_CANCELED_DATE IS NULL
                    GROUP BY FORMAT(IHF_INVOICE_DATE, 'yyyy-MM')
                ", conn);
                goalCmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

                var goalMonths = new List<decimal>();
                using (var reader = goalCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var amount = reader.IsDBNull("TotalAmount") ? 0 : Convert.ToDecimal(reader["TotalAmount"]);
                        if (amount > 0) goalMonths.Add(amount);
                    }
                }
                if (goalMonths.Any())
                    sixMonthGoal = Math.Round(goalMonths.Average() * 1.1m, 2);

                // 2. YTD sales + avg order amount
                DateTime ytdStart = new DateTime(DateTime.Today.Year, 1, 1);
                var ytdCmd = new SqlCommand(@"
                    SELECT
                        SUM(IHF_TOTAL_AMOUNT) AS YTDSales,
                        COUNT(DISTINCT IHF_INVOICE_NUMBER) AS InvoiceCount
                    FROM INVOICE_HEADER
                    WHERE IHF_SMNMAS_ORDER = @SalesmanID
                      AND IHF_INVOICE_DATE BETWEEN @YTDStart AND @EndDate
                      AND IHF_CANCELED_DATE IS NULL;
                ", conn);
                ytdCmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                ytdCmd.Parameters.AddWithValue("@YTDStart", ytdStart);
                ytdCmd.Parameters.AddWithValue("@EndDate", endDate);

                using (var reader = ytdCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        performance.YTDSales = reader.IsDBNull("YTDSales") ? 0 : Convert.ToDecimal(reader["YTDSales"]);
                        int invoiceCount = reader.IsDBNull("InvoiceCount") ? 0 : reader.GetInt32("InvoiceCount");
                        avgOrderAmount = invoiceCount > 0 ? performance.YTDSales / invoiceCount : 0;
                    }
                }

                // 3. Monthly actuals in selected range
                var monthlyCmd = new SqlCommand(@"
                    SELECT
                        FORMAT(IHF_INVOICE_DATE, 'yyyy-MM') AS MonthKey,
                        FORMAT(IHF_INVOICE_DATE, 'MMM yy') AS MonthLabel,
                        COUNT(DISTINCT IHF_INVOICE_NUMBER) AS InvoiceCount,
                        SUM(IHF_TOTAL_AMOUNT) AS TotalAmount
                    FROM INVOICE_HEADER
                    WHERE IHF_SMNMAS_ORDER = @SalesmanID
                        AND IHF_INVOICE_DATE BETWEEN @StartDate AND @EndDate
                        AND IHF_CANCELED_DATE IS NULL
                    GROUP BY FORMAT(IHF_INVOICE_DATE, 'yyyy-MM'), FORMAT(IHF_INVOICE_DATE, 'MMM yy')
                    ORDER BY MIN(IHF_INVOICE_DATE);
                ", conn);
                monthlyCmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                monthlyCmd.Parameters.AddWithValue("@StartDate", startDate);
                monthlyCmd.Parameters.AddWithValue("@EndDate", endDate);

                using (var reader = monthlyCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var label = reader.GetString("MonthLabel");
                        var count = reader.GetInt32("InvoiceCount");
                        var actual = reader.IsDBNull("TotalAmount") ? 0 : Convert.ToDecimal(reader["TotalAmount"]);
                        var expected = Math.Round(count * avgOrderAmount, 2);

                        monthData.Add(new MonthlyPerformanceTrend
                        {
                            MonthLabel = label,
                            Actual = actual,
                            Average = expected,
                            InvoiceCount = count,
                            Goal = sixMonthGoal
                        });
                    }
                }

                // 4. MTD
                var mtdCmd = new SqlCommand(@"
                    SELECT SUM(IHF_TOTAL_AMOUNT) AS MTDSales
                    FROM INVOICE_HEADER
                    WHERE IHF_SMNMAS_ORDER = @SalesmanID
                        AND MONTH(IHF_INVOICE_DATE) = MONTH(GETDATE())
                        AND YEAR(IHF_INVOICE_DATE) = YEAR(GETDATE())
                        AND IHF_CANCELED_DATE IS NULL;
                ", conn);
                mtdCmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                var mtdResult = mtdCmd.ExecuteScalar();
                performance.MTDSales = (mtdResult == null || mtdResult == DBNull.Value)
                    ? 0 : Convert.ToDecimal(mtdResult);

                performance.TargetSalesGoal = sixMonthGoal;
                performance.MonthlyPerformance = monthData
                    .OrderByDescending(m => DateTime.ParseExact(m.MonthLabel, "MMM yy", CultureInfo.InvariantCulture))
                    .ToList();
            }

            // 5. Task completion
            using (var conn = new SqlConnection(_config.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT
                        COUNT(*) AS TotalTasks,
                        SUM(CASE WHEN CompletedDate IS NOT NULL THEN 1 ELSE 0 END) AS CompletedTasks
                    FROM Tasks
                    WHERE AssignedTo IN (
                        SELECT Users_ID FROM Users WHERE SalesmanID = @SalesmanID AND Location = @LocationId
                    )
                    AND CancelledDate IS NULL;
                ", conn);
                cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                cmd.Parameters.AddWithValue("@LocationId", locationId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    performance.TaskCount = reader.GetInt32("TotalTasks");
                    performance.CompletedTasks = reader.IsDBNull("CompletedTasks") ? 0 : reader.GetInt32("CompletedTasks");
                }
            }

            // 6. Inactive customers
            string officeConn = _config.GetConnectionString(LocationHelper.GetLocationCode(locationId));
            using (var conn = new SqlConnection(officeConn))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT IHF_CUSTOMER_NUMBER) AS InactiveCount
                    FROM INVOICE_HEADER
                    WHERE IHF_SMNMAS_ORDER = @SalesmanID
                      AND IHF_ORDER_DATE < DATEADD(DAY, -60, GETDATE());
                ", conn);
                cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                var result = cmd.ExecuteScalar();
                performance.InactiveAccounts = (result == null || result == DBNull.Value)
                    ? 0 : Convert.ToInt32(result);
            }

            performance.PerformanceRating = performance.MTDSales >= performance.TargetSalesGoal
                ? "On Track" : "Needs Improvement";

            return performance;
        }

        private LostAndReturningCustomerSummaryViewModel GetLostAndReturningCustomers(
            int salesmanId, int locationId, DateTime startDate, DateTime endDate)
        {
            var summary = new LostAndReturningCustomerSummaryViewModel();

            string officeLocation = LocationHelper.GetLocationCode(locationId);
            string connectionString = _config.GetConnectionString(officeLocation);

            using var conn = new SqlConnection(connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
        WITH CustomerOrderHistory AS (
            SELECT
                C.CUM_CUMMAS_ID,
                SH.SOH_CUSTOMER_NUMBER,
                C.CUM_CUSTOMER_NAME,
                PC.IPC_DESCRIPTION AS ManagementCompany,
                SH.SOH_ORDER_DATE,
                COALESCE(A.ARO_INVOICE_AMOUNT, SH.SOH_TOTAL_AMOUNT, 0) AS ActualOrderAmount,
                A.ARO_INVOICE_BALANCE_DUE AS BalanceDue,
                SM.SMN_SMNMAS_ID AS SalesmanId,
                SM.SMN_SALESMAN_NAME AS SalesmanName,
                LAG(SH.SOH_ORDER_DATE) OVER (PARTITION BY SH.SOH_CUSTOMER_NUMBER ORDER BY SH.SOH_ORDER_DATE) AS PreviousOrderDate,
                LAG(SM.SMN_SMNMAS_ID) OVER (PARTITION BY SH.SOH_CUSTOMER_NUMBER ORDER BY SH.SOH_ORDER_DATE) AS PreviousSalesmanId,
                LAG(SM.SMN_SALESMAN_NAME) OVER (PARTITION BY SH.SOH_CUSTOMER_NUMBER ORDER BY SH.SOH_ORDER_DATE) AS PreviousSalesmanName,
                LAG(COALESCE(A.ARO_INVOICE_AMOUNT, SH.SOH_TOTAL_AMOUNT, 0)) OVER (PARTITION BY SH.SOH_CUSTOMER_NUMBER ORDER BY SH.SOH_ORDER_DATE) AS PreviousOrderAmount
            FROM SALES_HEADER SH
            INNER JOIN CUSTOMER_MASTER C ON SH.SOH_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
            INNER JOIN SALESMAN_MASTER SM ON SH.SOH_SMNMAS_ID = SM.SMN_SMNMAS_ID
            LEFT JOIN PRICE_CODES PC ON C.CUM_PRICE_CODE = PC.IPC_PRICE_CODE
            LEFT JOIN AR_OPEN_ITEM A ON SH.SOH_NUMBER = A.ARO_SALES_ORDER_NUMBER
            WHERE
                SH.SOH_CANCELED_DATE IS NULL
                AND SH.SOH_WHSMAS_ID = 1
                AND SH.SOH_ORDER_DATE >= DATEADD(MONTH, -36, @startDate)
                AND SH.SOH_ORDER_DATE <= @endDate
        ),
        CustomersWithGaps AS (
            SELECT
                CUM_CUMMAS_ID AS CustomerId,
                SOH_CUSTOMER_NUMBER AS CustomerNumber,
                CUM_CUSTOMER_NAME AS CustomerName,
                ManagementCompany,
                PreviousOrderDate AS LastOrderBeforeGap,
                PreviousSalesmanId,
                PreviousSalesmanName,
                PreviousOrderAmount,
                SOH_ORDER_DATE AS FirstOrderAfterReturn,
                SalesmanId AS ReturningSalesmanId,
                SalesmanName AS ReturningSalesmanName,
                ActualOrderAmount AS ReturningOrderAmount,
                BalanceDue AS ReturningBalanceDue,
                DATEDIFF(MONTH, PreviousOrderDate, SOH_ORDER_DATE) AS MonthsInactive
            FROM CustomerOrderHistory
            WHERE
                PreviousOrderDate IS NOT NULL
                AND DATEDIFF(MONTH, PreviousOrderDate, SOH_ORDER_DATE) >= 14
                AND SOH_ORDER_DATE BETWEEN @startDate AND @endDate
                AND (SalesmanId = @salesmanId OR PreviousSalesmanId = @salesmanId)
        ),
        ReturningCustomerSummary AS (
            SELECT
                CWG.CustomerId, CWG.CustomerNumber, CWG.CustomerName, CWG.ManagementCompany,
                CWG.LastOrderBeforeGap, CWG.PreviousSalesmanId, CWG.PreviousSalesmanName,
                CWG.PreviousOrderAmount, CWG.FirstOrderAfterReturn, CWG.ReturningSalesmanId,
                CWG.ReturningSalesmanName, CWG.ReturningOrderAmount, CWG.ReturningBalanceDue,
                CWG.MonthsInactive,
                COUNT(OH.SOH_CUSTOMER_NUMBER) AS OrdersSinceReturn,
                SUM(OH.ActualOrderAmount) AS TotalAmountSinceReturn,
                SUM(ISNULL(OH.BalanceDue, 0)) AS TotalBalanceDue
            FROM CustomersWithGaps CWG
            LEFT JOIN CustomerOrderHistory OH ON CWG.CustomerNumber = OH.SOH_CUSTOMER_NUMBER
                AND OH.SOH_ORDER_DATE >= CWG.FirstOrderAfterReturn
                AND OH.SOH_ORDER_DATE <= @endDate
            GROUP BY
                CWG.CustomerId, CWG.CustomerNumber, CWG.CustomerName, CWG.ManagementCompany,
                CWG.LastOrderBeforeGap, CWG.PreviousSalesmanId, CWG.PreviousSalesmanName,
                CWG.PreviousOrderAmount, CWG.FirstOrderAfterReturn, CWG.ReturningSalesmanId,
                CWG.ReturningSalesmanName, CWG.ReturningOrderAmount, CWG.ReturningBalanceDue,
                CWG.MonthsInactive
        )
        SELECT
            CustomerId, CustomerNumber, CustomerName,
            ISNULL(ManagementCompany, 'Unknown') AS ManagementCompany,
            LastOrderBeforeGap, PreviousSalesmanId, PreviousSalesmanName, PreviousOrderAmount,
            FirstOrderAfterReturn, ReturningSalesmanId, ReturningSalesmanName, ReturningOrderAmount,
            ReturningBalanceDue, MonthsInactive, OrdersSinceReturn, TotalAmountSinceReturn, TotalBalanceDue
        FROM ReturningCustomerSummary
        ORDER BY FirstOrderAfterReturn DESC;
            ", conn);

            cmd.Parameters.AddWithValue("@salesmanId", salesmanId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);

            using var reader = cmd.ExecuteReader();
            var customerDetails = new List<LostAndReturningCustomerViewModel>();

            while (reader.Read())
            {
                customerDetails.Add(new LostAndReturningCustomerViewModel
                {
                    CustomerId = Convert.ToInt32(reader["CustomerId"]),
                    CustomerNumber = Convert.ToInt32(reader["CustomerNumber"]),
                    CustomerName = reader["CustomerName"]?.ToString() ?? "",
                    ManagementCompany = reader["ManagementCompany"]?.ToString() ?? "",
                    LastOrderBeforeGap = Convert.ToDateTime(reader["LastOrderBeforeGap"]),
                    PreviousSalesperson = reader["PreviousSalesmanName"]?.ToString() ?? "",
                    PreviousSalesmanId = reader["PreviousSalesmanId"] != DBNull.Value ? Convert.ToInt32(reader["PreviousSalesmanId"]) : 0,
                    LastOrderAmount = reader.IsDBNull("PreviousOrderAmount") ? 0 : Convert.ToDecimal(reader["PreviousOrderAmount"]),
                    FirstOrderAfterReturn = Convert.ToDateTime(reader["FirstOrderAfterReturn"]),
                    ReturningSalesperson = reader["ReturningSalesmanName"]?.ToString() ?? "",
                    ReturningSalesmanId = reader["ReturningSalesmanId"] != DBNull.Value ? Convert.ToInt32(reader["ReturningSalesmanId"]) : 0,
                    ReturningOrderAmount = reader.IsDBNull("ReturningOrderAmount") ? 0 : Convert.ToDecimal(reader["ReturningOrderAmount"]),
                    ReturningBalanceDue = reader.IsDBNull("ReturningBalanceDue") ? 0 : Convert.ToDecimal(reader["ReturningBalanceDue"]),
                    TotalBalanceDue = reader.IsDBNull("TotalBalanceDue") ? 0 : Convert.ToDecimal(reader["TotalBalanceDue"]),
                    MonthsInactive = Convert.ToInt32(reader["MonthsInactive"]),
                    OrdersSinceReturn = Convert.ToInt32(reader["OrdersSinceReturn"]),
                    TotalAmountSinceReturn = reader.IsDBNull("TotalAmountSinceReturn") ? 0 : Convert.ToDecimal(reader["TotalAmountSinceReturn"])
                });
            }

            summary.CustomerDetails = customerDetails;
            summary.TotalReturningCustomers = customerDetails.Count;
            summary.CustomersWithSalespersonChange = customerDetails.Count(c => c.SalespersonChanged);
            summary.CustomersWithSameSalesperson = customerDetails.Count(c => !c.SalespersonChanged);
            summary.TotalRevenueRecovered = customerDetails.Sum(c => c.TotalAmountSinceReturn);
            summary.AverageGapInMonths = customerDetails.Any() ? (decimal)customerDetails.Average(c => c.MonthsInactive) : 0;
            summary.AverageReturningOrderValue = customerDetails.Any() ? customerDetails.Average(c => c.ReturningOrderAmount) : 0;
            summary.TotalOutstandingBalance = customerDetails.Sum(c => c.TotalBalanceDue);
            summary.CustomersWithOutstandingBalance = customerDetails.Count(c => c.HasOutstandingBalance);
            summary.OverallCollectionRate = summary.TotalRevenueRecovered > 0
                ? ((summary.TotalRevenueRecovered - summary.TotalOutstandingBalance) / summary.TotalRevenueRecovered) * 100
                : 0;

            return summary;
        }
    }
}
