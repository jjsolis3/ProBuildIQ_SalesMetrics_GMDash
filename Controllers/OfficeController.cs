using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Services.Helpers;
using SalesMetrics.Services.Erp;
using SalesMetrics.Services.Permissions;
using System.Data;
using System.Security.Claims;

namespace SalesMetrics.Controllers
{
    /// <summary>
    /// Office Dashboard for Office Staff and Office Manager roles
    /// Shows envelope metrics, weekly jobs, inactive customers, and AR data
    /// </summary>
    [Authorize]
    public class OfficeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ErpClientFactory _erpFactory;
        private readonly SalesMetricsDbContext _db;
        private readonly IPermissionService _permissionService;

        public OfficeController(IConfiguration configuration, ErpClientFactory erpFactory, SalesMetricsDbContext db, IPermissionService permissionService)
        {
            _configuration = configuration;
            _erpFactory = erpFactory;
            _db = db;
            _permissionService = permissionService;
        }

        /// <summary>
        /// Helper to create ErpContext from current session location
        /// </summary>
        private ErpContext GetErpContext()
        {
            var locationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
            return new ErpContext { LocationCode = locationCode };
        }

        /// <summary>
        /// Office Dashboard - Shows envelope metrics and key business data
        /// Access: Office Manager (RoleId 5), Office Staff (RoleId 6)
        /// </summary>
        public async Task<IActionResult> Index(int weekOffset = 0)
        {
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);

