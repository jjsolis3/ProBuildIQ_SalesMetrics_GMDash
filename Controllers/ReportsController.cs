using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Linq;
using SalesMetrics.Models;
using SalesMetrics.Models.Reports;
using SalesMetrics.Services.Helpers;
using SalesMetrics.Services.Reports;
using SalesMetrics.Services.Erp;
using SalesMetrics.Services.Erp.Configuration;

namespace SalesMetrics.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IReportCatalog _catalog;
        private readonly IReportRunner _runner;
        private readonly IReportAuthorizationService _authorizationService;
        private readonly IReportExportService _exportService;
        private readonly ILogger<ReportsController> _logger;
        private readonly ErpClientFactory _erpFactory;
        private readonly IConfiguration _configuration;

        public ReportsController(
            IReportCatalog catalog,
            IReportRunner runner,
            IReportAuthorizationService authorizationService,
            IReportExportService exportService,
            ILogger<ReportsController> logger,
            ErpClientFactory erpFactory,
            IConfiguration configuration)
        {
            _catalog = catalog;
            _runner = runner;
            _authorizationService = authorizationService;
            _exportService = exportService;
            _logger = logger;
            _erpFactory = erpFactory;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userContext = BuildUserContext();
            if (userContext == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var reports = _catalog.GetAll()
                .Where(r => _authorizationService.IsUserAuthorized(r, userContext))
                .ToList();

            var viewModel = new ReportCatalogViewModel
            {
                AvailableReports = reports
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult Run(string id)
        {
            var definition = _catalog.GetById(id);
            var userContext = BuildUserContext();
            if (definition == null || userContext == null)
            {
                return RedirectToAction("Index");
            }

            if (!_authorizationService.IsUserAuthorized(definition, userContext))
            {
                return Forbid();
            }

            var parameters = new ReportParameters
            {
                FromDate = DateTime.Today.AddMonths(-3),
                ToDate = DateTime.Today,
                Location = userContext.OfficeLocation,
                MinMargin = 0.15m,
                TargetMargin = 0.22m,
                MgmtName = string.Empty,
                FilterByMgmt = false,
                WarehouseId = userContext.LocationId == 0 ? 1 : userContext.LocationId
            };

            var viewModel = new ReportRunViewModel
            {
                Definition = definition,
                Parameters = parameters,
                PageNumber = 1,
                PageSize = 10,
                ShowResults = false
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Run(ReportRunInput input)
        {
            var definition = _catalog.GetById(input.DefinitionId);
            var userContext = BuildUserContext();
            if (definition == null || userContext == null)
            {
                return RedirectToAction("Index");
            }

            if (!_authorizationService.IsUserAuthorized(definition, userContext))
            {
                return Forbid();
            }

            var viewModel = new ReportRunViewModel
            {
                Definition = definition,
                Parameters = input.Parameters,
                PageNumber = Math.Max(1, input.PageNumber),
                PageSize = input.PageSize <= 0 ? 10 : input.PageSize,
                ShowResults = true
            };

            if (!ValidateDateRange(input.Parameters, out var validationError))
            {
                viewModel.ErrorMessage = validationError;
                viewModel.ShowResults = false;
                return View(viewModel);
            }

            try
            {
                var data = await _runner.RunAsync(definition.Id, input.Parameters, userContext);
                PopulatePagedResults(viewModel, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run report {ReportId}", definition.Id);
                viewModel.ErrorMessage = "We couldn't complete that request right now. Please try again or contact support.";
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(ReportRunInput input)
        {
            var definition = _catalog.GetById(input.DefinitionId);
            var userContext = BuildUserContext();
            if (definition == null || userContext == null)
            {
                return RedirectToAction("Index");
            }

            if (!_authorizationService.IsUserAuthorized(definition, userContext))
            {
                return Forbid();
            }

            if (!ValidateDateRange(input.Parameters, out var validationError))
            {
                TempData["ReportError"] = validationError;
                return RedirectToAction("Run", new { id = input.DefinitionId });
            }

            try
            {
                var data = await _runner.RunAsync(definition.Id, input.Parameters, userContext);
                var isExcel = string.Equals(input.ExportFormat, "excel", StringComparison.OrdinalIgnoreCase);
                var bytes = isExcel
                    ? _exportService.ExportToExcel(data, out var excelContentType)
                    : _exportService.ExportToCsv(data, out excelContentType);
                var contentType = isExcel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv";
                var extension = isExcel ? "xlsx" : "csv";
                var fileName = $"{definition.Name.Replace(' ', '_')}_{DateTime.Now:yyyyMMddHHmmss}.{extension}";
                return File(bytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export report {ReportId}", definition?.Id);
                TempData["ReportError"] = "Export failed. Please try again later.";
                return RedirectToAction("Run", new { id = input.DefinitionId });
            }
        }

        private ReportUserContext? BuildUserContext()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var roleId = HttpContext.Session.GetString("RoleId");
            var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
            var officeLocation = HttpContext.Session.GetString("OfficeLocation") ?? string.Empty;
            var locationId = LocationHelper.GetCurrentLocationId(HttpContext);

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleId))
            {
                return null;
            }

            return new ReportUserContext
            {
                UserId = userId,
                RoleId = roleId,
                RoleName = roleName,
                OfficeLocation = officeLocation,
                LocationId = locationId
            };
        }

        private static void PopulatePagedResults(ReportRunViewModel viewModel, DataTable data)
        {
            viewModel.Columns = data.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            viewModel.TotalRows = data.Rows.Count;

            // Send all rows to the view - DataTables will handle client-side pagination
            var allRows = data.AsEnumerable()
                .Select(row => data.Columns.Cast<DataColumn>()
                    .ToDictionary(c => c.ColumnName, c => row[c]))
                .ToList();

            viewModel.Rows = allRows;
        }

        private bool ValidateDateRange(ReportParameters parameters, out string error)
        {
            error = string.Empty;

            if (!parameters.FromDate.HasValue || !parameters.ToDate.HasValue)
            {
                error = "Please provide both a From and To date.";
                return false;
            }

            if (parameters.FromDate > parameters.ToDate)
            {
                error = "From Date cannot be after To Date.";
                return false;
            }

            if ((parameters.ToDate.Value - parameters.FromDate.Value).TotalDays > 365)
            {
                error = "Please limit the date range to 12 months or less.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get order details for drill-down from reports
        /// Uses direct SQL query to load complete order details including line items and notes
        /// </summary>
        [HttpGet]
        public IActionResult GetOrderDetails(string orderId)
        {
            try
            {
                string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
                var connectionString = _configuration.GetConnectionString(officeLocation);

                var viewModel = new WorkOrderDetailViewModel();

                using (var conn = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new System.Data.SqlClient.SqlCommand(@"
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
                                Prod_Style = reader.GetString(reader.GetOrdinal("PRODUCT_STYLE")),
                                Prod_Color = reader.GetString(reader.GetOrdinal("PRODUCT_COLOR")),
                                Quantity = Convert.ToDecimal(reader[2]),
                                UOM = reader.GetString(reader.GetOrdinal("ORDER_UOM")),
                                ProdClass = reader.GetString(reader.GetOrdinal("PRODUCT_CLASS")),
                                Description = reader.GetString(reader.GetOrdinal("PRODUCT_DESC")),
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

                return PartialView("~/Views/Orders/_WorkOrderDetailsPartial.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load order details for order {OrderId}", orderId);
                return PartialView("~/Views/Orders/_WorkOrderDetailsPartial.cshtml", new WorkOrderDetailViewModel());
            }
        }
    }
}
