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
using SalesMetrics.Services.Erp;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace SalesMetrics.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ErpClientFactory _erpFactory;

        public DashboardController(IConfiguration configuration, ErpClientFactory erpFactory)
        {
            _configuration = configuration;
            _erpFactory = erpFactory;
        }

        /// <summary>
        /// Helper to create ErpContext from current session location
        /// </summary>
        private ErpContext GetErpContext()
        {
            var locationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
            return new ErpContext { LocationCode = locationCode };
        }

        //private bool IsSalesPerson(int roleId) => roleId == 2;

        [Authorize]
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, int weekOffset = 0, int? rangeType = 0, int? filterSalesmanId = null, int whsId = 0)
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
            // For management roles, use the filterSalesmanId if provided
            else if (RoleHelper.CanViewSalesTeamData(roleId))
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

            // Fetch Sales Data from ERP using abstraction layer
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            // Get sales metrics
            var salesMetrics = await client.GetSalesMetricsAsync(
                startDate ?? DateTime.Today.AddDays(-30),
                endDate ?? DateTime.Today,
                context,
                effectiveSalesmanId > 0 ? effectiveSalesmanId : null);

            // Map ErpSalesMetrics to Dictionary<string, object> for backward compatibility
            var salesData = new Dictionary<string, object>
            {
                ["TotalSales"] = salesMetrics.TotalRevenue,
                ["AmountDue"] = salesMetrics.AmountDue,
                ["PropertyCount"] = salesMetrics.PropertyCount,
                ["PropertyNameCount"] = salesMetrics.PropertyNameCount,
                ["TotalInvoices"] = salesMetrics.TotalInvoices,
                ["PaidInvoices"] = salesMetrics.PaidInvoices,
                ["OverdueInvoices"] = salesMetrics.OverdueInvoices,
                ["AvgInvoiceAmount"] = salesMetrics.AverageOrderValue,
                ["MaxInvoiceAmount"] = salesMetrics.MaxInvoiceAmount,
                ["MinInvoiceAmount"] = salesMetrics.MinInvoiceAmount
            };

            // Get weekly orders with range
            int daysToSunday = (int)DateTime.Today.DayOfWeek;
            DateTime startOfWeek = DateTime.Today.AddDays(-daysToSunday).Date.AddDays(weekOffset * 7);
            DateTime endOfWeek = startOfWeek.AddDays(6);

            var dailyOrderCounts = await client.GetDailyOrderCountsAsync(
                startOfWeek,
                endOfWeek,
                context,
                effectiveSalesmanId > 0 ? effectiveSalesmanId : null);

            // Map ErpDailyOrderCount to DailyOrderCount for backward compatibility
            var weeklyOrders = dailyOrderCounts.Select(d => new DailyOrderCount
            {
                WeekdayName = d.WeekdayName,
                OrdersCount = d.OrdersCount,
                TotalOrderAmount = d.TotalOrderAmount
            }).ToList();

            // Get AR aging summary
            var arSummary = await client.GetARAgingSummaryAsync(
                context,
                effectiveSalesmanId > 0 ? effectiveSalesmanId : null);

            // Map ErpARAgingSummary to TransactionSummary for backward compatibility
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
            var erpOverdueInvoices = await client.GetOverdueInvoicesAsync(
                context,
                effectiveSalesmanId > 0 ? effectiveSalesmanId : null);

            // Map ErpOverdueInvoice to OverdueInvoice for backward compatibility
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

            var todayTasks = GetTodayTasks(selectedUserId, locationId);
            var inactiveCustomers = GetInactiveCustomers(connectionString, roleId, effectiveSalesmanId, whsId);

            ViewBag.RoleId = roleId;
            ViewBag.UserId = userId;
            ViewBag.Users_Id = users_Id;

            // Management roles get dropdown to filter by sales team members
            if (RoleHelper.CanViewSalesTeamData(roleId))
            {
                ViewBag.Users = GetActiveUsers(Convert.ToInt32(users_Id), roleId, Convert.ToInt32(locationId));
            }
            else if (roleId == 2)
            {
                ViewBag.Users = new List<User>();
                ViewBag.SalespersonId = salesmanId;
            }

            ViewBag.SelectedWhsId = whsId;
            ViewBag.WhsList = await GetWarehouseIDAsync();

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

        private async Task<List<Warehouses>> GetWarehouseIDAsync()
        {
            // Use ERP abstraction layer
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            var erpWarehouses = await client.GetWarehousesAsync(context);

            // Map ErpWarehouse to Warehouses for backward compatibility
            // Filter to warehouse numbers < 10 (original logic)
            var whsList = new List<Warehouses>();
            foreach (var w in erpWarehouses)
            {
                if (int.TryParse(w.WarehouseNumber, out var num) && num < 10)
                {
                    whsList.Add(new Warehouses
                    {
                        WhsID = num,  // Use the already-parsed value
                        WhsName = w.WarehouseName
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

                // Management roles see all sales team members in their location
                string query = RoleHelper.CanViewSalesTeamData(roleId)
                    ? @"SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE SalesmanID IS NOT NULL AND Location = @Location"
                    : @"SELECT Users_ID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Users_ID = @Users_Id";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (RoleHelper.CanViewSalesTeamData(roleId))
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

        [HttpGet]
        public async Task<IActionResult> GetKpiTiles(DateTime startDate, DateTime endDate, int? filterSalesmanId = null, int whsId = 0)
        {
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            int effectiveSalesmanId = 0;

            if (roleId == 2)
                effectiveSalesmanId = Convert.ToInt32(HttpContext.Session.GetInt32("SalesmanId"));
            else if (RoleHelper.CanViewSalesTeamData(roleId))
                effectiveSalesmanId = filterSalesmanId ?? 0;

            // Use ERP abstraction layer
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            var salesMetrics = await client.GetSalesMetricsAsync(
                startDate,
                endDate,
                context,
                effectiveSalesmanId > 0 ? effectiveSalesmanId : null);

            // Map to Dictionary for backward compatibility
            var salesData = new Dictionary<string, object>
            {
                ["TotalSales"] = salesMetrics.TotalRevenue,
                ["AmountDue"] = salesMetrics.AmountDue,
                ["PropertyCount"] = salesMetrics.PropertyCount,
                ["PropertyNameCount"] = salesMetrics.PropertyNameCount,
                ["TotalInvoices"] = salesMetrics.TotalInvoices,
                ["PaidInvoices"] = salesMetrics.PaidInvoices,
                ["OverdueInvoices"] = salesMetrics.OverdueInvoices,
                ["AvgInvoiceAmount"] = salesMetrics.AverageOrderValue,
                ["MaxInvoiceAmount"] = salesMetrics.MaxInvoiceAmount,
                ["MinInvoiceAmount"] = salesMetrics.MinInvoiceAmount
            };

            ViewBag.SalesData = salesData;

            return PartialView("_KpiTilesPartial");
        }

        [HttpGet]
        public async Task<JsonResult> GetWeeklyOrders(int weekOffset = 0, int? filterSalesmanId = null, int whsId = 0)
        {
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            int effectiveSalesmanId = 0;

            if (roleId == 2)
            {
                effectiveSalesmanId = Convert.ToInt32(HttpContext.Session.GetInt32("SalesmanId"));
            }
            else if (RoleHelper.CanViewSalesTeamData(roleId))
            {
                effectiveSalesmanId = filterSalesmanId ?? 0;
            }

            // Use ERP abstraction layer
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            // Calculate week range
            int daysToSunday = (int)DateTime.Today.DayOfWeek;
            DateTime startOfWeek = DateTime.Today.AddDays(-daysToSunday).Date.AddDays(weekOffset * 7);
            DateTime endOfWeek = startOfWeek.AddDays(6);

            var dailyOrderCounts = await client.GetDailyOrderCountsAsync(
                startOfWeek,
                endOfWeek,
                context,
                effectiveSalesmanId > 0 ? effectiveSalesmanId : null);

            // Map to DailyOrderCount for backward compatibility
            var orders = dailyOrderCounts.Select(d => new DailyOrderCount
            {
                WeekdayName = d.WeekdayName,
                OrdersCount = d.OrdersCount,
                TotalOrderAmount = d.TotalOrderAmount
            }).ToList();

            return Json(orders);
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
                        PropertyId = reader["PropertyID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PropertyID"]),
                        PropertyNumber = reader["PropertyNumber"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PropertyNumber"]),
                        PropertyName = reader["PropertyName"]?.ToString(),
                        City = reader["City"]?.ToString(),
                        OrderType = reader["OrderType"]?.ToString(),
                        Qty = reader["Qty"] == DBNull.Value ? 0 : Convert.ToDouble(reader["Qty"]),
                        OrderTotal = reader["OrderTotal"] == DBNull.Value ? 0 : Math.Round(Convert.ToDecimal(reader["OrderTotal"]), 2),
                        UnitNumber = reader["UnitNumber"]?.ToString(),
                        UnitType = reader["UnitType"]?.ToString(),
                        DeliveryDate = reader["DeliveryDate"]?.ToString(),
                        ProductClass = reader["ProductClass"]?.ToString(),
                        ProductDescription = reader["ProductDescription"]?.ToString(),
                        OrderedBy = reader["OrderedBy"]?.ToString(),
                        ManagementName = reader["ManagementName"]?.ToString(),
                        Status = reader["Status"]?.ToString(),
                        Location = reader["Location"]?.ToString(),
                        SalesmanId = reader["SalesmanID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SalesmanID"]),
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
                            SalespersonName = reader.IsDBNull(0) ? "" : reader.GetString(0),
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
                                SalespersonName = reader.IsDBNull(0) ? "" : reader.GetString(0),
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
