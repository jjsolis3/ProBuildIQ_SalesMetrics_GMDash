using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Services.Permissions;
using SalesMetrics.Services.Settings;

namespace SalesMetrics.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly SalesMetricsDbContext _context;
        private readonly IPermissionService _permissionService;

        public SettingsController(ISettingsService settingsService, SalesMetricsDbContext context, IPermissionService permissionService)
        {
            _settingsService = settingsService;
            _context = context;
            _permissionService = permissionService;
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
        // Settings Hub
        // ======================================================================

        // GET: /Settings/Hub
        [HttpGet]
        public async Task<IActionResult> Hub()
        {
            var userId = GetCurrentUserId();

            var vm = new SettingsHubViewModel
            {
                CanAccessAnnouncements  = userId > 0 && await _permissionService.HasFeatureAccessAsync(userId, "Announcements"),
                CanAccessSystemSettings = userId > 0 && await _permissionService.HasFeatureAccessAsync(userId, "Settings"),
                CanAccessAccessControl  = userId > 0 && await _permissionService.HasFeatureAccessAsync(userId, "Users"),
                CanAccessYardiUpload    = userId > 0 && await _permissionService.HasFeatureAccessAsync(userId, "YardiUpload"),
                CanAccessErrorLogs      = userId > 0 && await _permissionService.HasFeatureAccessAsync(userId, "ErrorLogs"),
            };

            if (!vm.CanAccessAnnouncements && !vm.CanAccessSystemSettings &&
                !vm.CanAccessAccessControl  && !vm.CanAccessYardiUpload  && !vm.CanAccessErrorLogs)
                return Forbid();

            return View(vm);
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
                await _settingsService.InitializeDefaultEnvelopeNotificationSettingsAsync();
                await _settingsService.InitializeDefaultBrandingSettingsAsync();

                TempData["SuccessMessage"] = "Default settings initialized successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error initializing defaults: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ======================================================================
        // Envelope Notification Settings API
        // ======================================================================

        // GET: /Settings/GetEnvelopeNotificationSettings
        [HttpGet]
        public async Task<IActionResult> GetEnvelopeNotificationSettings()
        {
            if (!IsAdmin())
                return Forbid();

            var settings = await _settingsService.GetEnvelopeNotificationSettingsAsync();
            return Json(settings);
        }

        // POST: /Settings/SaveEnvelopeNotificationSetting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEnvelopeNotificationSetting([FromBody] SaveEnvelopeNotificationSettingRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                var userId = GetCurrentUserId();
                await _settingsService.SaveEnvelopeNotificationSettingAsync(request, userId);
                return Json(new { success = true, message = "Envelope notification setting saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ======================================================================
        // Branding Settings API
        // ======================================================================

        // POST: /Settings/SaveBrandingSetting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBrandingSetting([FromBody] SaveBrandingSettingRequest request)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                var userId = GetCurrentUserId();
                await _settingsService.SaveBrandingSettingAsync(request, userId);
                return Json(new { success = true, message = "Branding setting saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: /Settings/SaveAllBrandingSettings  (saves the whole Branding form at once)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAllBrandingSettings(IFormCollection form)
        {
            if (!IsAdmin())
                return Forbid();

            try
            {
                var userId = GetCurrentUserId();
                var keys = new[] { "CompanyName", "LogoUrl", "EnvelopeLogoUrl", "Website", "Phone" };
                foreach (var key in keys)
                {
                    if (form.TryGetValue($"Branding_{key}", out var val))
                    {
                        await _settingsService.SaveBrandingSettingAsync(
                            new SaveBrandingSettingRequest { SettingKey = key, SettingValue = val.ToString() },
                            userId);
                    }
                }
                TempData["SuccessMessage"] = "Branding settings saved successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error saving branding settings: {ex.Message}";
            }

            return RedirectToAction(nameof(Index), new { tab = "branding" });
        }
    }
}
