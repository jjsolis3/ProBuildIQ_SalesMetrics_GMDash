using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SalesMetrics.Data;
using SalesMetrics.Data.Entities.QueryBuilder;
using SalesMetrics.Services.Permissions;
using SalesMetrics.Models.QueryBuilder;
using Microsoft.EntityFrameworkCore;

namespace SalesMetrics.Controllers
{
    [Authorize]
    public class ReportBuilderController : Controller
    {
        private readonly SalesMetricsDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<ReportBuilderController> _logger;

        public ReportBuilderController(
            SalesMetricsDbContext context,
            IPermissionService permissionService,
            ILogger<ReportBuilderController> logger)
        {
            _context = context;
            _permissionService = permissionService;
            _logger = logger;
        }

        /// <summary>
        /// Display list of all custom reports (Query Builder Index page)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();

            // Check if user has Query Builder access
            var hasAccess = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ACCESS");
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted to access Query Builder without permission", userId);
                return Forbid();
            }

            var currentLocation = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
            var roleId = HttpContext.Session.GetString("RoleId") ?? "0";

            // Get all reports the user can access
            var reports = await _context.ReportDefinitions
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.ModifiedDate ?? r.CreatedDate)
                .ToListAsync();

            // TODO: Filter reports based on user's role and location
            // For now, show all active reports

            var viewModel = new ReportBuilderIndexViewModel
            {
                Reports = reports.Select(r => new ReportSummaryDto
                {
                    ReportId = r.ReportDefinitionId,
                    Name = r.Name ?? "Untitled Report",
                    Description = r.Description ?? "",
                    DataSourceType = r.DataSourceType ?? "SQL",
                    CreatedBy = "User " + r.CreatedByUserId, // TODO: Join with Users table to get name
                    CreatedDate = r.CreatedDate,
                    LastModifiedDate = r.ModifiedDate ?? r.CreatedDate,
                    IsShared = !string.IsNullOrEmpty(r.AllowedRoles) || !string.IsNullOrEmpty(r.AllowedLocations)
                }).ToList(),
                CanCreateReports = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_CREATE"),
                CanEditReports = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_EDIT"),
                CanDeleteReports = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_DELETE"),
                IsAdmin = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ADMIN")
            };

            return View(viewModel);
        }

        /// <summary>
        /// Show create report wizard - Step 1: Table Selection
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();

            var canCreate = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_CREATE");
            if (!canCreate)
            {
                _logger.LogWarning("User {UserId} attempted to create report without permission", userId);
                return Forbid();
            }

            // Load available tables from whitelist
            var allowedTables = await _context.AllowedTables
                .Where(t => t.IsActive)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.TableName)
                .ToListAsync();

            // Build view model for Step 1
            var viewModel = new ReportBuilderCreateViewModel
            {
                CurrentStep = 1,
                TotalSteps = 5,
                AvailableTables = allowedTables.Select(t => new TableSelectionItem
                {
                    TableId = t.AllowedTableId,
                    TableName = t.TableName ?? "",
                    SchemaName = t.SchemaName ?? "dbo",
                    DisplayName = t.DisplayName ?? t.TableName ?? "",
                    Description = t.Description ?? "",
                    Category = t.Category ?? "Uncategorized",
                    DataSourceType = t.DataSourceType ?? "SQL",
                    IsSelected = false
                }).ToList(),
                CanShare = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_SHARE"),
                IsAdmin = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_ADMIN")
            };

            _logger.LogInformation("User {UserId} started creating a new report", userId);

            return View(viewModel);
        }

        /// <summary>
        /// Show edit report wizard (Phase 2 implementation)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();

            var canEdit = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_EDIT");
            if (!canEdit)
            {
                _logger.LogWarning("User {UserId} attempted to edit report without permission", userId);
                return Forbid();
            }

            var report = await _context.ReportDefinitions
                .FirstOrDefaultAsync(r => r.ReportDefinitionId == id);

            if (report == null)
            {
                return NotFound();
            }

            // TODO: Implement edit report wizard
            return View();
        }

        /// <summary>
        /// Delete a report
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();

            var canDelete = await _permissionService.HasFeatureAccessAsync(userId, "REPORT_BUILDER_DELETE");
            if (!canDelete)
            {
                return Json(new { success = false, message = "You don't have permission to delete reports" });
            }

            var report = await _context.ReportDefinitions
                .FirstOrDefaultAsync(r => r.ReportDefinitionId == id);

            if (report == null)
            {
                return Json(new { success = false, message = "Report not found" });
            }

            // Soft delete
            report.IsActive = false;
            report.ModifiedDate = DateTime.UtcNow;
            report.ModifiedByUserId = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted report {ReportId}", userId, id);

            return Json(new { success = true, message = "Report deleted successfully" });
        }

        /// <summary>
        /// Helper method to get current user ID from claims
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("Users_ID")?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
