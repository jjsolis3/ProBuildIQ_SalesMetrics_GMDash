using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Linq;
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

        public ReportsController(
            IReportCatalog catalog,
            IReportRunner runner,
            IReportAuthorizationService authorizationService,
            IReportExportService exportService,
            ILogger<ReportsController> logger,
            ErpClientFactory erpFactory)
        {
            _catalog = catalog;
            _runner = runner;
            _authorizationService = authorizationService;
            _exportService = exportService;
            _logger = logger;
            _erpFactory = erpFactory;
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
        /// Uses ERP abstraction layer to work with any ERP
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            try
            {
                var officeLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
                var context = new ErpContext
                {
                    LocationCode = officeLocation
                };

                var client = _erpFactory.GetClient(context);
                var order = await client.GetOrderDetailAsync(orderId, context);

                if (order == null)
                {
                    return PartialView("_OrderDetailsPartial", null);
                }

                return PartialView("_OrderDetailsPartial", order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load order details for order {OrderId}", orderId);
                return PartialView("_OrderDetailsPartial", null);
            }
        }
    }
}
