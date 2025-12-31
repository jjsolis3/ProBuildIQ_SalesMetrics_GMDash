using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Services.Settings;

namespace SalesMetrics.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly SalesMetricsDbContext _context;

        public SettingsController(ISettingsService settingsService, SalesMetricsDbContext context)
        {
            _settingsService = settingsService;
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("Users_Id")?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private int GetCurrentRoleId()
        {
            var roleIdClaim = User.FindFirst("RoleId")?.Value;
            return int.TryParse(roleIdClaim, out var roleId) ? roleId : 0;
        }

        private bool IsAdmin()
        {
            var roleId = GetCurrentRoleId();
            return roleId == 1; // Admin only
        }

        // ======================================================================
        // Main View
        // ======================================================================

        // GET: /Settings
        [HttpGet]
        public async Task<IActionResult> Index(string tab = "notifications")
        {
            if (!IsAdmin())
                return Forbid();

            var dashboard = await _settingsService.GetDashboardDataAsync();
            dashboard.ActiveTab = tab;

            // Get additional data for dropdowns
            ViewBag.TaskStatuses = _context.Tasks
                .Select(t => new { t.Status })
                .Distinct()
                .OrderBy(t => t.Status)
                .ToList();

            return View(dashboard);
        }

        // ======================================================================
        // Notification Settings API
        // ======================================================================

        // POST: /Settings/UpdateNotificationSetting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotificationSetting([FromBody] UpdateNotificationSettingRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                var userId = GetCurrentUserId();
                await _settingsService.UpdateNotificationSettingAsync(request, userId);
                return Json(new { success = true, message = "Notification setting updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: /Settings/GetNotificationSettings
        [HttpGet]
        public async Task<IActionResult> GetNotificationSettings()
        {
            if (!IsAdmin())
                return Forbid();

            var settings = await _settingsService.GetNotificationSettingsAsync();
            return Json(settings);
        }

        // GET: /Settings/GetNotificationSetting/5
        [HttpGet]
        public async Task<IActionResult> GetNotificationSetting(int id)
        {
            if (!IsAdmin())
                return Forbid();

            var setting = await _settingsService.GetNotificationSettingByIdAsync(id);
            if (setting == null)
                return NotFound();

            return Json(setting);
        }

        // ======================================================================
        // Security Settings API
        // ======================================================================

        // POST: /Settings/UpdateSecuritySetting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSecuritySetting([FromBody] UpdateSecuritySettingRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                var userId = GetCurrentUserId();
                await _settingsService.UpdateSecuritySettingAsync(request, userId);
                return Json(new { success = true, message = "Security setting updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: /Settings/GetSecuritySettings
        [HttpGet]
        public async Task<IActionResult> GetSecuritySettings()
        {
            if (!IsAdmin())
                return Forbid();

            var settings = await _settingsService.GetSecuritySettingsAsync();
            return Json(settings);
        }

        // GET: /Settings/GetSecuritySettingsByCategory
        [HttpGet]
        public async Task<IActionResult> GetSecuritySettingsByCategory(string category)
        {
            if (!IsAdmin())
                return Forbid();

            var settings = await _settingsService.GetSecuritySettingsByCategoryAsync(category);
            return Json(settings);
        }

        // ======================================================================
        // Initialization
        // ======================================================================

        // POST: /Settings/InitializeDefaults
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitializeDefaults()
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                await _settingsService.InitializeDefaultNotificationSettingsAsync();
                await _settingsService.InitializeDefaultSecuritySettingsAsync();

                TempData["SuccessMessage"] = "Default settings initialized successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error initializing defaults: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
