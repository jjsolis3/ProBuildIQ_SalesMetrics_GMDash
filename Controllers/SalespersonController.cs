using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SalesMetrics.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SalesMetrics.Models.EFCore;
using System.Globalization;

namespace SalesMetrics.Controllers
{
    public class SalespersonController : Controller
    {
        private readonly IConfiguration _config;

        public SalespersonController(IConfiguration config)
        {
            _config = config;
        }


        [HttpGet]
        public IActionResult Index(int? selectedSalesmanId, DateTime? startDate, DateTime? endDate)
        {
            var roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            var locationId = LocationHelper.GetCurrentLocationId(HttpContext);

            var dateFormat = "MM/dd/yyyy";
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            // Try to parse, or fallback to defaults
            var parsedStartDate = startDate ?? DateTime.UtcNow.AddMonths(-1).AddDays(-DateTime.UtcNow.Day + 1);
            var parsedEndDate = endDate ?? DateTime.UtcNow;

            //###########  

            var viewModel = new SalesRepProfilePageViewModel()
            {
                // Dropdown list of all sales reps
                SalesReps = GetAllSalesReps(roleId, locationId),
                StartDate = parsedStartDate,
                EndDate = parsedEndDate

            };

            if (selectedSalesmanId.HasValue)
            {
                viewModel.Metrics = GetSalesMetrics(selectedSalesmanId.Value, locationId, viewModel.StartDate, viewModel.EndDate);
                viewModel.UserProfile = GetUserProfileBySalesmanId(selectedSalesmanId.Value, locationId);
                viewModel.SelectedSalesmanId = selectedSalesmanId.Value;
            }

            return View(viewModel);
        }

        private List<SelectListItem> GetAllSalesReps(int roleId, int locationId)
        {
            var list = new List<SelectListItem>();

            string sqlQuery = @"
                SELECT 
                    DISTINCT SalesmanID, 
                    FirstName + ' ' + LastName AS FullName 
                    FROM Users 
                    WHERE RoleID = 2";

            if(roleId == 1 || roleId == 4)
                sqlQuery += " AND Location = @locationId";
            
            sqlQuery += @" ORDER BY FullName";

            using var conn = new SqlConnection(_config.GetConnectionString("SalesMetrics"));
            conn.Open();
            
            var cmd = new SqlCommand(sqlQuery, conn);

            if (roleId == 1 || roleId == 4)
                cmd.Parameters.AddWithValue("@locationId", locationId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SelectListItem
                {
                    Value = reader["SalesmanID"].ToString(),
                    Text = reader["FullName"].ToString()
                });
            }

            return list;
        }

        private UserProfileViewModel GetUserProfileBySalesmanId(int salesmanId, int locationId)
        {
            var model = new UserProfileViewModel();

            using var conn = new SqlConnection(_config.GetConnectionString("SalesMetrics"));
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT FirstName, LastName, Email, GoogleEmail, GoogleAccessToken, GoogleRefreshToken,
                       SalesmanID, SalesmanNumber, Location
                FROM Users
                WHERE SalesmanID = @SalesmanID
                    AND Location = @LocationID
            ", conn);
            cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
            cmd.Parameters.AddWithValue("@LocationID", locationId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                model.FirstName = reader["FirstName"]?.ToString();
                model.LastName = reader["LastName"]?.ToString();
                model.Email = reader["Email"]?.ToString();
                model.FullName = $"{model.FirstName} {model.LastName}";
                model.GoogleEmail = reader["GoogleEmail"]?.ToString();
                model.GoogleAccessToken = reader["GoogleAccessToken"]?.ToString();
                model.GoogleRefreshToken = reader["GoogleRefreshToken"]?.ToString();
                model.SalesmanID = reader["SalesmanID"] != DBNull.Value ? Convert.ToInt32(reader["SalesmanID"]) : 0;
                model.SalesmanNumber = reader["SalesmanNumber"]?.ToString();
                model.LocationId = reader["Location"] != DBNull.Value ? Convert.ToInt32(reader["Location"]) : 0;
            }

            return model;
        }

