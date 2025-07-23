using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Security.Claims;
using SalesMetrics.Models;
using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Authorization;
using SalesMetrics.Models.EFCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using SalesMetrics.Services.Helpers;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SalesMetrics.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;

        public DashboardController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        //private bool IsSalesPerson(int roleId) => roleId == 2;

        [Authorize]
        public IActionResult Index(DateTime? startDate, DateTime? endDate, int weekOffset = 0, int? rangeType = 0, int? filterSalesmanId = null, int whsId = 0)
        {
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);

            var users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            var userId = HttpContext.Session.GetString("UserId");
            var fullName = HttpContext.Session.GetString("FullName");
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            int salesmanId = Convert.ToInt32(HttpContext.Session.GetInt32("SalesmanId"));

            if (string.IsNullOrEmpty(userId) || users_Id == 0)
            {
                // fallback to Claims if session expired but cookie still exists
                userId = User.FindFirstValue("UserId");
                users_Id = Convert.ToInt32(HttpContext.Session.GetString("Users_ID"));
                if (string.IsNullOrEmpty(userId))
                    return RedirectToAction("Login", "Auth");

                fullName = User.FindFirstValue("FullName");
            }

            if (!startDate.HasValue || !endDate.HasValue)
            {
                var today = DateTime.Today;
                startDate = new DateTime(today.Year, today.Month, 1); // First day of current month
                endDate = today; // Today
            }

            // 👇 Only use a salesmanId if a specific salesperson was selected
            // Determine effective SalesmanId
            int effectiveSalesmanId = 0;

            // For Salesperson (Role 2), use their own SalesmanId from session
            if (roleId == 2)
            {
                effectiveSalesmanId = salesmanId;  // This is the SalesmanId from session
            }
            // For Admin/SalesAdmin (Role 1 or 3), use the filterSalesmanId if provided
            else if (roleId == 1 || roleId == 3 || roleId == 4)
            {
                effectiveSalesmanId = filterSalesmanId ?? 0; // Fallback to 0 if no filter is selected
            }

            // When using the Selected SalesmanId, we need to get the UserId for that Salesman
            int selectedUserId;

            if (effectiveSalesmanId > 0)
            {
                selectedUserId = GetUserIdBySalesmanId(effectiveSalesmanId, locationId);
            }
            else
            {
                selectedUserId = users_Id; // Fallback for new users with no SalesmanID
            }

            // Fetch Sales Data from the CUF ERP Database
            var salesData = GetSalesData(connectionString, effectiveSalesmanId, officeLocation, startDate, endDate, roleId, whsId);
            var (weeklyOrders, startOfWeek, endOfWeek) = GetWeeklyOrdersDataWithRange(weekOffset, roleId, effectiveSalesmanId, whsId);
            var transactionSummary = GetTransactionSummary(connectionString, startDate, endDate, roleId, effectiveSalesmanId, whsId);
            var overdueInvoices = GetOverdueInvoices(connectionString, roleId, effectiveSalesmanId, whsId);
            var todayTasks = GetTodayTasks(selectedUserId, locationId);
            var inactiveCustomers = GetInactiveCustomers(connectionString, roleId, effectiveSalesmanId, whsId);

            ViewBag.RoleId = roleId;
            ViewBag.UserId = userId;
            ViewBag.Users_Id = users_Id;

            if (roleId == 1 || roleId == 3 || roleId == 4)
            {
                ViewBag.Users = GetActiveUsers(Convert.ToInt32(users_Id), roleId, Convert.ToInt32(locationId));
            }
            else if (roleId == 2)
            {
                ViewBag.Users = new List<User>();
                ViewBag.SalespersonId = salesmanId;
            }

            ViewBag.SelectedWhsId = whsId;
            ViewBag.WhsList = GetWarehouseID(connectionString);

            ViewBag.TodayTasks = todayTasks;
            ViewBag.Username = fullName.Split(' ')[0];
            ViewBag.Greeting = GetGreeting();
            ViewBag.SalesData = salesData;
            ViewBag.WeeklyOrders = weeklyOrders ?? new List<DailyOrderCount>(); // Avoid null issues
            ViewBag.WeeklyStart = startOfWeek;
            ViewBag.WeeklyEnd = endOfWeek;
            ViewBag.WeekOffset = weekOffset;
            ViewBag.TransactionSummary = transactionSummary;
            ViewBag.OverdueInvoices = overdueInvoices;
            ViewBag.InactiveCustomers = inactiveCustomers;

            ViewBag.StartDate = startDate?.ToString("MM/dd/yyyy");
            ViewBag.EndDate = endDate?.ToString("MM/dd/yyyy");

            var (mtdRanking, ytdRanking, mtdStart, mtdEnd, ytdStart, ytdEnd) = GetSalesRanking(whsId);
            ViewBag.MTDRanking = mtdRanking;
            ViewBag.YTDRanking = ytdRanking;
            ViewBag.MTDRange = $"{mtdStart:MMM d} - {mtdEnd:MMM d} {mtdStart.Year}";
            ViewBag.YTDRange = $"{ytdStart:MMM d} - {ytdEnd:MMM d} {ytdStart.Year}";

            ViewBag.SelectedRangeType = rangeType ?? 0; // Save selected option in ViewBag
            ViewBag.SelectedSalesmanId = filterSalesmanId;

            ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("Dashboard");

            return View();
        }

        private List<Warehouses> GetWarehouseID(string connectionString)
        {
            var whsList = new List<Warehouses>();

            using (var conn = new SqlConnection(connectionString))
            {
                var sql = @"
                    SELECT WHS_WAREHOUSE_NUMBER as [WhsId],
	                    WHS_WAREHOUSE_NAME as [WhsName]
                    FROM WAREHOUSE_MASTER
                    WHERE WHS_WAREHOUSE_NUMBER < 10
                ";

                var cmd = new SqlCommand(sql, conn);

                conn.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    whsList.Add(new Warehouses
                    {
                        WhsID = reader.GetInt32(0),
                        WhsName = reader.GetString(1)
                    });
                }
            }

            return whsList;
        }

        private string GetGreeting()
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                < 12 => "Good Morning",
                < 18 => "Good Afternoon",
                _ => "Good Evening"
            };
        }

        private List<User> GetActiveUsers(int users_Id, int roleId, int locationId)
        {
            var users = new List<User>();
            var connectionString = _configuration.GetConnectionString("SalesMetrics");

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = (roleId == 1 || roleId == 3 || roleId == 4)
                    ? @"SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE SalesmanID IS NOT NULL AND Location = @Location"
                    : @"SELECT Users_ID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Users_ID = @Users_Id";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (roleId == 1 || roleId == 3 || roleId == 4)
                        cmd.Parameters.AddWithValue("@Location", locationId);
                    else
                        cmd.Parameters.AddWithValue("@Users_Id", users_Id);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                Users_ID = reader.GetInt32("Users_ID"),
                                UserID = reader.GetInt32("UserID"),
                                FirstName = reader.GetString("FirstName"),
                                LastName = reader.GetString("LastName"),
                                RoleID = reader.GetInt32("RoleID"),
                                Location = reader.GetInt32("Location"),
                                CreatedDate = reader.GetDateTime("CreatedDate"),
                                SalesmanID = reader.IsDBNull("SalesmanID") ? 0 : reader.GetInt32("SalesmanID")
                            });
                        }
                    }
                }
            }

            return users;
        }

        private int GetUserIdBySalesmanId(int salesmanId, int locationId)
        {
            // If no SalesmanID, just return the current user
            if (salesmanId == 0)
            {
                return Convert.ToInt32(HttpContext.Session.GetString("Users_Id"));
            }

            var connectionString = _configuration.GetConnectionString("SalesMetrics");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Users_ID FROM Users WHERE SalesmanID = @SalesmanID AND Location = @LocationID", conn);
                cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                cmd.Parameters.AddWithValue("@LocationID", locationId);
                var result = cmd.ExecuteScalar();

                // ✅ Fallback to current user if no match found
                return result != null ? Convert.ToInt32(result) : Convert.ToInt32(HttpContext.Session.GetString("Users_Id"));
            }
        }

        private Dictionary<string, object> GetSalesData(string connectionString, int salesmanID, string officeLocation, DateTime? startDate, DateTime? endDate, int roleId = 0, int whsId = 0)
        {
            var data = new Dictionary<string, object>();

            // Get the correct ERP Database connection string based on office location
            //string connectionString = _configuration.GetConnectionString(officeLocation);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string for '{officeLocation}' is not found in appsettings.json.");
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var query = @"
                    SELECT 
                        SUM(AR.ARO_INVOICE_AMOUNT) AS TotalSales,
                        SUM(AR.ARO_INVOICE_BALANCE_DUE) AS AmountDue,
                        COUNT(DISTINCT I.IHF_CUSTOMER_NUMBER) AS PropertyCount,
                        COUNT(DISTINCT I.IHF_SHIP_TO_NAME) AS PropertyNameCount,
                        COUNT(I.IHF_INVOICE_NUMBER) AS TotalInvoices,
                        SUM(CASE WHEN AR.ARO_INVOICE_BALANCE_DUE = 0 THEN 1 ELSE 0 END) AS PaidInvoices,
                        SUM(CASE WHEN AR.ARO_DUE_DATE < GETDATE() AND AR.ARO_INVOICE_BALANCE_DUE > 0 THEN 1 ELSE 0 END) AS OverdueInvoices,
                        AVG(AR.ARO_INVOICE_AMOUNT) AS AvgInvoiceAmount,
                        MAX(AR.ARO_INVOICE_AMOUNT) AS MaxInvoiceAmount,
                        MIN(AR.ARO_INVOICE_AMOUNT) AS MinInvoiceAmount
                    FROM AR_OPEN_ITEM AS AR
                        LEFT JOIN INVOICE_HEADER AS I ON AR.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                        LEFT JOIN SALESMAN_MASTER AS SM ON I.IHF_SMNMAS_ORDER = SM.SMN_SMNMAS_ID
                    WHERE  I.IHF_INVOICE_DATE >= @StartDate
                        AND I.IHF_INVOICE_DATE <= @EndDate
                        AND I.IHF_CANCELED_DATE IS NULL
                ";

                if (whsId > 0)
                {
                    query += $" AND I.IHF_WHSMAS_ID = @WhsId";
                }

                if (salesmanID > 0)
                {
                    query += " AND SM.SMN_SMNMAS_ID = @SalesmanID";
                }

                SqlCommand cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@StartDate", startDate ?? DateTime.Today.AddDays(-30));
                cmd.Parameters.AddWithValue("@EndDate", endDate ?? DateTime.Today);

                if (whsId > 0)
                {
                    cmd.Parameters.AddWithValue("@WhsId", whsId);
                }

                if (salesmanID > 0)
                {
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanID);
                }

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        data["TotalSales"] = reader["TotalSales"] != DBNull.Value ? Convert.ToDecimal(reader["TotalSales"]) : 0;
                        data["AmountDue"] = reader["AmountDue"] != DBNull.Value ? Convert.ToDecimal(reader["AmountDue"]) : 0;
                        data["PropertyCount"] = reader["PropertyCount"] != DBNull.Value ? Convert.ToInt32(reader["PropertyCount"]) : 0;
                        data["PropertyNameCount"] = reader["PropertyNameCount"] != DBNull.Value ? Convert.ToInt32(reader["PropertyNameCount"]) : 0;
                        data["TotalInvoices"] = reader["TotalInvoices"] != DBNull.Value ? Convert.ToInt32(reader["TotalInvoices"]) : 0;
                        data["PaidInvoices"] = reader["PaidInvoices"] != DBNull.Value ? Convert.ToInt32(reader["PaidInvoices"]) : 0;
                        data["OverdueInvoices"] = reader["OverdueInvoices"] != DBNull.Value ? Convert.ToInt32(reader["OverdueInvoices"]) : 0;
                        data["AvgInvoiceAmount"] = reader["AvgInvoiceAmount"] != DBNull.Value ? Convert.ToDecimal(reader["AvgInvoiceAmount"]) : 0;
                        data["MaxInvoiceAmount"] = reader["MaxInvoiceAmount"] != DBNull.Value ? Convert.ToDecimal(reader["MaxInvoiceAmount"]) : 0;
                        data["MinInvoiceAmount"] = reader["MinInvoiceAmount"] != DBNull.Value ? Convert.ToDecimal(reader["MinInvoiceAmount"]) : 0;
                    }
                }
            }

            return data;
        }

        [HttpGet]
        public IActionResult GetKpiTiles(DateTime startDate, DateTime endDate, int? filterSalesmanId = null, int whsId = 0)
        {
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            int effectiveSalesmanId = 0;

            if (roleId == 2)
                effectiveSalesmanId = Convert.ToInt32(HttpContext.Session.GetInt32("SalesmanId"));
            else if (roleId == 1 || roleId == 3 || roleId == 4)
                effectiveSalesmanId = filterSalesmanId ?? 0;

            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);

            var salesData = GetSalesData(connectionString, effectiveSalesmanId, officeLocation, startDate, endDate, roleId, whsId);
            ViewBag.SalesData = salesData;

            return PartialView("_KpiTilesPartial");
        }

        // GET WEEKLY ORDERS LOGIC
        //private (List<DailyOrderCount> Orders, DateTime Start, DateTime End) GetWeeklyOrdersDataWithRange(int weekOffset = 0, int roleId = 0, int salesmanId = 0)
        //{
        //    var orders = new List<DailyOrderCount>();
        //    string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
        //    var connectionString = _configuration.GetConnectionString(officeLocation);

        //    var userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue("UserId");
        //    var users_Id = HttpContext.Session.GetString("Users_Id") ?? User.FindFirstValue("Users_Id");
        //    if (string.IsNullOrEmpty(users_Id)) return (new List<DailyOrderCount>(), DateTime.Today, DateTime.Today);

        //    int daysToSunday = (int)DateTime.Today.DayOfWeek;
        //    DateTime sunday = DateTime.Today.AddDays(-daysToSunday).Date.AddDays(weekOffset * 7);
        //    DateTime saturday = sunday.AddDays(6);

        //    using (var conn = new SqlConnection(connectionString))
        //    {
        //        var query = @"
        //            SELECT 
        //                DATENAME(WEEKDAY, SH.SOH_DELIVERY_DATE) AS WeekdayName,
        //                COUNT(DISTINCT SH.SOH_NUMBER) AS OrdersCount
        //            FROM SALES_HEADER AS SH
        //            WHERE 
        //                SH.SOH_DELIVERY_DATE >= @StartDate
        //                AND SH.SOH_DELIVERY_DATE <= @EndDate
        //        ";

        //        if (salesmanId > 0)
        //        {
        //            query += " AND SH.SOH_SMNMAS_ID = @SalesmanID";
        //        }

        //        query += @"
        //            GROUP BY 
        //                DATENAME(WEEKDAY, SH.SOH_DELIVERY_DATE),
        //                DATEPART(WEEKDAY, SH.SOH_DELIVERY_DATE)
        //            ORDER BY DATEPART(WEEKDAY, SH.SOH_DELIVERY_DATE);
        //        ";

        //        var cmd = new SqlCommand(query, conn);

        //        if (salesmanId > 0)
        //        {
        //            cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
        //        }

        //        cmd.Parameters.AddWithValue("@StartDate", sunday);
        //        cmd.Parameters.AddWithValue("@EndDate", saturday);


        //        conn.Open();
        //        using (var reader = cmd.ExecuteReader())
        //        {
        //            while (reader.Read())
        //            {
        //                orders.Add(new DailyOrderCount
        //                {
        //                    WeekdayName = reader.GetString(0),
        //                    OrdersCount = reader.GetInt32(1)
        //                });
        //            }
        //        }
        //    }

        //    return (orders, sunday, saturday);
        //}
        private (List<DailyOrderCount> Orders, DateTime Start, DateTime End) GetWeeklyOrdersDataWithRange(int weekOffset = 0, int roleId = 0, int salesmanId = 0, int whsId = 0)
        {
            var orders = new List<DailyOrderCount>();
            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);

            int daysToSunday = (int)DateTime.Today.DayOfWeek;
            DateTime sunday = DateTime.Today.AddDays(-daysToSunday).Date.AddDays(weekOffset * 7);
            DateTime saturday = sunday.AddDays(6);

            using (var conn = new SqlConnection(connectionString))
            {
                var query = @"
                SELECT 
                    DATENAME(WEEKDAY, SH.SOH_DELIVERY_DATE) AS WeekdayName,
                    COUNT(DISTINCT SH.SOH_NUMBER) AS OrdersCount,
                    SUM(CASE
                        WHEN SH.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NOT NULL THEN AR.ARO_INVOICE_AMOUNT
                        WHEN SH.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NULL THEN 0
                        ELSE SH.SOH_TOTAL_AMOUNT
                    END) AS TotalOrderAmount
                FROM SALES_HEADER AS SH
                LEFT JOIN AR_OPEN_ITEM AR ON SH.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                WHERE 
                    SH.SOH_DELIVERY_DATE BETWEEN @StartDate AND @EndDate
                    AND SH.SOH_CURRENT_STATUS <> 4
                    AND SH.SOH_CANCELED_DATE IS NULL";

                if (whsId > 0)
                {
                    query += $" AND SH.SOH_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    query += " AND SH.SOH_SMNMAS_ID = @SalesmanID";
                }

                query += @"
                    GROUP BY 
                        DATENAME(WEEKDAY, SH.SOH_DELIVERY_DATE),
                        DATEPART(WEEKDAY, SH.SOH_DELIVERY_DATE)
                    ORDER BY DATEPART(WEEKDAY, SH.SOH_DELIVERY_DATE);";

                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@StartDate", sunday);
                cmd.Parameters.AddWithValue("@EndDate", saturday);

                if (whsId > 0)
                    cmd.Parameters.AddWithValue("@WhsId", whsId);
                if (salesmanId > 0)
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        orders.Add(new DailyOrderCount
                        {
                            WeekdayName = reader.GetString(0),
                            OrdersCount = reader.GetInt32(1),
                            TotalOrderAmount = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetDouble(2))
                        });
                    }
                }
            }

            return (orders, sunday, saturday);
        }

        [HttpGet]
        public JsonResult GetWeeklyOrders(int weekOffset = 0, int? filterSalesmanId = null, int whsId = 0)
        {
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            int effectiveSalesmanId = 0;

            if (roleId == 2)
            {
                effectiveSalesmanId = Convert.ToInt32(HttpContext.Session.GetInt32("SalesmanId"));
            }
            else if (roleId == 1 || roleId == 3 || roleId == 4)
            {
                effectiveSalesmanId = filterSalesmanId ?? 0;
            }

            var (orders, _, _) = GetWeeklyOrdersDataWithRange(weekOffset, roleId, effectiveSalesmanId, whsId);
            return Json(orders);
        }


        private TransactionSummary GetTransactionSummary(string connectionString, DateTime? startDate, DateTime? endDate, int roleId = 0, int salesmanId = 0, int whsId = 0)
        {
            var summary = new TransactionSummary { TopDelinquentCustomers = new List<CustomerOutstanding>() };

            using (var conn = new SqlConnection(connectionString))
            {
                // Query A and B
                var query = @"
                    -- A. Pending Invoices Count
                    SELECT 
                        COUNT(*) AS PendingInvoices, 
                        SUM(A.ARO_INVOICE_BALANCE_DUE) AS PendingInvoicesAmount

                    FROM AR_OPEN_ITEM AS A
	                    LEFT JOIN INVOICE_HEADER as I on A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    WHERE ARO_INVOICE_BALANCE_DUE > 0 
	                    AND ARO_DATE_PAID_IN_FULL IS NULL";
                if (whsId > 0)
                {
                    query += $" AND ARO_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    query += " AND I.IHF_SMNMAS_ORDER = @SalesmanID";
                }

                query += @"
                    -- B. Aging Buckets 30-60
                    SELECT 
                        COUNT(*) AS Due30to60,
                        SUM(A.ARO_INVOICE_BALANCE_DUE) AS Due30to60Amount
                    FROM AR_OPEN_ITEM AS A
                        LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    WHERE 
                        A.ARO_INVOICE_BALANCE_DUE > 0 
                        AND A.ARO_DATE_PAID_IN_FULL IS NULL 
                        AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59";

                if (whsId > 0)
                {
                    query += $" AND ARO_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    query += " AND I.IHF_SMNMAS_ORDER = @SalesmanID";
                }

                query += @"
                    -- C. Aging Buckets 60-90
                    SELECT 
                        COUNT(*) AS Due60to90,
                        SUM(A.ARO_INVOICE_BALANCE_DUE) AS Due60to90Amount
                    FROM AR_OPEN_ITEM AS A
                        LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    WHERE 
                        A.ARO_INVOICE_BALANCE_DUE > 0 
                        AND A.ARO_DATE_PAID_IN_FULL IS NULL 
                        AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89";

                if (whsId > 0)
                {
                    query += $" AND ARO_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    query += " AND I.IHF_SMNMAS_ORDER = @SalesmanID";
                }

                query += @"
                    -- D. Aging Buckets 90-120
                    SELECT 
                        COUNT(*) AS Due90to120,
                        SUM(A.ARO_INVOICE_BALANCE_DUE) AS Due90to120Amount
                    FROM AR_OPEN_ITEM AS A
                        LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    WHERE 
                        A.ARO_INVOICE_BALANCE_DUE > 0 
                        AND A.ARO_DATE_PAID_IN_FULL IS NULL 
                        AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 120";

                if (whsId > 0)
                {
                    query += $" AND ARO_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    query += " AND I.IHF_SMNMAS_ORDER = @SalesmanID";
                }

                query += @"
                    -- E. Aging Buckets Over 120
                    SELECT 
                        COUNT(*) AS DueOver120,
                        SUM(A.ARO_INVOICE_BALANCE_DUE) AS DueOver120Amount
                    FROM AR_OPEN_ITEM AS A
                        LEFT JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    WHERE 
                        A.ARO_INVOICE_BALANCE_DUE > 0 
                        AND A.ARO_DATE_PAID_IN_FULL IS NULL 
                        AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) > 120";

                if (whsId > 0)
                {
                    query += $" AND ARO_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    query += " AND I.IHF_SMNMAS_ORDER = @SalesmanID";
                }

                using (var cmd = new SqlCommand(query, conn))
                {
                    if (whsId > 0)
                        cmd.Parameters.AddWithValue("@WhsId", whsId);

                    if (salesmanId > 0)
                    {
                        cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                    }

                    conn.Open();
                    using var reader = cmd.ExecuteReader();
                    // Read 1st result (PendingInvoices)
                    if (reader.Read())
                    summary.PendingInvoices = reader["PendingInvoices"] != DBNull.Value ? Convert.ToInt32(reader["PendingInvoices"]) : 0;
                    summary.PendingInvoicesAmount = reader["PendingInvoicesAmount"] != DBNull.Value ? Convert.ToDouble(reader["PendingInvoicesAmount"]) : 0;

                    // Read 2nd result (Due30to60)
                    if (reader.NextResult() && reader.Read())
                    {
                        summary.Due30to60 = reader["Due30to60"] != DBNull.Value ? Convert.ToInt32(reader["Due30to60"]) : 0;
                        summary.Due30to60Amount = reader["Due30to60Amount"] != DBNull.Value ? Convert.ToDouble(reader["Due30to60Amount"]) : 0;
                    }

                    // Read 3rd result (Due60to90)
                    if (reader.NextResult() && reader.Read())
                    {
                        summary.Due60to90 = reader["Due60to90"] != DBNull.Value ? Convert.ToInt32(reader["Due60to90"]) : 0;
                        summary.Due60to90Amount = reader["Due60to90Amount"] != DBNull.Value ? Convert.ToDouble(reader["Due60to90Amount"]) : 0;
                    }

                    // Read 4th result (Due90to120)
                    if (reader.NextResult() && reader.Read())
                    {
                        summary.Due90to120 = reader["Due90to120"] != DBNull.Value ? Convert.ToInt32(reader["Due90to120"]) : 0;
                        summary.Due90to120Amount = reader["Due90to120Amount"] != DBNull.Value ? Convert.ToDouble(reader["Due90to120Amount"]) : 0;
                    }

                    // Read 5th result (DueOver120)
                    if (reader.NextResult() && reader.Read())
                    {
                        summary.DueOver120 = reader["DueOver120"] != DBNull.Value ? Convert.ToInt32(reader["DueOver120"]) : 0;
                        summary.DueOver120Amount = reader["DueOver120Amount"] != DBNull.Value ? Convert.ToDouble(reader["DueOver120Amount"]) : 0;
                    }
                }

                // Query C - Top 5 Delinquents
                var queryDel = @"
                    SELECT TOP 5 
                        I.IHF_BILLTO_NAME AS CustomerName,
                        I.IHF_CUSTOMER_NUMBER as CustomerNumber,
	                    I.IHF_CUMMAS_ID as CustomerId,
                        CAST(SUM(A.ARO_INVOICE_BALANCE_DUE) AS DECIMAL(18, 2)) AS BalanceDue,
	                    CAST(SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) > 60 THEN A.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DECIMAL(18, 2)) AS OutstandingAmount
                    FROM AR_OPEN_ITEM as A
	                    JOIN INVOICE_HEADER as I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    WHERE A.ARO_INVOICE_BALANCE_DUE > 0
	                    AND A.ARO_DATE_PAID_IN_FULL IS NULL";

                if (whsId > 0)
                {
                    query += $" AND ARO_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    queryDel += " AND I.IHF_SMNMAS_ORDER = @SalesmanID";
                }

                queryDel += @"
                    GROUP BY I.IHF_CUMMAS_ID, I.IHF_CUSTOMER_NUMBER, I.IHF_BILLTO_NAME
                    ORDER By OutstandingAmount DESC;";

                using (var cmd = new SqlCommand(queryDel, conn))
                {
                    if (whsId > 0)
                        cmd.Parameters.AddWithValue("@WhsId", whsId);

                    if (salesmanId > 0)
                        cmd.Parameters.AddWithValue("SalesmanID", salesmanId);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        summary.TopDelinquentCustomers.Add(new CustomerOutstanding
                        {
                            CustomerName = reader.GetString(0),
                            CustomerNumber = Convert.ToInt32(reader.GetString(1)),
                            CustomerId = reader.GetInt32(2),
                            BalanceDue = reader.GetDecimal(3),
                            OutstandingAmount = reader.GetDecimal(4)
                        });
                    }
                }
            }
            return summary;
        }

        private List<OverdueInvoice> GetOverdueInvoices(string connectionString, int roleId = 0, int salesmanId = 0, int whsId = 0)
        {
            var overdueList = new List<OverdueInvoice>();
            var userId = Convert.ToInt32(User.FindFirstValue("UserId"));

            using (var conn = new SqlConnection(connectionString))
            {
                var sql = @"
                    SELECT 
	                    I.IHF_INVOICE_NUMBER AS Invoice,
                        C.CUM_CUMMAS_ID AS CustomerId,
                        I.IHF_BILLTO_NAME AS CustomerName,
	                    P.IPC_DESCRIPTION as MgmtCo,
	                    CAST(A.ARO_INVOICE_BALANCE_DUE AS DECIMAL(18, 2)) AS OutstandingAmount,
	                    CAST(A.ARO_DUE_DATE as DATE) as DueDate,
	                    CASE 
		                    WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) < 30 THEN 'Under 30 Days'
		                    WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59 THEN '30 to 60 Days'
		                    WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89 THEN '60 to 90 Days'
                            WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 119 THEN '90 to 120 Days'
		                    WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) >= 120 THEN 'Over 120 Days'
		                    ELSE '' END as Status,
	                    DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) as Days,
                        SM.SMN_SMNMAS_ID as SalespersonId,
                        SM.SMN_SALESMAN_NAME as Salesperson
                    FROM AR_OPEN_ITEM A
	                    LEFT JOIN INVOICE_HEADER I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                        LEFT JOIN CUSTOMER_MASTER C on A.ARO_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
	                    LEFT JOIN SALESMAN_MASTER AS SM ON I.IHF_SMNMAS_ORDER = SM.SMN_SMNMAS_ID
	                    LEFT JOIN PRICE_CODES as P on I.IHF_PRICE_CODE = P.IPC_PRICE_CODE

                    WHERE A.ARO_INVOICE_BALANCE_DUE > 0
	                    AND DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) > 30
	                    AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    ";

                if (connectionString == "LAX")
                {
                    sql += " AND I.IHF_SMNMAS_ORDER<> 24";
                }

                if (whsId > 0)
                {
                    sql += $" AND ARO_WHSMAS_ID = @WhsId";
                }

                if (salesmanId > 0)
                {
                    sql += " AND I.IHF_SMNMAS_ORDER = @SalesmanID";
                }

                sql += " ORDER BY OutstandingAmount DESC";

                var cmd = new SqlCommand(sql, conn);

                if (whsId > 0)
                    cmd.Parameters.AddWithValue("@WhsId", whsId);
                
                if (salesmanId > 0)
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

                conn.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    overdueList.Add(new OverdueInvoice
                    {
                        Invoice = reader.GetInt32(0),
                        CustomerId = reader.GetInt32(1),
                        CustomerName = reader.GetString(2),
                        MgmtCo = reader.GetString(3),
                        OutstandingAmount = reader.GetDecimal(4),
                        DueDate = reader.GetDateTime(5),
                        InvoiceAging = reader.GetString(6),
                        DaysPastDue = reader.GetInt32(7),
                        SalespersonId = reader.GetInt32(8),
                        Salesperson = reader.GetString(9)
                    });
                }
            }

            return overdueList;
        }

        [HttpGet]
        public IActionResult GetOrderDetails(int invoiceNumber)
        {
            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);
            var viewModel = new InvoiceDetailViewModel();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    -- ORDER LINE ITEMS
                    SELECT 
                        IDT_ORDER_QTY_ORDER,
                        IDT_ORDER_UOM,
                        IDT_PRODUCT_CLASS_CODE,
                        IDT_DESCRIPTION + ' ' + IDT_DESCRIPTION AS Description,
                        IDT_EXTENDED_PRICE
                    FROM INVOICE_DETAIL
                    WHERE IDT_LINE_TYPE = 1 AND IDT_INVOICE_NUMBER = @InvoiceNumber;

                    -- INVOICE HEADER
                    SELECT 
                        I.IHF_BILLTO_NAME AS CustomerName,
                        CAST(I.IHF_CUSTOMER_NUMBER as int) AS [IHF_CUSTOMER_NUMBER],
                        I.IHF_INVOICE_NUMBER,
                        A.ARO_SALES_ORDER_NUMBER,
                        I.IHF_INVOICE_DATE,
                        I.IHF_DELIVERY_DATE,
                        A.ARO_INVOICE_BALANCE_DUE,
                        CASE 
                            WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) < 30 THEN 'Under 30 Days'
                            WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59 THEN '30 to 60 Days'
                            WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89 THEN '60 to 90 Days'
                            WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 119 THEN '90 to 120 Days'
                            WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) >= 120 THEN 'Over 120 Days'
                            ELSE '' END as InvoiceAging,
                        I.IHF_CUSTOMER_PO 
                    FROM AR_OPEN_ITEM A
                        LEFT JOIN INVOICE_HEADER I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                    WHERE I.IHF_INVOICE_NUMBER = @InvoiceNumber;

                    -- INVOICE NOTES
                    SELECT
		                IDT_LINE_NUMBER,
		                IDT_COMMENTS
	                FROM INVOICE_DETAIL
	                WHERE IDT_LINE_TYPE <> 1 AND IDT_ORDER_QTY_ORDER = 0
		                AND IDT_INVOICE_NUMBER = @InvoiceNumber;
                ", conn);

                cmd.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);

                using (var reader = cmd.ExecuteReader())
                {
                    // Line Items
                    while (reader.Read())
                    {
                        viewModel.LineItems.Add(new OrderLineItem
                        {
                            Quantity = Convert.ToDecimal(reader[0]),
                            UOM = reader.GetString(1),
                            ProdClass = reader.GetString(2),
                            Description = reader.GetString(3),
                            ProductTotal = Convert.ToDecimal(reader[4])
                        });
                    }
                    // Move to 2nd result set (header)
                    if (reader.NextResult() && reader.Read())
                    {
                        viewModel.Header = new InvoiceHeader
                        {
                            CustomerName = reader.GetString(0),
                            CustomerNumber = reader.GetInt32(1),
                            InvoiceNumber = reader.GetInt32(2),
                            OrderNumber = reader.GetInt32(3),
                            InvoiceDate = reader.GetDateTime(4),
                            DeliveryDate = reader.GetDateTime(5),
                            OutstandingAmount = Convert.ToDecimal(reader[6]),
                            InvoiceAging = reader.GetString(7),
                            CustomerPO = reader.GetString(8),
                        };
                    }
                    // Move to 3rd result set (Notes)
                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            viewModel.Notes.Add(new OrderNotes
                            {
                                LineNumber = reader.GetInt32(0),
                                Comment = reader.GetString(1)
                            });
                        }
                    }

                }
            }

            return PartialView("_OrderDetailsPartial", viewModel);
        }

        public IActionResult GetWorkOrdersByDay(string dayOfWeek, int weekOffset = 0, int? filterSalesmanId = null, int whsId = 0)
        {
            var officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);
            var results = new List<WorkOrderViewModel>();

            int salesmanId = 0;
            
            if (filterSalesmanId.HasValue && filterSalesmanId.Value != 0)
            {
                salesmanId = filterSalesmanId.Value;
            }
            else if (!int.TryParse(User.FindFirst("SalesmanId")?.Value, out salesmanId))
            {
                salesmanId = 0; // fallback default
            }

            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");

            // Week starts on Sunday
            int daysToSunday = (int)DateTime.Today.DayOfWeek;
            DateTime startOfWeek = DateTime.Today.AddDays(-daysToSunday).Date.AddDays(weekOffset * 7);

            var daysMap = new Dictionary<string, int>
            {
                { "Sunday", 0 },
                { "Monday", 1 },
                { "Tuesday", 2 },
                { "Wednesday", 3 },
                { "Thursday", 4 },
                { "Friday", 5 },
                { "Saturday", 6 }
            };

            if (!daysMap.ContainsKey(dayOfWeek))
                return Json(results);

            DateTime fromDate = startOfWeek.AddDays(daysMap[dayOfWeek]);

            using (var conn = new SqlConnection(connectionString))
            {
                var sqlQuery = @"
                    SELECT   
	                    SOH_NUMBER AS OrderID,
                        CUS.CUM_CUMMAS_ID AS PropertyID,
                        CUS.CUM_CUSTOMER_NUMBER AS PropertyNumber,
                        CUS.CUM_CUSTOMER_NAME AS PropertyName,
                        CUS.CUM_CITY AS City,
                        SOH_SHIP_VIA AS OrderType,

                        -- Quantity
                        ISNULL(
                            (SELECT SUM(SDT_ORDER_QTY_ORDER)
                             FROM dbo.SALES_DETAIL
                                LEFT JOIN dbo.ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                             WHERE SDT_SALOHD_ID = SOH_NUMBER 
                               AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                               AND CASE WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                        ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 END NOT LIKE '%Metal%'
                            ), 0.00) AS Qty,

                        CASE 
                            WHEN S.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NOT NULL THEN AR.ARO_INVOICE_AMOUNT
                            WHEN S.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NULL THEN 0
                            ELSE S.SOH_TOTAL_AMOUNT
                            END as OrderTotal,

                        ISNULL((
		                    SELECT 
			                    STUFF((
				                    SELECT ' | ' + CAST(SDT_COMMENTS AS VARCHAR(MAX))
				                    FROM SALES_DETAIL
				                    WHERE SDT_LINE_TYPE <> 1 
				                      AND SDT_ORDER_QTY_ORDER = 0 
				                      AND SDT_SALOHD_ID = S.SOH_NUMBER
				                      AND ISNULL(CAST(SDT_COMMENTS AS VARCHAR(MAX)), '') <> ''
				                    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)')
			                    , 1, 3, '')  -- remove the first ' | '
		                    ), '') AS Notes,

                        B.BuildingNumber + ' - ' + A.ApartmentNumber AS UnitNumber, 
                        APT.Description AS UnitType,
                        CONVERT(VARCHAR, S.SOH_DELIVERY_DATE, 101) AS DeliveryDate,
                        CONVERT(VARCHAR, S.SOH_MOVING_DATE, 101) AS MoveInDate,

                        -- Product Class
                        ISNULL(
                            (SELECT TOP 1 
                                ITM_PCLMAS_ID
                             FROM dbo.SALES_DETAIL
                                LEFT JOIN dbo.ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                             WHERE SDT_SALOHD_ID = SOH_NUMBER 
                               AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                               AND CASE WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                        ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 END NOT LIKE '%Metal%'
                            ), '') AS ProductClass,

                        -- Product Descriptions based on Product Class
                        ISNULL(
                            (SELECT TOP 1 
                                CASE 
                                    WHEN ITM_PCLMAS_ID = 'CARPET' THEN SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2
                                    WHEN ITM_PCLMAS_ID = 'VINYL' THEN SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2
                                    WHEN ITM_PCLMAS_ID = 'TILE' THEN SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2
                                    WHEN ITM_PCLMAS_ID = 'WOOD' THEN SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2
                                    WHEN ITM_PCLMAS_ID = 'VINYLPLANK' THEN SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2
                                    ELSE ''
                                END
                             FROM dbo.SALES_DETAIL
                                LEFT JOIN dbo.ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                             WHERE SDT_SALOHD_ID = SOH_NUMBER 
                               AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                               AND CASE WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                        ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 END NOT LIKE '%Metal%'
                            ), '') AS ProductDescription,

                        S.SOH_ORDERED_BY AS OrderedBy, 
                        P.IPC_DESCRIPTION AS ManagementName, 
                        CASE WHEN SOH_CANCELED_DATE IS NOT NULL THEN 'C' ELSE 'U' END AS Status,
                        [WHS_WAREHOUSE_NUMBER] AS Location,
						SM.SMN_SMNMAS_ID as SalesmanID,
						SM.SMN_SALESMAN_NAME as SalesmanName

                    FROM dbo.SALES_HEADER AS S
                        LEFT JOIN dbo.Apartments AS A ON S.SOH_APARTMENT_ID = A.Id 
                        LEFT JOIN dbo.Buildings AS B ON A.Building_id = B.Id
                        LEFT JOIN dbo.ApartmentType AS APT ON A.ApartmentType_Id = APT.ID
                        LEFT JOIN dbo.CUSTOMER_MASTER AS CUS ON CUS.CUM_CUMMAS_ID = S.SOH_CUMMAS_ID
                        LEFT JOIN dbo.PRICE_CODES as P on CUS.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                        LEFT JOIN [dbo].[WAREHOUSE_MASTER] ON [WHS_WAREHOUSE_NUMBER] = [SOH_WHSMAS_ID]
                        LEFT JOIN [dbo].[SALESMAN_MASTER] as SM on S.SOH_SMNMAS_ID = SM.SMN_SMNMAS_ID
                        LEFT JOIN dbo.AR_OPEN_ITEM as AR on S.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER

                    WHERE S.SOH_DELIVERY_DATE = @FromDate 
                      AND SOH_CURRENT_STATUS <> 4
                      AND SOH_CANCELED_DATE IS NULL
                      AND EXISTS (
                          SELECT 1 
                          FROM dbo.SALES_DETAIL 
                            LEFT JOIN dbo.ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                          WHERE SDT_SALOHD_ID = SOH_NUMBER 
                      )";

                if (whsId > 0)
                {
                    sqlQuery += $" AND S.SOH_WHSMAS_ID = @whsId";
                }

                if (salesmanId > 0)
                {
                    sqlQuery += " AND SOH_SMNMAS_ID = @SalesmanID";
                }

                sqlQuery += @"
                    ORDER BY OrderID";

                var cmd = new SqlCommand(sqlQuery, conn);
                cmd.Parameters.AddWithValue("@FromDate", fromDate);
                if (whsId > 0)
                {
                    cmd.Parameters.AddWithValue("@whsId", whsId);
                }

                if (salesmanId > 0)
                {
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                }

                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new WorkOrderViewModel
                    {
                        OrderID = reader["OrderID"]?.ToString(),
                        PropertyId = Convert.ToInt32(reader["PropertyID"]),
                        PropertyNumber = Convert.ToInt32(reader["PropertyNumber"]),
                        PropertyName = reader["PropertyName"]?.ToString(),
                        City = reader["City"]?.ToString(),
                        OrderType = reader["OrderType"]?.ToString(),
                        Qty = Convert.ToDouble(reader["Qty"]),
                        OrderTotal = Math.Round(Convert.ToDecimal(reader["OrderTotal"]), 2),
                        UnitNumber = reader["UnitNumber"]?.ToString(),
                        UnitType = reader["UnitType"]?.ToString(),
                        DeliveryDate = reader["DeliveryDate"]?.ToString(),
                        ProductClass = reader["ProductClass"]?.ToString(),
                        ProductDescription = reader["ProductDescription"]?.ToString(),
                        OrderedBy = reader["OrderedBy"]?.ToString(),
                        ManagementName = reader["ManagementName"]?.ToString(),
                        Status = reader["Status"]?.ToString(),
                        Location = reader["Location"]?.ToString(),
                        SalesmanId = Convert.ToInt32(reader["SalesmanID"]),
                        SalesmanName = reader["SalesmanName"]?.ToString(),
                    });
                }
            }

            return Json(new
            {
                data = results,
                deliveryDate = fromDate.ToString("MM/dd")
            });
        }

        private List<SalesTask> GetTodayTasks(int users_Id, int locationId)
        {
            var results = new List<SalesTask>();
            var connectionString = _configuration.GetConnectionString("SalesMetrics");

            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
                SELECT TaskID, Title, Description, DueDate, Status, Property, Type, CreatedBy
                FROM Tasks
                WHERE AssignedTo = @Users_Id
                  AND CAST(DueDate AS DATE) >= CAST(GETDATE() AS DATE)
                  AND (CancelledDate IS NULL AND CompletedDate IS NULL)
                  AND Location = @LocationId
                  AND Status <> 'Deleted'
                ORDER BY DueDate", conn);

                cmd.Parameters.AddWithValue("@Users_Id", users_Id);
                cmd.Parameters.AddWithValue("@LocationId", locationId);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new SalesTask
                        {
                            TaskID = reader.GetInt32(0),
                            Title = reader.GetString(1),
                            Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            DueDate = reader.GetDateTime(3),
                            Status = reader.GetString(4),
                            Property = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Type = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            CreatedBy = reader.IsDBNull(7) ? "" : reader.GetString(7)
                        });
                    }
                }
            }

            return results;
        }

        // SALES RANKING SECTION
        private (List<SalesRanking> MTD, List<SalesRanking> YTD, DateTime mtdStart, DateTime mtdEnd, DateTime ytdStart, DateTime ytdEnd) GetSalesRanking(int whsId = 0)
        {
            var mtdResults = new List<SalesRanking>();
            var ytdResults = new List<SalesRanking>();
            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);

            DateTime today = DateTime.Today;
            DateTime mtdStart = new DateTime(today.Year, today.Month, 1);
            DateTime ytdStart = new DateTime(today.Year, 1, 1);

            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(@"
                    DECLARE @MTD_Start DATE = @pMTD_Start;
                    DECLARE @MTD_End DATE = @pToday;
                    DECLARE @YTD_Start DATE = @pYTD_Start;
                    DECLARE @YTD_End DATE = @pToday;

                    -- MTD
                    SELECT
                        SM.SMN_SALESMAN_NAME,
                        IHF.IHF_SMNMAS_ORDER AS SalespersonID,
                        SUM(ARO.ARO_INVOICE_AMOUNT) AS MTDSales
                    FROM AR_OPEN_ITEM AS ARO
                    LEFT JOIN INVOICE_HEADER AS IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                    LEFT JOIN SALESMAN_MASTER AS SM ON IHF.IHF_SMNMAS_ORDER = SM.SMN_SMNMAS_ID
                    WHERE 
                        IHF.IHF_INVOICE_DATE BETWEEN @MTD_Start AND @MTD_End
                        AND IHF.IHF_CANCELED_DATE IS NULL
                        AND ARO.ARO_INVOICE_TYPE = 'I'
                        AND (@WhsId = 0 OR ARO.ARO_WHSMAS_ID = @WhsId)
                        AND ARO.ARO_INVOICE_AMOUNT <> 0
                    GROUP BY SM.SMN_SALESMAN_NAME, IHF.IHF_SMNMAS_ORDER
                    ORDER BY MTDSales DESC;

                    -- YTD
                    SELECT
                        SM.SMN_SALESMAN_NAME,
                        IHF.IHF_SMNMAS_ORDER AS SalespersonID,
                        SUM(ARO.ARO_INVOICE_AMOUNT) AS YTDSales
                    FROM AR_OPEN_ITEM AS ARO
                    LEFT JOIN INVOICE_HEADER AS IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                    LEFT JOIN SALESMAN_MASTER AS SM ON IHF.IHF_SMNMAS_ORDER = SM.SMN_SMNMAS_ID
                    WHERE 
                        IHF.IHF_INVOICE_DATE BETWEEN @YTD_Start AND @YTD_End
                        AND IHF.IHF_CANCELED_DATE IS NULL
                        AND ARO.ARO_INVOICE_TYPE = 'I'
                        AND (@WhsId = 0 OR ARO.ARO_WHSMAS_ID = @WhsId)
                        AND ARO.ARO_INVOICE_AMOUNT <> 0
                    GROUP BY SM.SMN_SALESMAN_NAME, IHF.IHF_SMNMAS_ORDER
                    ORDER BY YTDSales DESC;
                ", conn);

                cmd.Parameters.AddWithValue("@pMTD_Start", mtdStart);
                cmd.Parameters.AddWithValue("@pYTD_Start", ytdStart);
                cmd.Parameters.AddWithValue("@pToday", today);
                cmd.Parameters.AddWithValue("@whsId", whsId);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    // Read MTD results
                    while (reader.Read())
                    {
                        mtdResults.Add(new SalesRanking
                        {
                            SalespersonName = reader.GetString(0),
                            SalespersonID = reader.GetInt32(1),
                            MTDSales = Convert.ToDecimal(reader[2])
                        });
                    }

                    // Move to YTD results
                    if (reader.NextResult())
                    {
                        // Read YTD results
                        while (reader.Read())
                        {
                            ytdResults.Add(new SalesRanking
                            {
                                SalespersonName = reader.GetString(0),
                                SalespersonID = reader.GetInt32(1),
                                YTDSales = Convert.ToDecimal(reader[2])
                            });
                        }
                    }
                }
            }

            // Define Grouping Rules
            var allGroupMappings = new Dictionary<string, Dictionary<int, (string GroupName, int GroupID)>>
            {
                // Los Angeles
                ["LAX"] = new Dictionary<int, (string, int)> {
                    { 9, ("Steve Standage", 9) },
                    { 28, ("Steve Standage", 9) }, // Military
                },
                // Las Vegas
                ["LSV"] = new Dictionary<int, (string, int)>
                {
                    { 6, ("Giselle Rodriguez", 6) },
                    { 19, ("Giselle Rodriguez", 6) }, // House Giselle
                    { 2, ("Hilda Magana", 2) },
                    { 18, ("Hilda Magana", 2) }, // House Hilda
                    { 25, ("Alex Reynoso", 25) },
                    { 22, ("Alex Reynoso", 25) }, // House Alex
                    { 1, ("House", 1) },
                    { 15, ("House", 1) }, // Z.Anthony
                    { 21, ("House", 1) }, // Z.House Anthony
                    { 24, ("House", 1) }, // Z.House Demarcus
                    { 23, ("House", 1) }, // Z.Demarcus
                    { 26, ("House", 1) }, // Z.House Karla
                    { 27, ("House", 1) }, // Z.House Sean
                    { 17, ("House", 1) }, // Z.Chellie
                    { 20, ("House", 1) }, // Z.House Chellie
                },
                // Chino
                ["CHN"] = new Dictionary<int, (string, int)>
                {
                    { 22, ("Joshua Legaspi", 22) },
                    { 26, ("Joshua Legaspi", 22) }, // Military
                    { 1, ("House", 1) },
                    { 10, ("House", 1) }, // Z.Tony Alberti
                    { 29, ("House", 1) }, // Z.Vanessa Ray
                },
                // San Diego
                ["SND"] = new Dictionary<int, (string, int)>
                {
                    { 20, ("House", 20) },
                    { 26, ("House", 20) }, // Z.Stossi
                },
                // Phoenix
                ["PHX"] = new Dictionary<int, (string, int)> {
                    { 14, ("Heather Keytack", 14) },
                    { 27, ("Heather Keytack", 14) }, // Split 13/12
                    { 26, ("Heather Keytack", 14) }, // Split 20/5
                    { 1, ("House", 1) },
                    { 19, ("House", 1) } // Z.Brian Kmetko
                }
            };

            // Fallback if not found
            var groupMappings = allGroupMappings.ContainsKey(officeLocation)
                ? allGroupMappings[officeLocation]
                : new Dictionary<int, (string, int)>();

            // Group MTD results
            var groupedMTDResults = mtdResults
                .GroupBy(r => groupMappings.ContainsKey(r.SalespersonID) ? groupMappings[r.SalespersonID].Item2 : r.SalespersonID)
                .Select(g =>
                {
                    var first = g.First();
                    var name = groupMappings.ContainsKey(first.SalespersonID) ? groupMappings[first.SalespersonID].Item1 : first.SalespersonName;

                    return new SalesRanking
                    {
                        SalespersonID = g.Key,
                        SalespersonName = name,
                        MTDSales = g.Sum(x => x.MTDSales),
                    };
                })
                .OrderByDescending(r => r.MTDSales)
                .Take(5)
                .ToList();

            // Group YTD results
            var groupedYTDResults = ytdResults
                .GroupBy(r => groupMappings.ContainsKey(r.SalespersonID) ? groupMappings[r.SalespersonID].Item2 : r.SalespersonID)
                .Select(g =>
                {
                    var first = g.First();
                    var name = groupMappings.ContainsKey(first.SalespersonID) ? groupMappings[first.SalespersonID].Item1 : first.SalespersonName;

                    return new SalesRanking
                    {
                        SalespersonID = g.Key,
                        SalespersonName = name,
                        YTDSales = g.Sum(x => x.YTDSales),
                    };
                })
                .OrderByDescending(r => r.YTDSales)
                .Take(5)
                .ToList();

            return (groupedMTDResults, groupedYTDResults, mtdStart, today, ytdStart, today);
        }

        public List<InactiveCustomerViewModel> GetInactiveCustomers(string connectionString, int roleId = 0, int salesmanId = 0, int whsId = 0)
        {
            DateTime today = DateTime.Today;
            DateTime CutOffStart = today.AddDays(-31);
            DateTime CutOffEnd = today.AddMonths(-14).AddDays(today.Day - 1);

            var results = new List<InactiveCustomerViewModel>();

            using (var conn = new SqlConnection(connectionString))
            {
                string sql = @"
                    WITH CustomerActivityData AS (
                        SELECT 
                            I.[IHF_ORDER_DATE], 
                            C.[CUM_CUMMAS_ID],
                            C.[CUM_CUSTOMER_NUMBER], 
                            C.[CUM_CUSTOMER_NAME], 
                            P.[IPC_DESCRIPTION], 
                            C.[CUM_PHONE_NUMBER], 
                            S.[SOH_SHIP_TO_ADDRESS_1], 
                            S.[SOH_SHIP_TO_CITY], 
                            S.[SOH_SHIP_TO_STATE], 
                            S.[SOH_SHIP_TO_ZIP], 
                            S.[SOH_ORDER_DATE], 
                            I.[IHF_DELIVERY_DATE],
                            I.[IHF_PRICE_CODE], 
                            I.[IHF_WHSMAS_ID], 
                            SM.[SMN_SMNMAS_ID],
                            SM.[SMN_SALESMAN_NAME], 
                            I.[IHF_TOTAL_AMOUNT],
		                    COM.numberofunits
                        FROM 
                            INVOICE_HEADER AS I
                            LEFT OUTER JOIN CUSTOMER_MASTER AS C ON I.[IHF_CUSTOMER_NUMBER] = C.[CUM_CUSTOMER_NUMBER]
                            LEFT OUTER JOIN PRICE_CODES AS P ON I.[IHF_PRICE_CODE] = P.[IPC_PRICE_CODE]
                            LEFT OUTER JOIN SALES_HEADER AS S ON I.[IHF_ORDER_NUMBER] = S.[SOH_NUMBER]
                            LEFT OUTER JOIN SALESMAN_MASTER AS SM ON S.[SOH_SMNMAS_ID] = SM.[SMN_SMNMAS_ID]
		                    LEFT OUTER JOIN Complex as COM on C.CUM_CUMMAS_ID = COM.Customer_id
                        WHERE  
                            (@WhsId = 0 OR I.[IHF_WHSMAS_ID] = @WhsId)
                            AND S.[SOH_CANCELED_DATE] IS NULL
                            AND C.[CUM_CUSTOMER_NAME] IS NOT NULL
                    ),
                    YTD_Sales AS (
                        SELECT 
                            C.[CUM_CUSTOMER_NUMBER],
                            SUM(CASE WHEN YEAR(I.[IHF_ORDER_DATE]) = YEAR(GETDATE()) - 1 THEN I.[IHF_TOTAL_AMOUNT] ELSE 0 END) AS [YTD_Prior],
                            SUM(CASE WHEN YEAR(I.[IHF_ORDER_DATE]) = YEAR(GETDATE()) THEN I.[IHF_TOTAL_AMOUNT] ELSE 0 END) AS [YTD_Current]
                        FROM 
                            INVOICE_HEADER AS I
                            LEFT JOIN CUSTOMER_MASTER AS C ON I.[IHF_CUSTOMER_NUMBER] = C.[CUM_CUSTOMER_NUMBER]
                        WHERE 
                            I.[IHF_ORDER_DATE] >= DATEFROMPARTS(YEAR(GETDATE()) - 1, 1, 1)
                        GROUP BY 
                            C.[CUM_CUSTOMER_NUMBER]
                    ), 
                    AR_Sales as (
	                    SELECT 
		                    ARO_CUSTOMER_NUMBER, 
		                    MAX(ARO_INVOICE_DATE) as ARO_INVOICE_DATE, 
		                    MAX(ARO_INVOICE_BALANCE_DUE) as ARO_INVOICE_BALANCE_DUE, 
		                    COUNT(DISTINCT ARO_SALES_ORDER_NUMBER) as ARO_SALES_ORDERS_COUNT , 
		                    MAX(ARO_DATE_PAID_IN_FULL) as LAST_PAID_DATE
	                    FROM AR_OPEN_ITEM A
	                    GROUP BY ARO_CUSTOMER_NUMBER
                    )
                    SELECT 
                        CAD.CUM_CUMMAS_ID as [CustomerID],
                        CAD.CUM_CUSTOMER_NUMBER AS [Customer#], 
                        CAD.CUM_CUSTOMER_NAME AS [Customer Name],
                        CAD.IHF_PRICE_CODE AS [Price Code],
                        CAD.IPC_DESCRIPTION AS [Mgmt Co],
	                    ISNULL(MAX(CAD.numberofunits), 0) as [# of Units],
                        --SUM(CAD.IHF_TOTAL_AMOUNT) AS [Order Sum],
                        CAST(MAX(CAD.IHF_ORDER_DATE) AS DATE) AS [Last Order Date],
	                    DATEDIFF(day, MAX(CAD.IHF_ORDER_DATE), GETDATE()) as [Last Order Days],
                        CAST(MAX(CAD.IHF_DELIVERY_DATE) AS DATE) AS [Last Install Date],
	                    DATEDIFF(day, MAX(CAD.IHF_DELIVERY_DATE), GETDATE()) as [Last Install Days],

                        ISNULL(ROUND(MAX(YTD.[YTD_Current]), 2), 0) AS [YTD Current],
	                    ISNULL(ROUND(MAX(YTD.[YTD_Prior]), 2), 0) AS [YTD Previous],
	
	                    MAX(A.ARO_INVOICE_BALANCE_DUE) as [AR Balance],
                        MAX(CAD.SMN_SMNMAS_ID) as [SalespersonID],
	                    MAX(CAD.SMN_SALESMAN_NAME) as [Salesperson],
                        CAD.IHF_WHSMAS_ID AS [WhsID],
	                    CAST(Max(C.CUM_ESTABLISHED_DATE) as DATE) as [Established Date]
                    FROM 
                        CustomerActivityData AS CAD
                        LEFT JOIN YTD_Sales AS YTD ON CAD.CUM_CUSTOMER_NUMBER = YTD.CUM_CUSTOMER_NUMBER
	                    LEFT JOIN AR_Sales as A on CAD.CUM_CUSTOMER_NUMBER = A.ARO_CUSTOMER_NUMBER
	                    LEFT JOIN CUSTOMER_MASTER as C on CAD.CUM_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER

                    WHERE CAD.IHF_PRICE_CODE <> 1
	                    AND NOT EXISTS (
		                    SELECT 1 
		                    FROM INSTALLER_MASTER IM 
		                    WHERE IM.INS_INSTALLER_NUMBER = CAD.CUM_CUSTOMER_NUMBER
	                    )";

                if (salesmanId > 0)
                {
                    sql += " AND CAD.SMN_SMNMAS_ID = @SalesmanID";
                }

                sql += @" GROUP BY 
                        CAD.IHF_WHSMAS_ID, 
                        CAD.IHF_PRICE_CODE, 
                        CAD.IPC_DESCRIPTION, 
                        CAD.CUM_CUMMAS_ID,
                        CAD.CUM_CUSTOMER_NUMBER, 
                        CAD.CUM_CUSTOMER_NAME
                    HAVING 
	                    MAX(CAD.IHF_DELIVERY_DATE) BETWEEN @InactiveStartDate AND @InactiveEndDate
                    ORDER BY 
	                    MAX(CAD.SMN_SALESMAN_NAME), CAD.IPC_DESCRIPTION, CAD.CUM_CUSTOMER_NAME;
                ";

                var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@InactiveStartDate", CutOffEnd);
                cmd.Parameters.AddWithValue("@InactiveEndDate", CutOffStart);
                cmd.Parameters.AddWithValue("@WhsId", whsId);
                
                if (salesmanId > 0)
                {
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                }

                conn.Open();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new InactiveCustomerViewModel
                    {
                        CustomerId = reader.GetInt32("CustomerId"),
                        CustomerNumber = reader["Customer#"].ToString(),
                        CustomerName = reader["Customer Name"].ToString(),
                        PriceCode = reader["Price Code"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Price Code"]),
                        MgmtCo = reader["Mgmt Co"].ToString(),
                        Units = Convert.ToInt32(reader["# of Units"]),
                        LastOrderDate = Convert.ToDateTime(reader["Last Order Date"]),
                        LastInstallDate = Convert.ToDateTime(reader["Last Install Date"]),
                        DaysSinceLastOrder = Convert.ToInt32(reader["Last Order Days"]),
                        DaysSinceLastInstall = Convert.ToInt32(reader["Last Install Days"]),
                        YTDCurrent = reader.IsDBNull("YTD Current") ? 0 : Convert.ToDouble(reader["YTD Current"]),
                        YTDPrevious = reader.IsDBNull("YTD Previous") ? 0 : Convert.ToDouble(reader["YTD Previous"]),
                        Balance = reader.IsDBNull("AR Balance") ? 0 : Convert.ToDouble(reader["AR Balance"]),
                        SalesmanID = reader.IsDBNull("SalespersonID") ? 0 : Convert.ToInt32(reader["SalespersonID"]),

                        Salesperson = reader["Salesperson"].ToString(),
                        EstablishedDate = Convert.ToDateTime(reader["Established Date"])
                    });
                }
            }

            return results;
        }

        // END OF CONTROLLER
    }
}
