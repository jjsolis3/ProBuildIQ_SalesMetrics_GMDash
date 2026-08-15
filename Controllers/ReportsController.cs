using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Linq;
using SalesMetrics.Data;
using SalesMetrics.Data.Entities;
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
        private readonly SalesMetricsDbContext _db;

        public ReportsController(
            IReportCatalog catalog,
            IReportRunner runner,
            IReportAuthorizationService authorizationService,
            IReportExportService exportService,
            ILogger<ReportsController> logger,
            ErpClientFactory erpFactory,
            IConfiguration configuration,
            SalesMetricsDbContext db)
        {
            _catalog = catalog;
            _runner = runner;
            _authorizationService = authorizationService;
            _exportService = exportService;
            _logger = logger;
            _erpFactory = erpFactory;
            _configuration = configuration;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userContext = BuildUserContext();
            if (userContext == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var all = _catalog.GetAll().ToList();

            // Resolve every report's access decision in one pass so a single
            // round-trip covers the whole catalog instead of one query per card.
            var accessMap = await BuildCatalogAccessMapAsync(all.Select(r => r.Id));

            var reports = all
                .Where(r => IsCatalogReportAllowed(r, userContext, accessMap))
                .ToList();

            var viewModel = new ReportCatalogViewModel
            {
                AvailableReports = reports,
                IsAdmin = IsAdminUser(userContext)
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Run(string id)
        {
            // The envelope activity report has its own dedicated view and action.
            if (string.Equals(id, "envelope-activity", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(EnvelopeReport));

            var definition = _catalog.GetById(id);
            var userContext = BuildUserContext();
            if (definition == null || userContext == null)
            {
                return RedirectToAction("Index");
            }

            if (!await CanUserRunCatalogReportAsync(definition, userContext))
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

            if (!await CanUserRunCatalogReportAsync(definition, userContext))
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

            if (!await CanUserRunCatalogReportAsync(definition, userContext))
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

                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
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

        // ======================================================================
        // Envelope Activity Report
        // ======================================================================

        public class ReportAccessRequest  { public string ReportId { get; set; } = ""; public int UsersId { get; set; } }
    public class ReportAccessRevokeRequest { public int AccessId { get; set; } }

    private static readonly int[] EnvelopeReportAllowedRoles = { 1, 4, 5, 6, 7, 8 };

        private EnvelopeReportViewModel BuildEnvelopeReportBase(ReportUserContext userContext)
        {
            var roleId = int.Parse(userContext.RoleId ?? "0");
            // Admin (1), GM (4), President (7), Regional Manager (8) can view any branch.
            var canViewAll = roleId == 1 || roleId == 4 || roleId == 7 || roleId == 8;
            return new EnvelopeReportViewModel
            {
                CanViewAllBranches = canViewAll,
                Locations = LocationHelper.Locations,
                CurrentLocationCode = userContext.OfficeLocation
            };
        }

        /// <summary>
        /// Shared query + KPI logic for both the interactive report and the standalone print view.
        /// Populates vm.Rows and all KPI fields in place.
        /// </summary>
        private async Task PopulateEnvelopeReportDataAsync(EnvelopeReportViewModel vm, ReportUserContext userContext)
        {
            // Determine location scope:
            //   • Admins/GMs who explicitly picked a branch → use that branch.
            //   • Admins/GMs with no branch selected       → show all branches.
            //   • Office staff / managers                  → always their current location.
            string? locationCode;
            if (vm.CanViewAllBranches)
                locationCode = string.IsNullOrWhiteSpace(vm.BranchFilter) ? null : vm.BranchFilter;
            else
                locationCode = userContext.OfficeLocation;

            var fromUtc = vm.FromDate!.Value.Date;
            var toUtc   = vm.ToDate!.Value.Date.AddDays(1); // include full To date

            var query = _db.SignEnvelopes
                .AsNoTracking()
                .Where(e => e.CreatedDateUtc >= fromUtc && e.CreatedDateUtc < toUtc);

            if (!string.IsNullOrWhiteSpace(locationCode))
                query = query.Where(e => e.LocationCode == locationCode);

            if (!string.IsNullOrWhiteSpace(vm.StatusFilter))
                query = query.Where(e => e.Status == vm.StatusFilter);

            var envelopes = await query
                .OrderByDescending(e => e.SentAtUtc ?? e.CreatedDateUtc)
                .Select(e => new
                {
                    e.EnvelopeId,
                    e.Subject,
                    e.OrderNumber,
                    e.LocationCode,
                    e.Status,
                    e.CreatedDateUtc,
                    e.SentAtUtc,
                    e.CompletedAtUtc,
                    RecipientCount = e.Recipients.Count(),
                    SignedCount    = e.Recipients.Count(r => r.SignedAtUtc != null)
                })
                .ToListAsync();

            vm.Rows = envelopes.Select(e => new EnvelopeReportRow
            {
                EnvelopeId      = e.EnvelopeId,
                Subject         = e.Subject,
                OrderNumber     = e.OrderNumber,
                LocationCode    = e.LocationCode,
                Status          = e.Status,
                CreatedDateUtc  = e.CreatedDateUtc,
                SentAtUtc       = e.SentAtUtc,
                CompletedAtUtc  = e.CompletedAtUtc,
                RecipientCount  = e.RecipientCount,
                SignedCount     = e.SignedCount,
                DaysToComplete  = e.SentAtUtc.HasValue && e.CompletedAtUtc.HasValue
                    ? Math.Round((e.CompletedAtUtc.Value - e.SentAtUtc.Value).TotalDays, 1)
                    : (double?)null
            }).ToList();

            // KPI calculations
            vm.TotalEnvelopes    = vm.Rows.Count;
            vm.CompletedCount    = vm.Rows.Count(r => r.Status == "Completed");
            vm.PendingCount      = vm.Rows.Count(r => r.Status == "Sent" || r.Status == "Viewed");
            vm.ExpiredVoidedCount = vm.Rows.Count(r =>
                r.Status == "Expired" || r.Status == "Voided" || r.Status == "Declined");

            var sentRows = vm.Rows.Where(r => r.Status != "Draft").ToList();
            vm.CompletionRate = sentRows.Count > 0
                ? Math.Round((double)vm.CompletedCount / sentRows.Count * 100, 1)
                : 0;

            var completedTimes = vm.Rows
                .Where(r => r.DaysToComplete.HasValue)
                .Select(r => r.DaysToComplete!.Value)
                .ToList();
            vm.AvgDaysToComplete = completedTimes.Any()
                ? Math.Round(completedTimes.Average(), 1)
                : 0;
        }

        [HttpGet]
        public async Task<IActionResult> EnvelopeReport()
        {
            var definition  = _catalog.GetById("envelope-activity");
            var userContext = BuildUserContext();
            if (definition == null || userContext == null) return RedirectToAction("Index");
            if (!await CanUserRunCatalogReportAsync(definition, userContext)) return Forbid();

            var vm = BuildEnvelopeReportBase(userContext);
            vm.FromDate = DateTime.Today.AddDays(-30);
            vm.ToDate   = DateTime.Today;
            return View("RunEnvelope", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnvelopeReport(EnvelopeReportViewModel input)
        {
            var definition  = _catalog.GetById("envelope-activity");
            var userContext = BuildUserContext();
            if (definition == null || userContext == null) return RedirectToAction("Index");
            if (!await CanUserRunCatalogReportAsync(definition, userContext)) return Forbid();

            var vm = BuildEnvelopeReportBase(userContext);
            vm.FromDate     = input.FromDate;
            vm.ToDate       = input.ToDate;
            vm.StatusFilter = input.StatusFilter;
            vm.BranchFilter = input.BranchFilter;
            vm.ShowResults  = true;

            if (!vm.FromDate.HasValue || !vm.ToDate.HasValue)
            {
                vm.ErrorMessage = "Please select both a From and To date.";
                vm.ShowResults  = false;
                return View("RunEnvelope", vm);
            }
            if (vm.FromDate > vm.ToDate)
            {
                vm.ErrorMessage = "From Date cannot be after To Date.";
                vm.ShowResults  = false;
                return View("RunEnvelope", vm);
            }
            if ((vm.ToDate.Value - vm.FromDate.Value).TotalDays > 365)
            {
                vm.ErrorMessage = "Please limit the date range to 12 months.";
                vm.ShowResults  = false;
                return View("RunEnvelope", vm);
            }

            try
            {
                await PopulateEnvelopeReportDataAsync(vm, userContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run envelope activity report");
                vm.ErrorMessage = "Failed to load report data. Please try again.";
                vm.ShowResults  = false;
            }

            return View("RunEnvelope", vm);
        }

        // ======================================================================
        // Per-Report Access Management (Reports Center catalog reports)
        // ======================================================================

        /// <summary>
        /// Returns the list of users that have been granted explicit access to a catalog report.
        /// Also returns all active users so the admin can add new grantees.
        /// Admin-only endpoint (RoleId 1).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetReportAccess(string reportId)
        {
            if (!IsAdminUser()) return AccessDeniedJson();

            if (string.IsNullOrWhiteSpace(reportId))
                return Json(new { success = false, message = "No report was specified." });

            var reportKey = $"catalog:{reportId}";

            var grantedUsers = await _db.ReportAccess
                .Where(a => a.ReportKey == reportKey)
                .Include(a => a.User)
                .Select(a => new
                {
                    accessId = a.AccessId,
                    usersId  = a.Users_ID,
                    fullName = a.User != null ? a.User.FirstName + " " + a.User.LastName : "Unknown",
                    grantedDate = a.GrantedDate.ToString("MM/dd/yyyy")
                })
                .ToListAsync();

            var allUsers = await _db.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .Select(u => new { usersId = u.Users_ID, fullName = u.FirstName + " " + u.LastName, roleId = u.RoleId })
                .ToListAsync();

            return Json(new { success = true, grantedUsers, allUsers });
        }

        /// <summary>
        /// Grants a user access to a catalog report.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantReportAccess([FromBody] ReportAccessRequest request)
        {
            if (!IsAdminUser()) return AccessDeniedJson();

            if (request == null || string.IsNullOrWhiteSpace(request.ReportId) || request.UsersId <= 0)
                return Json(new { success = false, message = "Select a user before granting access." });

            var grantorUsersId = GetCurrentUsersId();
            var reportKey = $"catalog:{request.ReportId}";

            try
            {
                var existing = await _db.ReportAccess
                    .FirstOrDefaultAsync(a => a.ReportKey == reportKey && a.Users_ID == request.UsersId);

                if (existing != null)
                    return Json(new { success = true, message = "User already has access." });

                _db.ReportAccess.Add(new ReportAccessEntity
                {
                    ReportKey         = reportKey,
                    Users_ID          = request.UsersId,
                    GrantedDate       = DateTime.Now,
                    GrantedByUsers_ID = grantorUsersId > 0 ? grantorUsersId : null
                });

                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "User {GrantorId} granted user {UsersId} access to catalog report {ReportKey}",
                    grantorUsersId, request.UsersId, reportKey);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to grant user {UsersId} access to catalog report {ReportKey}",
                    request.UsersId, reportKey);
                return Json(new { success = false, message = "Could not save the grant. Please try again." });
            }
        }

        /// <summary>
        /// Revokes a user's access to a catalog report.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeReportAccess([FromBody] ReportAccessRevokeRequest request)
        {
            if (!IsAdminUser()) return AccessDeniedJson();

            if (request == null || request.AccessId <= 0)
                return Json(new { success = false, message = "No access record was specified." });

            try
            {
                var entry = await _db.ReportAccess.FindAsync(request.AccessId);
                if (entry == null) return Json(new { success = false, message = "Record not found." });

                _db.ReportAccess.Remove(entry);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "User {GrantorId} revoked catalog report access record {AccessId}",
                    GetCurrentUsersId(), request.AccessId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke catalog report access record {AccessId}", request.AccessId);
                return Json(new { success = false, message = "Could not remove the grant. Please try again." });
            }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        /// <summary>
        /// Checks whether the current user may run the given catalog report.
        ///
        /// Precedence (matches what the Manage Access modal tells the admin):
        ///   1. Admins always pass.
        ///   2. An explicit grant in [ReportAccess] wins outright — it *overrides*
        ///      the report's role/location rules. This is the whole point of the
        ///      feature: it lets an admin hand a single person a report their role
        ///      would not otherwise reach.
        ///   3. If the report has any grants at all it is "restricted", so anyone
        ///      not on the list is denied even when their role/location would allow it.
        ///   4. Otherwise fall back to the normal role + location rules.
        /// </summary>
        private async Task<bool> CanUserRunCatalogReportAsync(IReportDefinition definition, ReportUserContext userContext)
        {
            if (definition == null || userContext == null) return false;

            var accessMap = await BuildCatalogAccessMapAsync(new[] { definition.Id });
            return IsCatalogReportAllowed(definition, userContext, accessMap);
        }

        /// <summary>
        /// Access state for one catalog report: whether anyone at all is granted
        /// (making the report restricted) and whether the current user is one of them.
        /// </summary>
        private readonly record struct CatalogAccessState(bool IsRestricted, bool UserIsGranted);

        /// <summary>
        /// Loads the grant state for a set of catalog reports in a single query.
        /// Reports with no rows in [ReportAccess] are simply absent from the map,
        /// which callers read as "not restricted".
        /// </summary>
        private async Task<Dictionary<string, CatalogAccessState>> BuildCatalogAccessMapAsync(IEnumerable<string> reportIds)
        {
            var keys = reportIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => $"catalog:{id}")
                .Distinct()
                .ToList();

            var map = new Dictionary<string, CatalogAccessState>(StringComparer.OrdinalIgnoreCase);
            if (keys.Count == 0) return map;

            var currentUsersId = GetCurrentUsersId();

            try
            {
                var rows = await _db.ReportAccess
                    .Where(a => keys.Contains(a.ReportKey))
                    .Select(a => new { a.ReportKey, a.Users_ID })
                    .ToListAsync();

                foreach (var group in rows.GroupBy(r => r.ReportKey, StringComparer.OrdinalIgnoreCase))
                {
                    map[group.Key] = new CatalogAccessState(
                        IsRestricted: true,
                        UserIsGranted: currentUsersId > 0 && group.Any(r => r.Users_ID == currentUsersId));
                }
            }
            catch (Exception ex)
            {
                // A per-report grant list is an enhancement layered on top of the
                // role/location rules. If it cannot be read we log and fall back to
                // those rules rather than locking every user out of every report.
                _logger.LogError(ex, "Could not load per-report access grants; falling back to role/location rules");
            }

            return map;
        }

        /// <summary>
        /// Applies the access precedence rules described on
        /// <see cref="CanUserRunCatalogReportAsync"/> using a pre-loaded access map.
        /// </summary>
        private bool IsCatalogReportAllowed(
            IReportDefinition definition,
            ReportUserContext userContext,
            IReadOnlyDictionary<string, CatalogAccessState> accessMap)
        {
            if (definition == null || userContext == null) return false;

            // 1. Admins always pass.
            if (IsAdminUser(userContext)) return true;

            if (accessMap.TryGetValue($"catalog:{definition.Id}", out var state) && state.IsRestricted)
            {
                // 2 & 3. The grant list is authoritative once it has any entries.
                return state.UserIsGranted;
            }

            // 4. No grants recorded — normal role + location rules apply.
            return _authorizationService.IsUserAuthorized(definition, userContext);
        }

        /// <summary>
        /// True when the caller is an application administrator (RoleId 1).
        /// Reads the session first and falls back to the auth cookie claim so the
        /// check still holds if the session has been recycled but the user is
        /// still signed in.
        /// </summary>
        private bool IsAdminUser(ReportUserContext? userContext = null)
        {
            if (userContext != null && userContext.RoleId == "1") return true;

            var sessionRoleId = HttpContext.Session.GetString("RoleId");
            if (sessionRoleId == "1") return true;

            return User.FindFirst("RoleId")?.Value == "1";
        }

        /// <summary>
        /// Resolves the signed-in user's Users_ID primary key, preferring the auth
        /// cookie claim and falling back to the session. Returns 0 when unknown —
        /// never throws, unlike the int.Parse this replaced.
        /// </summary>
        private int GetCurrentUsersId()
        {
            if (int.TryParse(User.FindFirst("Users_ID")?.Value, out var fromClaim) && fromClaim > 0)
                return fromClaim;

            if (int.TryParse(HttpContext.Session.GetString("Users_ID"), out var fromSession) && fromSession > 0)
                return fromSession;

            return 0;
        }

        /// <summary>
        /// Returns a 403 carrying a JSON body. Plain <c>Forbid()</c> is handled by the
        /// cookie handler, which answers an AJAX call with a 302 to the access-denied
        /// page — the browser follows it and the caller sees an HTML document instead
        /// of an error, which is why failures used to be invisible in the UI.
        /// </summary>
        private IActionResult AccessDeniedJson()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Json(new { success = false, message = "You do not have permission to manage report access." });
        }

        /// <summary>
        /// Opens a standalone, print-ready view (Layout=null) for the Envelope Activity Report.
        /// Accepts the same filters as the interactive report via query-string so the Print button
        /// in RunEnvelope.cshtml can open it with window.open() in a new tab.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PrintEnvelopeReport(
            string? fromDate, string? toDate, string? statusFilter, string? branchFilter)
        {
            var definition  = _catalog.GetById("envelope-activity");
            var userContext = BuildUserContext();
            if (definition == null || userContext == null) return RedirectToAction("Index");
            if (!await CanUserRunCatalogReportAsync(definition, userContext)) return Forbid();

            var vm = BuildEnvelopeReportBase(userContext);
            vm.FromDate     = DateTime.TryParse(fromDate, out var f) ? f : DateTime.Today.AddDays(-30);
            vm.ToDate       = DateTime.TryParse(toDate,   out var t) ? t : DateTime.Today;
            vm.StatusFilter = statusFilter;
            vm.BranchFilter = branchFilter;
            vm.ShowResults  = true;

            try
            {
                await PopulateEnvelopeReportDataAsync(vm, userContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate envelope print report");
                vm.ErrorMessage = "Failed to load report data.";
            }

            return View("PrintEnvelopeReport", vm);
        }
    }
}