        private SalesRepMetricsViewModel GetSalesMetrics(int salesmanId, int locationId, DateTime startDate, DateTime endDate)
        {
            var metrics = new SalesRepMetricsViewModel
            {
                PropertyDetails = new List<SalesRepNewAccountSummaryViewModel>()
            };

            // Get salesperson full name for query
            string salesmanName = GetSalespersonFullName(salesmanId, locationId);
            if (string.IsNullOrEmpty(salesmanName))
                return metrics;

            string officeLocation = LocationHelper.GetLocationCode(locationId);
            //string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
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
                var item = new SalesRepNewAccountSummaryViewModel
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
                };
                metrics.PropertyDetails.Add(item);
            }

            // Summary Totals
            metrics.NewAccounts = metrics.PropertyDetails.Count;
            metrics.OrdersCount = metrics.PropertyDetails.Sum(x => x.Orders);
            metrics.InvoicesCount = metrics.PropertyDetails.Sum(x => x.Invoices);
            metrics.TotalSalesAmount = metrics.PropertyDetails.Sum(x => x.TotalSalesAmount);
            metrics.TotalInvoiceAmount = metrics.PropertyDetails.Sum(x => x.TotalInvoiceAmount);
            metrics.WithoutOrders = metrics.PropertyDetails.Count(x => x.Orders == 0);

            // Performance KPI
            metrics.PerformanceKPI = GetPerformanceKPI(salesmanId, locationId, startDate, endDate);

            return metrics;
        }

        private string GetSalespersonFullName(int salesmanId, int locationId)
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

        private SalesRepPerformanceViewModel GetPerformanceKPI(int salesmanId, int locationId, DateTime startDate, DateTime endDate)

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

                // Get Today's Date
                DateTime today = DateTime.Today;
                                
                // 1. Get YTD totals and order count for avg order amount
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
                        var amount = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1));
                        if (amount > 0)
                            goalMonths.Add(amount);

                        //performance.YTDSales = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                        //var totalInvoices = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        //avgOrderAmount = totalInvoices > 0 ? performance.YTDSales / totalInvoices : 0;
                    }
                }

                if (goalMonths.Any())
                    sixMonthGoal = Math.Round(goalMonths.Average() * 1.1m, 2); // Apply 10% boost


                // 2. Get (YTD) Yearly Sales & Avg amount 
                // Add a YTD Start Date
                DateTime YTDStart = new DateTime(DateTime.Today.Year, 1, 1);

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
                ytdCmd.Parameters.AddWithValue("@YTDStart", YTDStart);
                ytdCmd.Parameters.AddWithValue("@EndDate", endDate);

                using (var reader = ytdCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        performance.YTDSales = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                        int invoiceCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        avgOrderAmount = invoiceCount > 0 ? performance.YTDSales / invoiceCount : 0;
                    }
                }


                // 3. Get (MTD) monthly actuals + invoice counts
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

                var monthlyActuals = new List<decimal>();
                using (var reader = monthlyCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var label = reader.GetString(1);
                        var count = reader.GetInt32(2);
                        var actual = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3));

                        var expected = Math.Round(count * avgOrderAmount, 2);
                        monthlyActuals.Add(actual);

                        monthData.Add(new MonthlyPerformanceTrend
                        {
                            MonthLabel = label,
                            Actual = actual,
                            Average = expected, // calculated as: InvoiceCount × avg order $
                            InvoiceCount = count,
                            Goal = sixMonthGoal
                        });
                    }
                }

                // 4. Add to viewmodel
                performance.MonthlyPerformance = monthData
                    .OrderByDescending(m => DateTime.ParseExact(m.MonthLabel, "MMM yy", CultureInfo.InvariantCulture))
                    .ToList();

                // 5. MTD
                var mtdCmd = new SqlCommand(@"
                    SELECT 
                        SUM(IHF_TOTAL_AMOUNT)
                    FROM INVOICE_HEADER
                    WHERE IHF_SMNMAS_ORDER = @SalesmanID
                        AND MONTH(IHF_INVOICE_DATE) = MONTH(GETDATE())
                        AND YEAR(IHF_INVOICE_DATE) = YEAR(GETDATE())
                        AND IHF_CANCELED_DATE IS NULL;
                ", conn);
                mtdCmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                performance.MTDSales = Convert.ToDecimal(mtdCmd.ExecuteScalar() ?? 0);

                performance.TargetSalesGoal = sixMonthGoal;
                performance.MonthlyPerformance = monthData
                    .OrderByDescending(m => DateTime.ParseExact(m.MonthLabel, "MMM yy", CultureInfo.InvariantCulture))
                    .ToList();
            }

            // 6. Task completion from SalesMetrics
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
                    performance.TaskCount = reader.GetInt32(0);
                    performance.CompletedTasks = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                }
            }

            // 7. Inactive customers
            using (var conn = new SqlConnection(_config.GetConnectionString(officeLocation)))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT IHF_CUSTOMER_NUMBER)
                    FROM INVOICE_HEADER
                    WHERE IHF_SMNMAS_ORDER = @SalesmanID
                      AND IHF_ORDER_DATE < DATEADD(DAY, -60, GETDATE());
                ", conn);
                cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                performance.InactiveAccounts = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            }

            // 8. Performance flag
            performance.PerformanceRating = performance.MTDSales >= performance.TargetSalesGoal
                ? "On Track" 
                : "Needs Improvement";

            return performance;
        }


        // End Methods Controller
    }
}
