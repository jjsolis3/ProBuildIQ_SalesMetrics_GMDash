using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models.EFCore;
using System.Data;
using System.Security.Claims;
using SalesMetrics.Services.Helpers;

namespace SalesMetrics.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IConfiguration _configuration;

        public OrdersController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Orders()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");

            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            var officeLocation = LocationHelper.GetConnectionName(locationId) ?? "LAX"; // Default to LAX if not found
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirstValue("UserID");
                officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);

                if (string.IsNullOrEmpty(userId))
                    return RedirectToAction("Login", "Auth");
            }

            var connectionString = _configuration.GetConnectionString(officeLocation);
            var workOrders = new List<WorkOrderViewModel>();
            var fromDate = DateTime.Today.AddDays(-4);

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

                    CASE
		                WHEN S.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NOT NULL THEN AR.ARO_INVOICE_AMOUNT
		                WHEN S.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NULL THEN 0
		                ELSE S.SOH_TOTAL_AMOUNT
		                END as OrderTotal,

                    B.BuildingNumber + ' - ' + A.ApartmentNumber AS UnitNumber, 
                    APT.Description AS UnitType,
                    CONVERT(VARCHAR, S.SOH_ORDER_DATE, 101) AS OrderDate,
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
                    S.SOH_SMNMAS_ID AS SalespersonId,
	                SMN.SMN_SALESMAN_NUMBER as SalespersonNumber,
	                SMN.SMN_SALESMAN_NAME as Salesperson,
                    CASE 
		                WHEN AR.ARO_INVOICE_NUMBER IS NOT NULL THEN 
			                CASE 
				                WHEN AR.ARO_DATE_PAID_IN_FULL IS NOT NULL THEN 'PAID'
				                ELSE 'UNPAID'
				                END
		                ELSE 'NOT INVOICED'
		                END as Status,
                    WHS_WAREHOUSE_NUMBER AS Warehouse

                FROM SALES_HEADER AS S
                    LEFT JOIN Apartments AS A ON S.SOH_APARTMENT_ID = A.Id 
                    LEFT JOIN Buildings AS B ON A.Building_id = B.Id
                    LEFT JOIN ApartmentType AS APT ON A.ApartmentType_Id = APT.ID
                    LEFT JOIN CUSTOMER_MASTER AS CUS ON CUS.CUM_CUMMAS_ID = S.SOH_CUMMAS_ID
                    LEFT JOIN PRICE_CODES AS P ON CUS.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                    LEFT JOIN WAREHOUSE_MASTER ON WHS_WAREHOUSE_NUMBER = SOH_WHSMAS_ID
                    LEFT JOIN AR_OPEN_ITEM as AR on S.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                    LEFT JOIN SALESMAN_MASTER as SMN on S.SOH_SMNMAS_ID = SMN.SMN_SMNMAS_ID
                WHERE 
                    S.SOH_DELIVERY_DATE = @FromDate
                    AND S.SOH_CURRENT_STATUS <> 4
                    AND S.SOH_CANCELED_DATE IS NULL
                    AND EXISTS (
                        SELECT 1 
                        FROM SALES_DETAIL 
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER
                    )
            ";

            if (roleId == 2 && salesmanId != 0) // Salesman
            {
                sql += " AND SOH_SMNMAS_ID = @SalesmanID";
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FromDate", fromDate);
                if (roleId == 2 && salesmanId != 0)
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

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
                        OrderTotal = Convert.ToDecimal(reader["OrderTotal"]),
                        UnitNumber = reader["UnitNumber"]?.ToString(),
                        UnitType = reader["UnitType"]?.ToString(),
                        OrderDate = reader["OrderDate"]?.ToString(),
                        DeliveryDate = reader["DeliveryDate"]?.ToString(),
                        MoveInDate = reader["MoveInDate"]?.ToString(),
                        PaidInFullDate = reader["PaidInFullDate"]?.ToString(),
                        ProductClass = reader["ProductClass"]?.ToString(),
                        ProductDescription = reader["ProductDescription"]?.ToString(),
                        OrderedBy = reader["OrderedBy"]?.ToString(),
                        SalesmanId = Convert.ToInt32(reader["SalespersonId"]),
                        SalesmanName = reader["Salesperson"].ToString(),
                        Status = reader["Status"]?.ToString(),
                        Warehouse = Convert.ToInt32(reader["Warehouse"]),
                        Location = locationId.ToString()
                    });
                }
            }

            return View(workOrders);
        }

        [HttpGet]
        public JsonResult GetWorkOrdersByDate(DateTime? date)
        {
            var userId = HttpContext.Session.GetString("UserId");
            var users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");

            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            var officeLocation = LocationHelper.GetConnectionName(locationId) ?? "LAX"; // Default to LAX if not found
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            var connectionString = _configuration.GetConnectionString(officeLocation);
            var workOrders = new List<WorkOrderViewModel>();
            var fromDate = date ?? DateTime.Today;

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

                    CASE
		                WHEN S.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NOT NULL THEN AR.ARO_INVOICE_AMOUNT
		                WHEN S.SOH_TOTAL_AMOUNT = 0 AND AR.ARO_INVOICE_AMOUNT IS NULL THEN 0
		                ELSE S.SOH_TOTAL_AMOUNT
		                END as OrderTotal,

                    B.BuildingNumber + ' - ' + A.ApartmentNumber AS UnitNumber, 
                    APT.Description AS UnitType,
                    CONVERT(VARCHAR, S.SOH_ORDER_DATE, 101) AS OrderDate,
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
                    S.SOH_SMNMAS_ID AS SalespersonId,
	                SMN.SMN_SALESMAN_NUMBER as SalespersonNumber,
	                SMN.SMN_SALESMAN_NAME as Salesperson,
                    CASE 
		                WHEN AR.ARO_INVOICE_NUMBER IS NOT NULL THEN 
			                CASE 
				                WHEN AR.ARO_DATE_PAID_IN_FULL IS NOT NULL THEN 'PAID'
				                ELSE 'UNPAID'
				                END
		                ELSE 'NOT INVOICED'
		                END as Status,
                    WHS_WAREHOUSE_NUMBER AS Warehouse

                FROM SALES_HEADER AS S
                    LEFT JOIN Apartments AS A ON S.SOH_APARTMENT_ID = A.Id 
                    LEFT JOIN Buildings AS B ON A.Building_id = B.Id
                    LEFT JOIN ApartmentType AS APT ON A.ApartmentType_Id = APT.ID
                    LEFT JOIN CUSTOMER_MASTER AS CUS ON CUS.CUM_CUMMAS_ID = S.SOH_CUMMAS_ID
                    LEFT JOIN PRICE_CODES AS P ON CUS.CUM_PRICE_CODE = P.IPC_PRICE_CODE
                    LEFT JOIN WAREHOUSE_MASTER ON WHS_WAREHOUSE_NUMBER = SOH_WHSMAS_ID
                    LEFT JOIN AR_OPEN_ITEM as AR on S.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                    LEFT JOIN SALESMAN_MASTER as SMN on S.SOH_SMNMAS_ID = SMN.SMN_SMNMAS_ID
                WHERE 
                    S.SOH_DELIVERY_DATE = @FromDate
                    AND S.SOH_CURRENT_STATUS <> 4
                    AND S.SOH_CANCELED_DATE IS NULL
                    AND EXISTS (
                        SELECT 1 
                        FROM SALES_DETAIL 
                            LEFT JOIN ITEM_MASTER ON ITM_ID_FIRST = SDT_ITEM_FIRST AND ITM_ID_LAST = SDT_ITEM_LAST
                        WHERE SDT_SALOHD_ID = SOH_NUMBER
                    )
            ";

            if (roleId == 2 && salesmanId != 0)
                sql += " AND SOH_SMNMAS_ID = @SalesmanID";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FromDate", fromDate);
                if (roleId == 2 && salesmanId != 0)
                    cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

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
                        OrderTotal = Convert.ToDecimal(reader["OrderTotal"]),
                        UnitNumber = reader["UnitNumber"]?.ToString(),
                        UnitType = reader["UnitType"]?.ToString(),
                        OrderDate = reader["OrderDate"]?.ToString(),
                        DeliveryDate = reader["DeliveryDate"]?.ToString(),
                        MoveInDate = reader["MoveInDate"]?.ToString(),
                        PaidInFullDate = reader["MoveInDate"]?.ToString(),
                        ProductClass = reader["ProductClass"]?.ToString(),
                        ProductDescription = reader["ProductDescription"]?.ToString(),
                        OrderedBy = reader["OrderedBy"]?.ToString(),
                        SalesmanId = Convert.ToInt32(reader["SalespersonId"]),
                        SalesmanName = reader["Salesperson"].ToString(),
                        Status = reader["Status"]?.ToString(),
                        Warehouse = Convert.ToInt32(reader["Warehouse"]),
                        Location = locationId.ToString()
                    });
                }
            }

            return Json(new { data = workOrders });
        }

        [HttpGet]
        public IActionResult WorkOrderDetailsPartial(string orderId)
        {
            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);
            
            var viewModel = new WorkOrderDetailViewModel();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    -- ===============================
                    -- 1. ORDER LINE ITEMS (Product + Stewardship)
                    -- ===============================
	                SELECT 
	                    CASE 
		                    WHEN SDT_LINE_TYPE = 3 THEN 'SY' 
		                    ELSE SDT_ITEM_FIRST
		                    END as PRODUCT_STYLE,
	                    CASE 
		                    WHEN SDT_LINE_TYPE = 3 THEN 'SY' 
		                    ELSE SDT_ITEM_LAST
		                    END as PRODUCT_COLOR,
                        SDT_ORDER_QTY_ORDER as [ORDER_QTY],
                        CASE 
		                    WHEN SDT_LINE_TYPE = 3 THEN 'SY' 
		                    ELSE SDT_ORDER_UOM 
		                    END AS [ORDER_UOM],
                        CASE 
		                    WHEN SDT_LINE_TYPE = 3 THEN 'CARPET STEWARDSHIP' 
		                    ELSE SDT_PRODUCT_CLASS_CODE 
		                    END AS [PRODUCT_CLASS],
                        CASE 
		                    WHEN SDT_LINE_TYPE = 3 THEN 
			                    CASE 
				                    WHEN SDT_ORDER_QTY_ORDER <> 0 THEN 'Carpet Stewardship Fee at $' + TRY_CAST((SDT_EXTENDED_PRICE/SDT_ORDER_QTY_ORDER)  as nvarchar)
				                    ELSE 'Carpet Stewardship Fee'
				                    END
		                    ELSE LTRIM(RTRIM(SDT_DESCRIPTION + ' ' + SDT_DESCRIPTION_2)) 
		                    END AS [PRODUCT_DESC],
                        SDT_EXTENDED_PRICE as [TOTAL_PRICE],
                        SDT_LINE_TYPE                   
                    FROM SALES_DETAIL
                    WHERE SDT_LINE_TYPE IN (1, 3)
                        AND SDT_SALOHD_ID = @SalesOrderNumber;

                    -- ===============================
                    -- 2. SALES HEADER (Invoice Aging + Address Handling)
                    -- ===============================
                    SELECT 
	                    S.SOH_SHIP_TO_NAME as [CUSTOMER_NAME],
                        TRY_CAST(S.SOH_CUSTOMER_NUMBER AS INT) AS CUSTOMERNUMBER,
                        S.SOH_NUMBER as [ORDER_NUMBER],
                        ISNULL(A.ARO_INVOICE_NUMBER, 0) as INVOICE_NUMBER,
	
                        S.SOH_ORDER_DATE as [ORDER_DATE],
                        S.SOH_DELIVERY_DATE as [DELIVERY_DATE],
                        A.ARO_DATE_PAID_IN_FULL as [DATE_PAID_IN_FULL],
                        ISNULL(A.ARO_INVOICE_BALANCE_DUE, S.SOH_BALANCE_DUE) as [BALANCE_DUE],
	                    CASE 
                            WHEN A.ARO_INVOICE_NUMBER IS NOT NULL THEN
                                CASE 
                                    WHEN DATEDIFF(DAY, A.ARO_DUE_DATE, GETDATE()) < 30 THEN 'Invoiced Under 30 Days'
                                    WHEN DATEDIFF(DAY, A.ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59 THEN 'Invoiced 30 to 60 Days'
                                    WHEN DATEDIFF(DAY, A.ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89 THEN 'Invoiced 60 to 90 Days'
                                    WHEN DATEDIFF(DAY, A.ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 119 THEN 'Invoiced 90 to 120 Days'
                                    WHEN DATEDIFF(DAY, A.ARO_DUE_DATE, GETDATE()) >= 120 THEN 'Invoiced Over 120 Days'
                                    ELSE 'Invoiced Aging Unknown'
                                END 
                            ELSE
                                CASE 
                                    WHEN DATEDIFF(DAY, S.SOH_DELIVERY_DATE, GETDATE()) < 30 THEN 'Installed Under 30 Days'
                                    WHEN DATEDIFF(DAY, S.SOH_DELIVERY_DATE, GETDATE()) BETWEEN 30 AND 59 THEN 'Installed 30 to 60 Days'
                                    WHEN DATEDIFF(DAY, S.SOH_DELIVERY_DATE, GETDATE()) BETWEEN 60 AND 89 THEN 'Installed 60 to 90 Days'
                                    WHEN DATEDIFF(DAY, S.SOH_DELIVERY_DATE, GETDATE()) BETWEEN 90 AND 119 THEN 'Installed 90 to 120 Days'
                                    WHEN DATEDIFF(DAY, S.SOH_DELIVERY_DATE, GETDATE()) >= 120 THEN 'Installed Over 120 Days'
                                    ELSE 'Installed Aging Unknown'
                                END 
                        END AS INVOICE_AGING,
	                    S.SOH_CUSTOMER_PO as [CUSTOMER_PO],
	                    CONCAT(' ',
                            NULLIF(S.SOH_SHIP_TO_ADDRESS_1, ''),
                            NULLIF(S.SOH_SHIP_TO_ADDRESS_2, ''),
                            NULLIF(S.SOH_SHIP_TO_ADDRESS_3, '')
		                    ) AS [ADDRESS],
                        S.SOH_SHIP_TO_CITY as [CITY],
	                    S.SOH_SHIP_TO_STATE as [STATE],
	                    S.SOH_SHIP_TO_ZIP as [ZIP],
	                    B.BuildingNumber as [BLDG],
	                    Apt.ApartmentNumber as [APT_#],
	                    SM.SMN_SALESMAN_NAME as [SALES_PERSON],
	                    P.IPC_PRICE_CODE as [PRICE_CODE],
	                    P.IPC_DESCRIPTION as [PRICE_CODE_DESC],
	                    S.SOH_ORDERED_BY as [ORDERED_BY],
	                    S.SOH_WHSMAS_ID as [WHS_ID]
	
                    FROM SALES_HEADER S
	                    LEFT JOIN AR_OPEN_ITEM as A on S.SOH_NUMBER = A.ARO_SALES_ORDER_NUMBER
	                    LEFT JOIN PRICE_CODES as P on S.SOH_PRICE_CODE = P.IPC_PRICE_CODE
	                    LEFT JOIN SALESMAN_MASTER as SM on S.SOH_SMNMAS_ID = SM.SMN_SMNMAS_ID
	                    LEFT JOIN Apartments as Apt on S.SOH_APARTMENT_ID = Apt.Id
	                    LEFT JOIN Buildings as B on Apt.Building_id = B.Id

                    WHERE S.SOH_NUMBER = @SalesOrderNumber;

                    -- ===============================
                    -- 3. ORDER NOTES
                    -- ===============================

                    SELECT
                        SDT_LINE_NUMBER,
                        SDT_COMMENTS,
                        SDT_LINE_TYPE
                    FROM SALES_DETAIL
                    WHERE SDT_LINE_TYPE NOT IN (1, 3)
                        AND ISNULL(SDT_ORDER_QTY_ORDER, 0) = 0
                        AND SDT_SALOHD_ID = @SalesOrderNumber
                    ORDER BY SDT_LINE_NUMBER;
                ", conn);

                cmd.Parameters.AddWithValue("@SalesOrderNumber", orderId);

                using (var reader = cmd.ExecuteReader())
                {
                    // Line Items
                    while (reader.Read())
                    {
                        viewModel.LineItems.Add(new OrderLineItem
                        {
                            Prod_Style = reader.GetString("PRODUCT_STYLE"),
                            Prod_Color = reader.GetString("PRODUCT_COLOR"),
                            Quantity = Convert.ToDecimal(reader[2]),
                            UOM = reader.GetString("ORDER_UOM"),
                            ProdClass = reader.GetString("PRODUCT_CLASS"),
                            Description = reader.GetString("PRODUCT_DESC"),
                            ProductTotal = Convert.ToDecimal(reader[6])
                        });
                    }
                    // Move to 2nd result set (header)
                    if (reader.NextResult() && reader.Read())
                    {
                        viewModel.Header = new SalesOrderHeader
                        {
                            CustomerName = reader["CUSTOMER_NAME"] as string ?? "",
                            CustomerNumber = reader["CUSTOMERNUMBER"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CUSTOMERNUMBER"]),
                            OrderNumber = reader["ORDER_NUMBER"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ORDER_NUMBER"]),
                            InvoiceNumber = reader["INVOICE_NUMBER"] == DBNull.Value ? 0 : Convert.ToInt32(reader["INVOICE_NUMBER"]),
                            OrderDate = reader["ORDER_DATE"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["ORDER_DATE"]),
                            DeliveryDate = reader["DELIVERY_DATE"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["DELIVERY_DATE"]),
                            PaidInFullDate = reader["DATE_PAID_IN_FULL"] as string ?? "",
                            BalanceAmount = reader["BALANCE_DUE"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["BALANCE_DUE"]),
                            OrderAging = reader["INVOICE_AGING"] as string ?? "",
                            CustomerPO = reader["CUSTOMER_PO"] as string ?? "",
                            ShipAddress = reader["ADDRESS"] as string ?? "",
                            ShipState = reader["STATE"] as string ?? "",
                            ShipZip = reader["ZIP"] as string ?? "",
                            Bldg = reader["BLDG"] as string ?? "",
                            AptNumber = reader["APT_#"] as string ?? "",
                            Salesperson = reader["SALES_PERSON"] as string ?? "",
                            PriceCode = reader["PRICE_CODE"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PRICE_CODE"]),
                            MgmtCo = reader["PRICE_CODE_DESC"] as string ?? "",
                            OrderedBy = reader["ORDERED_BY"] as string ?? "",
                            WhsId = reader["WHS_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["WHS_ID"])
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

            // You can query SALES_DETAIL and CUSTOMER_MASTER to populate it
            return PartialView("_WorkOrderDetailsPartial", viewModel);
        }

    }
}
