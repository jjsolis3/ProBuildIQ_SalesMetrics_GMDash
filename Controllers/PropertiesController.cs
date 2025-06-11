using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using System.Data;
using System.Security.Claims;
using SalesMetrics.Services.Helpers;

using User = SalesMetrics.Models.User;
using EfUser = SalesMetrics.Models.EFCore.UserEntity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http.HttpResults;


namespace SalesMetrics.Controllers
{
    public class PropertiesController : Controller
    {
        private readonly IConfiguration _configuration;

        private readonly IMemoryCache _cache;

        public PropertiesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Orders()
        {
            return View();
        }

        public IActionResult Properties()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var officeLocation = HttpContext.Session.GetString("OfficeLocation");
            int? roleId = null;
            var roleStr = HttpContext.Session.GetString("RoleId");
            int? salesmanId = HttpContext.Session.GetInt32("SalesmanId");

            if (!string.IsNullOrEmpty(roleStr) && int.TryParse(roleStr, out var parsedRole))
            {
                roleId = parsedRole;
            }

            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirstValue("UserID");
                officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);

                if (string.IsNullOrEmpty(userId))
                    return RedirectToAction("Login", "Auth");
            }

            // Get assigned-to dropdown users based on session role + location
            var userList = UserHelper.GetActiveUsers(HttpContext, _configuration);

            ViewBag.Users = userList;
            ViewBag.UserId = userId;
            ViewBag.RoleID = roleId;
            ViewBag.SalesmanID = salesmanId;

            var connectionString = _configuration.GetConnectionString(officeLocation);
            var properties = new List<CustomerPropertyViewModel>();

            var baseSql = @"
                SELECT 
	                C.[CUM_CUMMAS_ID],
	                C.[CUM_CUSTOMER_NUMBER],
                    C.[CUM_CUSTOMER_NAME],
                    ISNULL(C.[CUM_ADDRESS_1], '') + ISNULL(C.[CUM_ADDRESS_2], '') + ISNULL(C.[CUM_ADDRESS_3], '') AS [ADDRESS],
                    C.[CUM_CITY],
                    C.[CUM_STATE],
                    C.[CUM_ZIP],
                    C.[CUM_PHONE_NUMBER],
                    C.[CUM_EMAIL],
                    C.[CUM_CREDIT_LIMIT],
                    C.[CUM_ESTABLISHED_DATE],
                    C.[CUM_AR_BALANCE],
                    C.[CUM_PRICE_CODE],
                    C.[CUM_ATTENTION_TO],
                    C.[CUM_SMNMAS_ID],
                    S.[SMN_SALESMAN_NAME],
                    C.[Id],
                    CAST(C.[CUM_CREDIT_HOLD_FLAG] as int) as [CUM_CREDIT_HOLD_FLAG],
                    C.[CUM_PO_NUMBER_REQUIRED],
	                P.[IPC_DESCRIPTION],
	                (SELECT MAX(SOH_WHSMAS_ID) FROM SALES_HEADER WHERE SOH_CUMMAS_ID = C.CUM_CUMMAS_ID AND SOH_WHSMAS_ID <> 1 GROUP BY SOH_CUMMAS_ID) as [SOH_WHSMAS_ID]

                FROM [CUSTOMER_MASTER] AS C
	                LEFT JOIN PRICE_CODES AS P ON C.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                    LEFT JOIN SALESMAN_MASTER as S on C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID
                WHERE C.[CUM_CUSTOMER_NUMBER] NOT IN (
	                    SELECT CAST(IM.INS_INSTALLER_NUMBER AS NVARCHAR(50))
	                    FROM [INSTALLER_MASTER] IM
	                    WHERE ISNUMERIC(IM.INS_INSTALLER_NUMBER) = 1
                    )
                ";

            if (roleId == 2)
            {
                baseSql += " AND C.CUM_SMNMAS_ID = @SalesmanId";
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {                      
                conn.Open();
                var cmd = new SqlCommand(baseSql, conn);

                if (roleId == 2)
                {
                    cmd.Parameters.AddWithValue("@SalesmanId", salesmanId);
                }
                
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        properties.Add(new CustomerPropertyViewModel
                        {
                            CustomerId = reader.GetInt32("CUM_CUMMAS_ID"),
                            CustomerNumber = reader.GetString("CUM_CUSTOMER_NUMBER"),
                            CustomerName = reader.GetString("CUM_CUSTOMER_NAME"),
                            Address = reader.GetString("ADDRESS").Trim(),
                            City = reader.GetString("CUM_CITY"),
                            State = reader.GetString("CUM_STATE"),
                            Zip = reader.GetString("CUM_ZIP"),
                            PhoneNumber = reader.GetString("CUM_PHONE_NUMBER"),
                            Email = reader.GetString("CUM_EMAIL"),
                            CreditLimit = reader.IsDBNull("CUM_CREDIT_LIMIT") ? 0 : Convert.ToDecimal(reader["CUM_CREDIT_LIMIT"]),
                            CreditHold = reader.GetInt32("CUM_CREDIT_HOLD_FLAG"),
                            EstablishedDate = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                            ARBalance = reader.IsDBNull("CUM_AR_BALANCE") ? 0 : Convert.ToDecimal(reader["CUM_AR_BALANCE"]),
                            PriceCode = reader.IsDBNull("CUM_PRICE_CODE") ? 0 : reader.GetInt32("CUM_PRICE_CODE"),
                            AttentionTo = reader.IsDBNull("CUM_ATTENTION_TO") ? "" : reader.GetString("CUM_ATTENTION_TO"),
                            SalesmanID = reader.IsDBNull("CUM_SMNMAS_ID") ? 0 : reader.GetInt32("CUM_SMNMAS_ID"),
                            Salesperson = reader.IsDBNull("SMN_SALESMAN_NAME") ? "" : reader.GetString("SMN_SALESMAN_NAME"),
                            PONumberRequired = reader.GetBoolean("CUM_PO_NUMBER_REQUIRED"),
                            MgmtCo = reader.IsDBNull("IPC_DESCRIPTION") ? "" : reader.GetString("IPC_DESCRIPTION")
                        });
                    }
                }
            }
            return View(properties);
        }

        public IActionResult PropertyDetails(string id) // id = CUM_CUSTOMER_NUMBER
        {
            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");
            string officeLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LA"; // Default fallback
            int locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            var vm = new PropertyDetailsViewModel
            {
                MonthlyInvoices = new List<MonthlyInvoiceSummary>(),
                Invoices = new List<Invoice>(),
                WorkOrders = GetWorkOrdersByCustomer(id, officeLocation, roleId, salesmanId),
            };
                        
            var connStr = _configuration.GetConnectionString(officeLocation);

            using (SqlConnection conn = new(connStr))
            {
                conn.Open();

                // 🔹 Get property info
                using (var cmd = new SqlCommand(@"
                    SELECT 
                        CUM_CUSTOMER_NUMBER as [CustomerNumber],  
                        CUM_CUSTOMER_NAME as [CustomerName], 
                        CONCAT(ISNULL(CUM_ADDRESS_1, ''), ' ', ISNULL(CUM_ADDRESS_2, ''),' ', ISNULL(CUM_ADDRESS_3, '')) as [Address],
	                    CUM_CITY as [City],
	                    CUM_STATE as [State],
	                    CUM_ZIP as [Zip], 
	                    CUM_ESTABLISHED_DATE as [EstablishedDate],
	                    CUM_CREDIT_LIMIT as [CreditLimit],
	                    CUM_PRICE_CODE [PriceCode],
	                    P.IPC_DESCRIPTION as [MgmtCo],
	                    CUM_ATTENTION_TO as [AttnTo], 
	                    CUM_SMNMAS_ID as [SalemanID],
	                    S.SMN_SALESMAN_NAME as [SalemanName],
	                    S.SMN_SALESMAN_NUMBER as [SalemanNumber],
                        CAST(C.[CUM_CREDIT_HOLD_FLAG] as int) as [CUM_CREDIT_HOLD_FLAG]

                    FROM CUSTOMER_MASTER as C
	                    JOIN PRICE_CODES as P on C.CUM_PRICE_CODE = P.IPC_PRICE_CODE
	                    LEFT JOIN SALESMAN_MASTER as S on C.CUM_SMNMAS_ID = S.SMN_SMNMAS_ID

                    WHERE C.CUM_CUMMAS_ID = @id
                    ", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        vm.PropertyId = id != null ? Convert.ToInt32(id) : 0;
                        vm.CustomerNumber = reader.GetString("CustomerNumber");
                        vm.CustomerName = reader.GetString("CustomerName");
                        vm.Address = reader.GetString("Address");
                        vm.City = reader.GetString("City");
                        vm.State = reader.GetString("State");
                        vm.Zip = reader.GetString("Zip");
                        vm.EstablishedDate = reader.IsDBNull("EstablishedDate") ? null : reader.GetDateTime("EstablishedDate");
                        vm.CreditLimit = reader.IsDBNull("CreditLimit") ? null : reader.GetDouble("CreditLimit");
                        vm.CreditHold = reader.IsDBNull("CUM_CREDIT_HOLD_FLAG") ? 0 : reader.GetInt32("CUM_CREDIT_HOLD_FLAG");
                        vm.PriceCode = reader.IsDBNull("PriceCode") ? 0 : reader.GetInt32("PriceCode");
                        vm.MgmtCo = reader.IsDBNull("MgmtCo") ? "" : reader.GetString("MgmtCo");
                        vm.AttnTo = reader.IsDBNull("AttnTo") ? "" : reader.GetString("AttnTo");
                        vm.SalesmanID = reader.IsDBNull("SalemanID") ? 0 : reader.GetInt32("SalemanID");
                        vm.SalemanName = reader.IsDBNull("SalemanName") ? "" : reader.GetString("SalemanName");
                        vm.SalemanNumber = reader.IsDBNull("SalemanNumber") ? "" : reader.GetString("SalemanNumber");
                    }
                }

                // 🔹 Get monthly invoices
                using (var cmd = new SqlCommand(@"
                    SELECT 
                        FORMAT(I.IHF_INVOICE_DATE, 'yyyy-MM') AS InvoiceMonthKey,
                        FORMAT(I.IHF_INVOICE_DATE, 'MMM') AS InvoiceMonth,
                        RIGHT(YEAR(I.IHF_INVOICE_DATE), 2) AS InvoiceYear,
                        ROUND(SUM(I.IHF_TOTAL_AMOUNT), 2) AS InvoiceAmount
                    FROM INVOICE_HEADER AS I
                        LEFT JOIN CUSTOMER_MASTER AS C ON I.IHF_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
                    WHERE I.IHF_TOTAL_AMOUNT > 0
                        AND C.CUM_CUMMAS_ID = @id
                    GROUP BY FORMAT(I.IHF_INVOICE_DATE, 'yyyy-MM'), FORMAT(I.IHF_INVOICE_DATE, 'MMM'), YEAR(I.IHF_INVOICE_DATE)
                    ORDER BY InvoiceMonthKey
                    ", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        vm.MonthlyInvoices.Add(new MonthlyInvoiceSummary
                        {
                            InvoiceMonthKey = reader.GetString(0),
                            InvoiceMonth = reader.GetString(1),
                            InvoiceYear = reader.GetString(2),
                            InvoiceAmount = reader.GetDouble(3)
                        });

                    }
                }

                // 🔹 Get individual invoices for TotalRevenue and KPI counts
                using (var cmd = new SqlCommand(@"
                    SELECT 
                        IHF_INVOICE_DATE,
                        IHF_TOTAL_AMOUNT,
                        CASE 
                            WHEN ARO_INVOICE_BALANCE_DUE = 0 THEN 1 ELSE 0
                        END AS IsPaid
                    FROM INVOICE_HEADER I
                        LEFT JOIN AR_OPEN_ITEM A ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                        LEFT JOIN CUSTOMER_MASTER C ON I.IHF_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
                    WHERE 
                        C.CUM_CUMMAS_ID = @id
                        AND I.IHF_TOTAL_AMOUNT > 0
                        --AND I.IHF_INVOICE_DATE >= DATEADD(YEAR, -1, GETDATE())
                ", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        vm.Invoices.Add(new Invoice
                        {
                            Date = reader.GetDateTime(0),
                            Amount = Convert.ToDecimal(reader[1]),
                            IsPaid = reader.GetInt32(2) == 1
                        });
                    }
                }

                // Get AR Sales Data
                using (
                    var cmd = new SqlCommand(@"
                        SELECT 
                            SUM(AR.ARO_INVOICE_AMOUNT) AS TotalSales,
                            SUM(AR.ARO_INVOICE_BALANCE_DUE) AS AmountDue,
                            COUNT(I.IHF_INVOICE_NUMBER) AS InvoiceCount,
                            SUM(CASE WHEN AR.ARO_DUE_DATE < GETDATE() AND AR.ARO_INVOICE_BALANCE_DUE > 0 THEN 1 ELSE 0 END) AS OverdueCount
                        FROM AR_OPEN_ITEM AS AR
                        LEFT JOIN INVOICE_HEADER AS I ON AR.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                        LEFT JOIN CUSTOMER_MASTER AS C ON I.IHF_CUSTOMER_NUMBER = C.CUM_CUSTOMER_NUMBER
                        WHERE C.CUM_CUMMAS_ID = @id
                    ", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        vm.ARTotalSales = reader["TotalSales"] != DBNull.Value ? Convert.ToDecimal(reader["TotalSales"]) : 0;
                        vm.ARAmountDue = reader["AmountDue"] != DBNull.Value ? Convert.ToDecimal(reader["AmountDue"]) : 0;
                        vm.ARInvoiceCount = reader["InvoiceCount"] != DBNull.Value ? Convert.ToInt32(reader["InvoiceCount"]) : 0;
                        vm.AROverdueInvoiceCount = reader["OverdueCount"] != DBNull.Value ? Convert.ToInt32(reader["OverdueCount"]) : 0;
                    }
                }

                conn.Close();
            }

            // Get property notes
            var connNotesStr = _configuration.GetConnectionString("SalesMetrics");

            using (SqlConnection conn = new(connNotesStr))
            {
                conn.Open();

                using (var notesCmd = new SqlCommand(@"
                    SELECT [NoteID]
                        ,[PropertyId]
                        ,[PropertyNumber]
                        ,[PropertyName]
                        ,[NoteText]
                        ,[NoteType]
                        ,[LocationId]
                        ,[CreatedBy]
                        ,[CreatedDate]
                        ,[ModifiedBy]
                        ,[ModifiedDate]
                        ,[IsActive] 
                    FROM PropertyNotes 
                    WHERE PropertyId = @id 
                        AND LocationId = @LocationId
                        AND IsActive = 1
                        AND NoteType = 'Property'
                    ORDER BY CreatedDate DESC
                ", conn))
                {
                    notesCmd.Parameters.AddWithValue("@id", id);
                    notesCmd.Parameters.AddWithValue("@LocationId", locationId);
                    using var reader = notesCmd.ExecuteReader();
                    while (reader.Read())
                    {
                        vm.PropertyNotes.Add(new PropertyNote
                        {
                            NoteID = reader.GetInt32("NoteID"),
                            PropertyId = reader.IsDBNull("PropertyId") ? null : reader.GetString("PropertyId"),
                            NoteText = reader.IsDBNull("NoteText") ? "" : reader.GetString("NoteText"),
                            CreatedBy = reader.IsDBNull("CreatedBy") ? "unknown" : reader.GetString("CreatedBy"),
                            CreatedDate = reader.IsDBNull("CreatedDate") ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedDate"])
                        });
                    }
                }

                conn.Close();
            }

            return View(vm);
        }

        [HttpGet]
        public JsonResult GetOrdersByProperty(string id)
        {
            // Retrieve session info similar to other methods.
            var userId = HttpContext.Session.GetString("UserId");
            var officeLocation = HttpContext.Session.GetString("OfficeLocation");
            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            var connectionString = _configuration.GetConnectionString(officeLocation);
            var workOrders = new List<WorkOrderViewModel>();

            // Adjust the query as needed. This sample query uses the property identifier (CUM_CUMMAS_ID)
            // to filter only the orders for the given property.
            var sql = @"
                SELECT   
                    SOH_NUMBER AS OrderID,
                    P.IPC_PRICE_CODE as ManagementID,
                    P.IPC_DESCRIPTION AS ManagementName,
                    CUS.CUM_CUMMAS_ID as PropertyID,
                    CUS.CUM_CUSTOMER_NUMBER as PropertyNumber,
                    CUS.CUM_CUSTOMER_NAME AS PropertyName,
                    CUS.CUM_CITY AS City,
                    SOH_SHIP_VIA AS OrderType,

                    ISNULL((
                        SELECT SUM(SDT_ORDER_QTY_ORDER)
                        FROM SALES_DETAIL
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER 
                          AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                          AND CASE 
                                WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 
                          END NOT LIKE '%Metal%'
                    ), 0.00) AS Qty,

                    B.BuildingNumber + ' - ' + A.ApartmentNumber AS UnitNumber, 
                    APT.Description AS UnitType,
                    CONVERT(VARCHAR, S.SOH_DELIVERY_DATE, 101) AS DeliveryDate,
                    CONVERT(VARCHAR, S.SOH_MOVING_DATE, 101) AS MoveInDate,
                    ISNULL(AR.ARO_DATE_PAID_IN_FULL, '') AS PaidInFullDate,

                    ISNULL((
                        SELECT TOP 1 ITM_PCLMAS_ID
                        FROM SALES_DETAIL
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER 
                          AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                          AND CASE 
                                WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 
                          END NOT LIKE '%Metal%'
                    ), '') AS ProductClass,

                    ISNULL((
                        SELECT TOP 1 SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2
                        FROM SALES_DETAIL
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER 
                          AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                          AND CASE 
                                WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 
                          END NOT LIKE '%Metal%'
                    ), '') AS ProductDescription,

                    S.SOH_ORDERED_BY AS OrderedBy, 
                    CASE 
		                WHEN AR.ARO_INVOICE_NUMBER IS NOT NULL THEN 
			                CASE 
				                WHEN AR.ARO_DATE_PAID_IN_FULL IS NOT NULL THEN 'PAID'
				                ELSE 'UNPAID' END
		                ELSE 'NOT INVOICED' END as Status,
                    WHS_WAREHOUSE_NUMBER AS Location

                FROM SALES_HEADER AS S
                    LEFT JOIN Apartments AS A ON S.SOH_APARTMENT_ID = A.Id 
                    LEFT JOIN Buildings AS B ON A.Building_id = B.Id
                    LEFT JOIN ApartmentType AS APT ON A.ApartmentType_Id = APT.ID
                    LEFT JOIN CUSTOMER_MASTER AS CUS ON CUS.CUM_CUMMAS_ID = S.SOH_CUMMAS_ID
                    LEFT JOIN PRICE_CODES AS P ON CUS.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                    LEFT JOIN WAREHOUSE_MASTER ON WHS_WAREHOUSE_NUMBER = S.SOH_WHSMAS_ID
	                LEFT JOIN AR_OPEN_ITEM as AR on S.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                WHERE 
                    S.SOH_CANCELED_DATE IS NULL
                    AND EXISTS (
                        SELECT 1 
                        FROM SALES_DETAIL 
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER
                    )
                    AND CUS.CUM_CUMMAS_ID = @id
            ";

            if (roleId == 2)
            {
                sql += " AND SOH_SMNMAS_ID = @SalesmanID";
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id); // Filter orders for this property
                if (roleId == 2)
                {
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    workOrders.Add(new WorkOrderViewModel
                    {
                        OrderID = reader["OrderID"]?.ToString(),
                        ManagementCode = Convert.ToInt32(reader["ManagementID"]),
                        ManagementName = reader["ManagementName"]?.ToString(),
                        PropertyId = Convert.ToInt32(reader["PropertyID"]),
                        PropertyNumber = Convert.ToInt32(reader["PropertyNumber"]),
                        PropertyName = reader["PropertyName"]?.ToString(),
                        City = reader["City"]?.ToString(),
                        OrderType = reader["OrderType"]?.ToString(),
                        Qty = Convert.ToDouble(reader["Qty"]),
                        UnitNumber = reader["UnitNumber"]?.ToString(),
                        UnitType = reader["UnitType"]?.ToString(),
                        DeliveryDate = reader["DeliveryDate"]?.ToString(),
                        PaidInFullDate = reader["MoveInDate"]?.ToString(),
                        ProductClass = reader["ProductClass"]?.ToString(),
                        ProductDescription = reader["ProductDescription"]?.ToString(),
                        OrderedBy = reader["OrderedBy"]?.ToString(),
                        Status = reader["Status"]?.ToString(),
                        Location = reader["Location"]?.ToString()
                    });
                }
            }
            return Json(new { data = workOrders });
        }

        private List<WorkOrderViewModel> GetWorkOrdersByCustomer(string customerId, string officeLocation, int roleId, int salesmanId)
        {
            var workOrders = new List<WorkOrderViewModel>();
            var connectionString = _configuration.GetConnectionString(officeLocation);

            var sql = @"
                SELECT   
                    SOH_NUMBER AS OrderID,
                    P.IPC_PRICE_CODE as ManagementID,
                    P.IPC_DESCRIPTION AS ManagementName,
                    CUS.CUM_CUMMAS_ID as PropertyID,
                    CUS.CUM_CUSTOMER_NUMBER as PropertyNumber,
                    CUS.CUM_CUSTOMER_NAME AS PropertyName,
                    CUS.CUM_CITY AS City,
                    SOH_SHIP_VIA AS OrderType,

                    ISNULL((
                        SELECT SUM(SDT_ORDER_QTY_ORDER)
                        FROM SALES_DETAIL
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER 
                          AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                          AND CASE 
                                WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 
                          END NOT LIKE '%Metal%'
                    ), 0.00) AS Qty,

                    B.BuildingNumber + ' - ' + A.ApartmentNumber AS UnitNumber, 
                    APT.Description AS UnitType,
                    CONVERT(VARCHAR, S.SOH_DELIVERY_DATE, 101) AS DeliveryDate,
                    CONVERT(VARCHAR, S.SOH_MOVING_DATE, 101) AS MoveInDate,
                    ISNULL(AR.ARO_DATE_PAID_IN_FULL, '') AS PaidInFullDate,

                    ISNULL((
                        SELECT TOP 1 ITM_PCLMAS_ID
                        FROM SALES_DETAIL
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER 
                          AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                          AND CASE 
                                WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 
                          END NOT LIKE '%Metal%'
                    ), '') AS ProductClass,

                    ISNULL((
                        SELECT TOP 1 SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2
                        FROM SALES_DETAIL
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER 
                          AND ITM_PCLMAS_ID IN ('CARPET', 'VINYL', 'TILE', 'WOOD', 'VINYLPLANK')
                          AND CASE 
                                WHEN ITM_PRIVATE_LABEL = '' THEN ITM_ID_FIRST + '-' + ITM_ID_LAST 
                                ELSE ITM_PRIVATE_LABEL + ' ' + ITM_PRIVATE_LABEL_2 
                          END NOT LIKE '%Metal%'
                    ), '') AS ProductDescription,

                    S.SOH_ORDERED_BY AS OrderedBy, 
                    CASE 
		                WHEN AR.ARO_INVOICE_NUMBER IS NOT NULL THEN 
			                CASE 
				                WHEN AR.ARO_DATE_PAID_IN_FULL IS NOT NULL THEN 'PAID'
				                ELSE 'UNPAID'
				                END
		                ELSE 'NOT INVOICED'
		                END as Status,
                    WHS_WAREHOUSE_NUMBER AS Location

                FROM SALES_HEADER AS S
                    LEFT JOIN Apartments AS A ON S.SOH_APARTMENT_ID = A.Id 
                    LEFT JOIN Buildings AS B ON A.Building_id = B.Id
                    LEFT JOIN ApartmentType AS APT ON A.ApartmentType_Id = APT.ID
                    LEFT JOIN CUSTOMER_MASTER AS CUS ON CUS.CUM_CUMMAS_ID = S.SOH_CUMMAS_ID
                    LEFT JOIN PRICE_CODES AS P ON CUS.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                    LEFT JOIN WAREHOUSE_MASTER ON WHS_WAREHOUSE_NUMBER = S.SOH_WHSMAS_ID
                    LEFT JOIN AR_OPEN_ITEM as AR on S.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                WHERE 
                    S.SOH_CANCELED_DATE IS NULL
                    AND EXISTS (
                        SELECT 1 
                        FROM SALES_DETAIL 
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER
                    )
                    AND CUS.CUM_CUMMAS_ID = @id
            ";

            //if (roleId == 2)
            //{
            //    sql += " AND SOH_SMNMAS_ID = @SalesmanID";
            //}

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", customerId);

                //if (roleId == 2)
                //    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    workOrders.Add(new WorkOrderViewModel
                    {
                        OrderID = reader["OrderID"]?.ToString(),
                        ManagementCode = Convert.ToInt32(reader["ManagementID"]),
                        ManagementName = reader["ManagementName"]?.ToString(),
                        PropertyId = Convert.ToInt32(reader["PropertyID"]),
                        PropertyNumber = Convert.ToInt32(reader["PropertyNumber"]),
                        PropertyName = reader["PropertyName"]?.ToString(),
                        City = reader["City"]?.ToString(),
                        OrderType = reader["OrderType"]?.ToString(),
                        Qty = Convert.ToDouble(reader["Qty"]),
                        UnitNumber = reader["UnitNumber"]?.ToString(),
                        UnitType = reader["UnitType"]?.ToString(),
                        DeliveryDate = reader["DeliveryDate"]?.ToString(),
                        PaidInFullDate = reader["PaidInFullDate"]?.ToString(),
                        ProductClass = reader["ProductClass"]?.ToString(),
                        ProductDescription = reader["ProductDescription"]?.ToString(),
                        OrderedBy = reader["OrderedBy"]?.ToString(),
                        Status = reader["Status"]?.ToString(),
                        Location = reader["Location"]?.ToString()
                    });
                }
            }

            return workOrders;
        }

        [HttpPost]
        public IActionResult AddPropertyNote(string PropertyId, string PropertyNumber, string PropertyName, string NoteText)
        {
            var user = HttpContext.Session.GetString("Username") ?? "unknown";
            var connection = _configuration.GetConnectionString("SalesMetrics");
            int locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            if (string.IsNullOrEmpty(PropertyId) || string.IsNullOrEmpty(NoteText))
            {
                return BadRequest("Property ID and Note Text are required.");
            }

            try
            {
                using var conn = new SqlConnection(connection);
                conn.Open();

                var cmd = new SqlCommand(@"
                INSERT INTO PropertyNotes (PropertyId, PropertyNumber, PropertyName,  NoteText, NoteType, LocationId, CreatedBy, CreatedDate )
                VALUES (@PropertyId, @PropertyNumber, @PropertyName, @NoteText, @NoteType, @LocationId, @CreatedBy, @CreatedDate)
            ", conn);

                cmd.Parameters.AddWithValue("@PropertyId", PropertyId);
                cmd.Parameters.AddWithValue("@PropertyNumber", PropertyNumber);
                cmd.Parameters.AddWithValue("@PropertyName", PropertyName);
                cmd.Parameters.AddWithValue("@NoteText", NoteText);
                cmd.Parameters.AddWithValue("@NoteType", "Property");
                cmd.Parameters.AddWithValue("@LocationId", locationId);
                cmd.Parameters.AddWithValue("@CreatedBy", user);
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);

                cmd.ExecuteNonQuery();
            }   
            catch (Exception ex)
            {
                // Log the exception (not implemented here)
                return BadRequest("An error occurred while adding the note: " + ex.Message);
            }
            

            return RedirectToAction("PropertyDetails", new { id = PropertyId });
        }


    }
}
