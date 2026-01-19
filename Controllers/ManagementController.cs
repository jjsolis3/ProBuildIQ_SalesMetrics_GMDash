using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models;
using SalesMetrics.Services.Helpers;
using System.Data;
using System.Security.Claims;

namespace SalesMetrics.Controllers
{
    public class ManagementController : Controller
    {
        private readonly IConfiguration _configuration;
        public ManagementController(IConfiguration configuration) => _configuration = configuration;

        // GET: Management/Management
        // This displays the dashboard with all management companies, rankings, and aggregate KPIs
        // WHY: Management needs a high-level overview with key metrics and comparative rankings
        // viewMode: "ytd" for year-to-date comparison, "fullyear" for full calendar year comparison
        public IActionResult Management(string viewMode = "ytd")
        {
            ViewBag.ViewMode = viewMode; // Pass to view for toggle state
            var userId = HttpContext.Session.GetString("UserId");
            var users_Id = HttpContext.Session.GetString("Users_Id");
            var officeLocation = HttpContext.Session.GetString("OfficeLocation");
            int? roleId = null;
            var roleStr = HttpContext.Session.GetString("RoleId");
            int? salesmanId = HttpContext.Session.GetInt32("SalesmanId");

            if (!string.IsNullOrEmpty(roleStr) && int.TryParse(roleStr, out var parsedRole))
            {
                roleId = parsedRole;
            }

            if (string.IsNullOrEmpty(users_Id))
            {
                users_Id = User.FindFirstValue("Users_ID");
                officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);

                if (string.IsNullOrEmpty(users_Id))
                    return RedirectToAction("Login", "Auth");
            }

            var connectionString = _configuration.GetConnectionString(officeLocation);
            var managementList = new List<ManagementCompanyListItem>();