            var users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            var userId = HttpContext.Session.GetString("UserId");
            var fullName = HttpContext.Session.GetString("FullName");
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));

            if (string.IsNullOrEmpty(userId) || users_Id == 0)
            {
                userId = User.FindFirstValue("UserId");
                users_Id = Convert.ToInt32(HttpContext.Session.GetString("Users_ID"));
                if (string.IsNullOrEmpty(userId))
                    return RedirectToAction("Login", "Auth");

                fullName = User.FindFirstValue("FullName");
            }

            // Access control - Only Office Manager (5) and Office Staff (6)
            if (roleId != 5 && roleId != 6)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            // Check envelope feature permissions for Quick Actions
            var hasEnvelopesAccess = await _permissionService.HasFeatureAccessAsync(users_Id, "Envelopes");
            var hasEnvelopeTemplatesAccess = await _permissionService.HasFeatureAccessAsync(users_Id, "EnvelopeTemplates");

            // Get envelope metrics
            var envelopeMetrics = await GetEnvelopeMetricsAsync(locationId);

            // Get recent envelope activity
            var recentEnvelopes = await GetRecentEnvelopesAsync(users_Id, locationId);

            // Get documents needing follow-up (sent 3+ days ago, still pending)
            var needsFollowUp = await GetEnvelopesNeedingFollowUpAsync(locationId);

            // Get weekly jobs data
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            int daysToSunday = (int)DateTime.Today.DayOfWeek;
            DateTime startOfWeek = DateTime.Today.AddDays(-daysToSunday).Date.AddDays(weekOffset * 7);
            DateTime endOfWeek = startOfWeek.AddDays(6);

            var dailyOrderCounts = await client.GetDailyOrderCountsAsync(
                startOfWeek,
                endOfWeek,
                context,
                null);

            var weeklyOrders = dailyOrderCounts.Select(d => new DailyOrderCount
            {
                WeekdayName = d.WeekdayName,
                OrdersCount = d.OrdersCount,
                TotalOrderAmount = d.TotalOrderAmount
            }).ToList();

            // Get AR aging summary
            var arSummary = await client.GetARAgingSummaryAsync(context, null);

            var transactionSummary = new TransactionSummary
            {
                PendingInvoices = arSummary.PendingInvoices,
                PendingInvoicesAmount = (double)arSummary.PendingInvoicesAmount,
                DueUnder30 = arSummary.DueUnder30,
                DueUnder30Amount = (double)arSummary.DueUnder30Amount,
                Due30to60 = arSummary.Due30to60,
                Due30to60Amount = (double)arSummary.Due30to60Amount,
                Due60to90 = arSummary.Due60to90,
                Due60to90Amount = (double)arSummary.Due60to90Amount,
                Due90to120 = arSummary.Due90to120,
                Due90to120Amount = (double)arSummary.Due90to120Amount,
                DueOver120 = arSummary.DueOver120,
                DueOver120Amount = (double)arSummary.DueOver120Amount,
                TopDelinquentCustomers = arSummary.TopDelinquentCustomers.Select(c => new CustomerOutstanding
                {
                    CustomerName = c.CustomerName,
                    CustomerNumber = c.CustomerNumber,
                    CustomerId = c.CustomerId,
                    BalanceDue = c.BalanceDue,
                    OutstandingAmount = c.OutstandingAmount
                }).ToList()
            };

            // Get overdue invoices
            var erpOverdueInvoices = await client.GetOverdueInvoicesAsync(context, null);

            var overdueInvoices = erpOverdueInvoices.Select(oi => new OverdueInvoice
            {
                Invoice = int.Parse(oi.InvoiceNumber),
                CustomerId = oi.CustomerId,
                CustomerName = oi.CustomerName,
                MgmtCo = oi.ManagementCompany,
                OutstandingAmount = oi.OutstandingAmount,
                DueDate = oi.DueDate,
                DaysPastDue = oi.DaysPastDue,
                InvoiceAging = oi.InvoiceAging,
                SalespersonId = oi.SalesmanId ?? 0,
                Salesperson = oi.SalesmanName
            }).ToList();

            // Get inactive customers
            var inactiveCustomers = GetInactiveCustomers(connectionString, roleId, 0, 0);

            // Set ViewBag data
            ViewBag.RoleId = roleId;
            ViewBag.UserId = userId;
            ViewBag.Users_Id = users_Id;
            ViewBag.Username = fullName?.Split(' ')[0];
            ViewBag.Greeting = GetGreeting();

            // Envelope metrics
            ViewBag.EnvelopeMetrics = envelopeMetrics;
            ViewBag.RecentEnvelopes = recentEnvelopes;
            ViewBag.NeedsFollowUp = needsFollowUp;

            // Envelope permissions for Quick Actions
            ViewBag.HasEnvelopesAccess = hasEnvelopesAccess;
            ViewBag.HasEnvelopeTemplatesAccess = hasEnvelopeTemplatesAccess;

            // Weekly jobs
            ViewBag.WeeklyOrders = weeklyOrders ?? new List<DailyOrderCount>();
            ViewBag.WeeklyStart = startOfWeek;
            ViewBag.WeeklyEnd = endOfWeek;
            ViewBag.WeekOffset = weekOffset;

            // AR data
            ViewBag.TransactionSummary = transactionSummary;
            ViewBag.OverdueInvoices = overdueInvoices;

            // Inactive customers
            ViewBag.InactiveCustomers = inactiveCustomers;

            ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("Office");

            return View();
        }

        /// <summary>
        /// Get envelope metrics for the current month
        /// </summary>
        private async Task<EnvelopeMetrics> GetEnvelopeMetricsAsync(int locationId)
        {
            var firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var today = DateTime.Today.AddDays(1); // Include today

            var locationCode = LocationHelper.Locations.ContainsKey(locationId)
                ? LocationHelper.Locations[locationId].Code
                : null;

            var envelopes = await _db.SignEnvelopes
                .Where(e => e.CreatedDateUtc >= firstDayOfMonth && e.CreatedDateUtc < today)
                .Where(e => locationCode == null || e.LocationCode == locationCode)
                .ToListAsync();

            var sentEnvelopes = envelopes.Where(e => e.Status != "Draft").ToList();
            var completedEnvelopes = envelopes.Where(e => e.Status == "Completed").ToList();
            var pendingEnvelopes = envelopes.Where(e => e.Status == "Sent" || e.Status == "Viewed").ToList();

            var completionRate = sentEnvelopes.Count > 0
                ? (double)completedEnvelopes.Count / sentEnvelopes.Count * 100
                : 0;

            // Calculate average time to sign (in hours)
            var completedWithTimes = completedEnvelopes
                .Where(e => e.SentAtUtc.HasValue && e.CompletedAtUtc.HasValue)
                .Select(e => (e.CompletedAtUtc!.Value - e.SentAtUtc!.Value).TotalHours)
                .ToList();

            var avgTimeToSign = completedWithTimes.Any()
                ? completedWithTimes.Average()
                : 0;

            // Get status breakdown
            var statusBreakdown = envelopes
                .GroupBy(e => e.Status)
                .Select(g => new StatusCount { Status = g.Key, Count = g.Count() })
                .ToList();

            return new EnvelopeMetrics
            {
                EnvelopesSentThisMonth = sentEnvelopes.Count,
                CompletionRate = completionRate,
                PendingSignatures = pendingEnvelopes.Count,
                AvgTimeToSign = avgTimeToSign,
                StatusBreakdown = statusBreakdown
            };
        }

        /// <summary>
        /// Get recent envelope activity (last 10 envelopes)
        /// </summary>
        private async Task<List<RecentEnvelope>> GetRecentEnvelopesAsync(int userId, int locationId)
        {
            var locationCode = LocationHelper.Locations.ContainsKey(locationId)
                ? LocationHelper.Locations[locationId].Code
                : null;

            var envelopes = await _db.SignEnvelopes
                .Where(e => locationCode == null || e.LocationCode == locationCode)
                .OrderByDescending(e => e.CreatedDateUtc)
                .Take(10)
                .Select(e => new RecentEnvelope
                {
                    EnvelopeId = e.EnvelopeId,
                    Subject = e.Subject,
                    Status = e.Status,
                    CreatedDateUtc = e.CreatedDateUtc,
                    LocationCode = e.LocationCode
                })
                .ToListAsync();

            return envelopes;
        }

        /// <summary>
        /// Get envelopes needing follow-up (sent 3+ days ago, still pending)
        /// </summary>
        private async Task<List<EnvelopeFollowUp>> GetEnvelopesNeedingFollowUpAsync(int locationId)
        {
            var threeDaysAgo = DateTime.UtcNow.AddDays(-3);

            var locationCode = LocationHelper.Locations.ContainsKey(locationId)
                ? LocationHelper.Locations[locationId].Code
                : null;

            var envelopes = await _db.SignEnvelopes
                .Where(e => e.SentAtUtc.HasValue && e.SentAtUtc.Value <= threeDaysAgo)
                .Where(e => e.Status == "Sent" || e.Status == "Viewed")
                .Where(e => locationCode == null || e.LocationCode == locationCode)
                .OrderBy(e => e.SentAtUtc)
                .Select(e => new EnvelopeFollowUp
                {
                    EnvelopeId = e.EnvelopeId,
                    Subject = e.Subject,
                    Status = e.Status,
                    SentAtUtc = e.SentAtUtc!.Value,
                    DaysPending = (int)(DateTime.UtcNow - e.SentAtUtc!.Value).TotalDays,
                    LocationCode = e.LocationCode
                })
                .ToListAsync();

            return envelopes;
        }

        /// <summary>
        /// Get weekly orders for a specific day
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GetWeeklyOrders(int weekOffset = 0)
        {
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            int daysToSunday = (int)DateTime.Today.DayOfWeek;
            DateTime startOfWeek = DateTime.Today.AddDays(-daysToSunday).Date.AddDays(weekOffset * 7);
            DateTime endOfWeek = startOfWeek.AddDays(6);

            var dailyOrderCounts = await client.GetDailyOrderCountsAsync(
                startOfWeek,
                endOfWeek,
                context,
                null);

            var orders = dailyOrderCounts.Select(d => new DailyOrderCount
            {
                WeekdayName = d.WeekdayName,
                OrdersCount = d.OrdersCount,
                TotalOrderAmount = d.TotalOrderAmount
            }).ToList();

            return Json(orders);
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

        private List<InactiveCustomerViewModel> GetInactiveCustomers(string connectionString, int roleId = 0, int salesmanId = 0, int whsId = 0)
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
                        CustomerId = Convert.ToInt32(reader["CustomerId"]),
                        CustomerNumber = reader["Customer#"]?.ToString() ?? "",
                        CustomerName = reader["Customer Name"]?.ToString() ?? "",
                        PriceCode = reader["Price Code"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Price Code"]),
                        MgmtCo = reader["Mgmt Co"]?.ToString() ?? "",
                        Units = Convert.ToInt32(reader["# of Units"]),
                        LastOrderDate = Convert.ToDateTime(reader["Last Order Date"]),
                        LastInstallDate = Convert.ToDateTime(reader["Last Install Date"]),
                        DaysSinceLastOrder = Convert.ToInt32(reader["Last Order Days"]),
                        DaysSinceLastInstall = Convert.ToInt32(reader["Last Install Days"]),
                        YTDCurrent = reader["YTD Current"] == DBNull.Value ? 0 : Convert.ToDouble(reader["YTD Current"]),
                        YTDPrevious = reader["YTD Previous"] == DBNull.Value ? 0 : Convert.ToDouble(reader["YTD Previous"]),
                        Balance = reader["AR Balance"] == DBNull.Value ? 0 : Convert.ToDouble(reader["AR Balance"]),
                        SalesmanID = reader["SalespersonID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SalespersonID"]),
                        Salesperson = reader["Salesperson"]?.ToString() ?? "",
                        EstablishedDate = Convert.ToDateTime(reader["Established Date"])
                    });
                }
            }

            return results;
        }
    }

    // View Models for Office Dashboard
    public class EnvelopeMetrics
    {
        public int EnvelopesSentThisMonth { get; set; }
        public double CompletionRate { get; set; }
        public int PendingSignatures { get; set; }
        public double AvgTimeToSign { get; set; }
        public List<StatusCount> StatusBreakdown { get; set; } = new List<StatusCount>();
    }

    public class StatusCount
    {
        public string Status { get; set; } = default!;
        public int Count { get; set; }
    }

    public class RecentEnvelope
    {
        public long EnvelopeId { get; set; }
        public string Subject { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime CreatedDateUtc { get; set; }
        public string? LocationCode { get; set; }
    }

    public class EnvelopeFollowUp
    {
        public long EnvelopeId { get; set; }
        public string Subject { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime SentAtUtc { get; set; }
        public int DaysPending { get; set; }
        public string? LocationCode { get; set; }
    }
}