            // This SQL query aggregates data by Management Company (Price Code)
            // It calculates total revenue based on selected view mode (YTD or Full Year)
            // viewMode determines the date filtering logic
            var dateFilter = viewMode == "fullyear"
                ? @"AND (
                        -- Current year: Full year to date
                        YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE())
                        OR
                        -- Last year: Complete calendar year
                        YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE()) - 1
                    )"
                : @"AND (
                        -- Current year: From Jan 1 to today (YTD)
                        (YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE())
                         AND I.IHF_DELIVERY_DATE <= GETDATE())
                        OR
                        -- Last year: From Jan 1 to same date last year (YTD comparison)
                        (YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE()) - 1
                         AND I.IHF_DELIVERY_DATE <= DATEADD(YEAR, -1, GETDATE()))
                    )";

            var sql = $@"
                WITH InvoiceData AS (
                    SELECT
                        P.IPC_PRICE_CODE,
                        P.IPC_DESCRIPTION,
                        C.CUM_CUMMAS_ID,
                        I.IHF_TOTAL_AMOUNT,
                        I.IHF_ORDER_DATE,
                        I.IHF_INVOICE_NUMBER,
                        YEAR(I.IHF_DELIVERY_DATE) AS InvoiceYear
                    FROM CUSTOMER_MASTER C
                        INNER JOIN PRICE_CODES P ON C.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                        LEFT JOIN INVOICE_HEADER I ON C.CUM_CUSTOMER_NUMBER = I.IHF_CUSTOMER_NUMBER
                        LEFT JOIN SALES_HEADER S ON I.IHF_ORDER_NUMBER = S.SOH_NUMBER
                    WHERE S.SOH_CANCELED_DATE IS NULL
                        {dateFilter}
                        AND C.CUM_CUSTOMER_NUMBER NOT IN (
                            SELECT CAST(IM.INS_INSTALLER_NUMBER AS NVARCHAR(50))
                            FROM INSTALLER_MASTER IM
                            WHERE ISNUMERIC(IM.INS_INSTALLER_NUMBER) = 1
                        )
                ),
                ARData AS (
                    SELECT 
                        P.IPC_PRICE_CODE,
                        SUM(A.ARO_INVOICE_BALANCE_DUE) AS TotalARDue
                    FROM CUSTOMER_MASTER C
                        INNER JOIN PRICE_CODES P ON C.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                        LEFT JOIN AR_OPEN_ITEM A ON C.CUM_CUSTOMER_NUMBER = A.ARO_CUSTOMER_NUMBER
                    WHERE A.ARO_DATE_PAID_IN_FULL IS NULL
                    GROUP BY P.IPC_PRICE_CODE
                )
                SELECT 
                    I.IPC_PRICE_CODE AS PriceCode,
                    I.IPC_DESCRIPTION AS Name,
                    COUNT(DISTINCT I.CUM_CUMMAS_ID) AS PropertyCount,
                    COUNT(DISTINCT I.IHF_INVOICE_NUMBER) AS InvoiceCount,
                    
                    ISNULL(SUM(I.IHF_TOTAL_AMOUNT), 0) AS TotalRevenue,
                    ISNULL(AR.TotalARDue, 0) AS AmountDue,
                    COUNT(DISTINCT CASE WHEN I.InvoiceYear = YEAR(GETDATE()) THEN I.IHF_INVOICE_NUMBER ELSE NULL END) AS ThisYearInvoiceCount,
                    ISNULL(SUM(CASE WHEN I.InvoiceYear = YEAR(GETDATE()) THEN I.IHF_TOTAL_AMOUNT ELSE 0 END), 0) AS ThisYearRevenue,
                    COUNT(DISTINCT CASE WHEN I.InvoiceYear = YEAR(GETDATE())-1 THEN I.IHF_INVOICE_NUMBER ELSE NULL END) AS LastYearInvoiceCount,
                    ISNULL(SUM(CASE WHEN I.InvoiceYear = YEAR(GETDATE()) - 1 THEN I.IHF_TOTAL_AMOUNT ELSE 0 END), 0) AS LastYearRevenue
                FROM InvoiceData I
                    LEFT JOIN ARData AR ON I.IPC_PRICE_CODE = AR.IPC_PRICE_CODE
                GROUP BY I.IPC_PRICE_CODE, I.IPC_DESCRIPTION, AR.TotalARDue
                HAVING COUNT(DISTINCT I.CUM_CUMMAS_ID) > 0
                ORDER BY I.IPC_DESCRIPTION
            ";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(sql, conn);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        managementList.Add(new ManagementCompanyListItem
                        {
                            PriceCode = reader.GetInt32("PriceCode"),
                            Name = reader.GetString("Name"),
                            PropertyCount = reader.GetInt32("PropertyCount"),
                            InvoiceCount = reader.GetInt32("InvoiceCount"),
                            TotalRevenue = Convert.ToDecimal(reader.GetDouble("TotalRevenue")),
                            AmountDue = Convert.ToDecimal(reader.GetDouble("AmountDue")),
                            ThisYearInvoiceCount = reader.GetInt32("ThisYearInvoiceCount"),
                            LastYearInvoiceCount = reader.GetInt32("LastYearInvoiceCount"),
                            ThisYearRevenue = Convert.ToDecimal(reader.GetDouble("ThisYearRevenue")),
                            LastYearRevenue = Convert.ToDecimal(reader.GetDouble("LastYearRevenue"))
                        });
                    }
                }
            }

            // Calculate rankings based on This Year Revenue
            // WHY: Rankings help management quickly identify top and bottom performers
            // Sort by revenue descending and assign rank numbers
            var rankedList = managementList
                .OrderByDescending(m => m.ThisYearRevenue)
                .Select((company, index) => {
                    company.Rank = index + 1;  // Rank 1 is the highest revenue
                    return company;
                })
                .ToList();

            // Create the dashboard ViewModel with aggregate KPIs
            // WHY: ViewModel pattern separates data aggregation from view logic
            // The calculated properties in the ViewModel provide branch-level insights
            var dashboardViewModel = new ManagementDashboardViewModel
            {
                Companies = rankedList
            };

            return View(dashboardViewModel);
        }

        // GET: Management/ManagementDetails/{id}
        // This displays detailed information for a specific management company
        // The 'id' parameter is the Price Code (IPC_PRICE_CODE)
        // viewMode: "ytd" for year-to-date comparison, "fullyear" for full calendar year comparison
        public IActionResult ManagementDetails(int id, string viewMode = "ytd")
        {
            ViewBag.ViewMode = viewMode; // Pass to view for toggle state
            var officeLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LA";
            var connectionString = _configuration.GetConnectionString(officeLocation);

            var vm = new ManagementDetailsViewModel
            {
                PriceCode = id,
                Properties = new List<ManagementPropertyItem>(),
                MonthlyInvoices = new List<MonthlyInvoiceSummary>()
            };

            // Define date filter based on view mode
            var detailsDateFilter = viewMode == "fullyear"
                ? @"AND (
                        -- Current year: Full year to date
                        YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE())
                        OR
                        -- Last year: Complete calendar year
                        YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE()) - 1
                    )"
                : @"AND (
                        -- Current year: From Jan 1 to today (YTD)
                        (YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE())
                            AND I.IHF_DELIVERY_DATE <= GETDATE())
                        OR
                        -- Last year: From Jan 1 to same date last year (YTD comparison)
                        (YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE()) - 1
                            AND I.IHF_DELIVERY_DATE <= DATEADD(YEAR, -1, GETDATE()))
                    )";

            // Define year filter conditions for CASE statements
            var thisYearCondition = viewMode == "fullyear"
                ? "YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE())"
                : "YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE()) AND I.IHF_DELIVERY_DATE <= GETDATE()";

            var lastYearCondition = viewMode == "fullyear"
                ? "YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE()) - 1"
                : "YEAR(I.IHF_DELIVERY_DATE) = YEAR(GETDATE()) - 1 AND I.IHF_DELIVERY_DATE <= DATEADD(YEAR, -1, GETDATE())";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // First, get the management company basic info
                // This query gets the name and aggregated totals for the KPI cards
                using (var cmd = new SqlCommand($@"
                    SELECT
                        P.IPC_DESCRIPTION AS Name,
                        COUNT(DISTINCT C.CUM_CUMMAS_ID) AS PropertyCount,
                        COUNT(DISTINCT I.IHF_INVOICE_NUMBER) AS InvoiceCount,
                        ISNULL(SUM(I.IHF_TOTAL_AMOUNT), 0) AS TotalRevenue,
                        ISNULL(SUM(CASE
                            WHEN {thisYearCondition}
                            THEN I.IHF_TOTAL_AMOUNT ELSE 0 END), 0) AS ThisYearRevenue,
                        ISNULL(SUM(CASE
                            WHEN {lastYearCondition}
                            THEN I.IHF_TOTAL_AMOUNT ELSE 0 END), 0) AS LastYearRevenue,
                        ISNULL((
                            SELECT SUM(ARO_INVOICE_BALANCE_DUE)
                            FROM AR_OPEN_ITEM A
                            INNER JOIN CUSTOMER_MASTER CM ON A.ARO_CUSTOMER_NUMBER = CM.CUM_CUSTOMER_NUMBER
                            WHERE CM.CUM_PRICE_CODE = @PriceCode
                                AND A.ARO_DATE_PAID_IN_FULL IS NULL
                        ), 0) AS AmountDue
                    FROM PRICE_CODES P
                        LEFT JOIN CUSTOMER_MASTER C ON P.IPC_PRICE_CODE = C.CUM_PRICE_CODE
                        LEFT JOIN INVOICE_HEADER I ON C.CUM_CUSTOMER_NUMBER = I.IHF_CUSTOMER_NUMBER
                        LEFT JOIN SALES_HEADER S ON I.IHF_ORDER_NUMBER = S.SOH_NUMBER
                    WHERE P.IPC_PRICE_CODE = @PriceCode
                        AND (S.SOH_CANCELED_DATE IS NULL OR S.SOH_CANCELED_DATE IS NULL)
                        {detailsDateFilter}
                        AND C.CUM_CUSTOMER_NUMBER NOT IN (
                            SELECT CAST(IM.INS_INSTALLER_NUMBER AS NVARCHAR(50))
                            FROM INSTALLER_MASTER IM
                            WHERE ISNUMERIC(IM.INS_INSTALLER_NUMBER) = 1
                        )
                    GROUP BY P.IPC_DESCRIPTION
                ", conn))
                {
                    cmd.Parameters.AddWithValue("@PriceCode", id);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        vm.Name = reader.GetString("Name");
                        vm.PropertyCount = reader.GetInt32("PropertyCount");
                        vm.InvoiceCount = reader.GetInt32("InvoiceCount");
                        vm.TotalRevenue = Convert.ToDecimal(reader.GetDouble("TotalRevenue"));
                        vm.ThisYearRevenue = Convert.ToDecimal(reader.GetDouble("ThisYearRevenue"));
                        vm.LastYearRevenue = Convert.ToDecimal(reader.GetDouble("LastYearRevenue"));
                        vm.AmountDue = Convert.ToDecimal(reader.GetDouble("AmountDue"));
                    }
                }

                // Get monthly invoice data for the chart
                // This query groups invoices by month to create the trend chart
                // Date filtering matches the selected view mode
                using (var cmd = new SqlCommand($@"
                    SELECT
                        YEAR(I.IHF_DELIVERY_DATE) AS InvoiceYear,
                        MONTH(I.IHF_DELIVERY_DATE) AS InvoiceMonth,
                        DATENAME(MONTH, I.IHF_DELIVERY_DATE) AS MonthName,
                        SUM(I.IHF_TOTAL_AMOUNT) AS InvoiceAmount
                    FROM INVOICE_HEADER I
                        INNER JOIN CUSTOMER_MASTER C ON I.IHF_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
                        INNER JOIN SALES_HEADER S ON I.IHF_ORDER_NUMBER = S.SOH_NUMBER
                    WHERE C.CUM_PRICE_CODE = @PriceCode
                        AND S.SOH_CANCELED_DATE IS NULL
                        {detailsDateFilter}
                    GROUP BY YEAR(I.IHF_DELIVERY_DATE), MONTH(I.IHF_DELIVERY_DATE), DATENAME(MONTH, I.IHF_DELIVERY_DATE)
                    ORDER BY YEAR(I.IHF_DELIVERY_DATE), MONTH(I.IHF_DELIVERY_DATE)
                ", conn))
                {
                    cmd.Parameters.AddWithValue("@PriceCode", id);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        vm.MonthlyInvoices.Add(new MonthlyInvoiceSummary
                        {
                            InvoiceYear = reader.GetInt32("InvoiceYear").ToString(),
                            InvoiceMonth = reader.GetString("MonthName"),
                            InvoiceAmount = reader.GetDouble("InvoiceAmount")
                        });
                    }
                }

                // Get the properties under this management company with enhanced metrics
                // This query gets each property's performance including year-over-year comparison
                using (var cmd = new SqlCommand($@"
                    SELECT
                        C.CUM_CUMMAS_ID AS PropertyId,
                        C.CUM_CUSTOMER_NUMBER AS CustomerNumber,
                        C.CUM_CUSTOMER_NAME AS CustomerName,
                        CONCAT(ISNULL(C.CUM_ADDRESS_1, ''), ' ', ISNULL(C.CUM_ADDRESS_2, ''), ' ', ISNULL(C.CUM_ADDRESS_3, '')) AS Address,
                        C.CUM_CITY AS City,
                        S.SMN_SALESMAN_NAME AS Salesperson,
                        ISNULL(C.CUM_AR_BALANCE, 0) AS ARBalance,
                        COUNT(DISTINCT I.IHF_INVOICE_NUMBER) AS InvoiceCount,
                        ISNULL(SUM(CASE
                            WHEN {thisYearCondition}
                            THEN I.IHF_TOTAL_AMOUNT ELSE 0 END), 0) AS ThisYearRevenue,
                        ISNULL(SUM(CASE
                            WHEN {lastYearCondition}
                            THEN I.IHF_TOTAL_AMOUNT ELSE 0 END), 0) AS LastYearRevenue
                    FROM CUSTOMER_MASTER C
                        LEFT JOIN INVOICE_HEADER I ON C.CUM_CUSTOMER_NUMBER = I.IHF_CUSTOMER_NUMBER
                        LEFT JOIN SALES_HEADER SH ON I.IHF_ORDER_NUMBER = SH.SOH_NUMBER
                        LEFT JOIN SALESMAN_MASTER S ON C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID
                    WHERE C.CUM_PRICE_CODE = @PriceCode
                        AND (SH.SOH_CANCELED_DATE IS NULL OR SH.SOH_CANCELED_DATE IS NULL)
                        AND C.CUM_CUSTOMER_NUMBER NOT IN (
                            SELECT CAST(IM.INS_INSTALLER_NUMBER AS NVARCHAR(50))
                            FROM INSTALLER_MASTER IM
                            WHERE ISNUMERIC(IM.INS_INSTALLER_NUMBER) = 1
                        )
                    GROUP BY 
                        C.CUM_CUMMAS_ID, 
                        C.CUM_CUSTOMER_NUMBER, 
                        C.CUM_CUSTOMER_NAME, 
                        C.CUM_ADDRESS_1, 
                        C.CUM_ADDRESS_2, 
                        C.CUM_ADDRESS_3, 
                        C.CUM_CITY, 
                        S.SMN_SALESMAN_NAME,
                        C.CUM_AR_BALANCE
                    ORDER BY C.CUM_CUSTOMER_NAME
                ", conn))
                {
                    cmd.Parameters.AddWithValue("@PriceCode", id);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        vm.Properties.Add(new ManagementPropertyItem
                        {
                            PropertyId = reader.GetInt32("PropertyId"),
                            CustomerNumber = reader.GetString("CustomerNumber"),
                            CustomerName = reader.GetString("CustomerName"),
                            Address = reader.GetString("Address").Trim(),
                            City = reader.GetString("City"),
                            Salesperson = reader.IsDBNull(reader.GetOrdinal("Salesperson")) ? "" : reader.GetString("Salesperson"),
                            ARBalance = Convert.ToDecimal(reader.GetDouble("ARBalance")),
                            InvoiceCount = reader.GetInt32("InvoiceCount"),
                            ThisYearRevenue = Convert.ToDecimal(reader.GetDouble("ThisYearRevenue")),
                            LastYearRevenue = Convert.ToDecimal(reader.GetDouble("LastYearRevenue"))
                        });
                    }
                }
            }

            return View(vm);
        }        
    }
}
