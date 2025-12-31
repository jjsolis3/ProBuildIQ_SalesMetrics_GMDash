using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Services.Announcements;

namespace SalesMetrics.Controllers
{
    public class AnnouncementsController : Controller
    {
        private readonly IAnnouncementService _announcementService;
        private readonly SalesMetricsDbContext _context;

        public AnnouncementsController(IAnnouncementService announcementService, SalesMetricsDbContext context)
        {
            _announcementService = announcementService;
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

        private bool IsAuthorized()
        {
            var roleId = GetCurrentRoleId();
            return roleId == 1 || roleId == 4; // Admin (1) or GM (4)
        }

        // ======================================================================
        // Main Views
        // ======================================================================

        // GET: /Announcements
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAuthorized())
                return Forbid();

            var dashboard = await _announcementService.GetDashboardDataAsync();
            return View(dashboard);
        }

        // GET: /Announcements/Create
        [HttpGet]
        public IActionResult Create()
        {
            if (!IsAuthorized())
                return Forbid();

            ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
            ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();

            var model = new AnnouncementCreateEditViewModel();
            return View(model);
        }

        // POST: /Announcements/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AnnouncementCreateEditViewModel model)
        {
            if (!IsAuthorized())
                return Forbid();

            if (!ModelState.IsValid)
            {
                ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
                ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();
                return View(model);
            }

            try
            {
                var userId = GetCurrentUserId();
                var id = await _announcementService.CreateAnnouncementAsync(model, userId);

                TempData["SuccessMessage"] = model.SendImmediately
                    ? "Announcement sent successfully!"
                    : "Announcement created successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating announcement: {ex.Message}");
                ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
                ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();
                return View(model);
            }
        }

        // GET: /Announcements/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAuthorized())
                return Forbid();

            var announcement = await _announcementService.GetAnnouncementByIdAsync(id);
            if (announcement == null)
                return NotFound();

            // Don't allow editing sent announcements
            if (announcement.IsSent)
            {
                TempData["ErrorMessage"] = "Cannot edit an announcement that has already been sent.";
                return RedirectToAction(nameof(Index));
            }

            var model = new AnnouncementCreateEditViewModel
            {
                BroadcastMessageId = announcement.BroadcastMessageId,
                Title = announcement.Title,
                Message = announcement.Message,
                TargetType = announcement.TargetType,
                TargetLocationId = announcement.TargetLocationId,
                TargetRoleId = announcement.TargetRoleId,
                ExpiresDate = announcement.ExpiresDate,
                Priority = announcement.Priority ?? "Normal",
                ScheduledDate = announcement.ScheduledDate,
                SendImmediately = !announcement.ScheduledDate.HasValue,
                IsTemplate = announcement.IsTemplate,
                TemplateCategory = announcement.TemplateCategory
            };

            ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
            ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();

            return View(model);
        }

        // POST: /Announcements/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AnnouncementCreateEditViewModel model)
        {
            if (!IsAuthorized())
                return Forbid();

            if (!ModelState.IsValid)
            {
                ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
                ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();
                return View(model);
            }

            try
            {
                var userId = GetCurrentUserId();
                await _announcementService.UpdateAnnouncementAsync(id, model, userId);

                TempData["SuccessMessage"] = "Announcement updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating announcement: {ex.Message}");
                ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
                ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();
                return View(model);
            }
        }

        // GET: /Announcements/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!IsAuthorized())
                return Forbid();

            var announcement = await _announcementService.GetAnnouncementByIdAsync(id);
            if (announcement == null)
                return NotFound();

            return View(announcement);
        }

        // POST: /Announcements/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAuthorized())
                return Forbid();

            try
            {
                await _announcementService.DeleteAnnouncementAsync(id);
                TempData["SuccessMessage"] = "Announcement deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting announcement: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Announcements/Send/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int id)
        {
            if (!IsAuthorized())
                return Forbid();

            try
            {
                var userId = GetCurrentUserId();
                await _announcementService.SendAnnouncementAsync(id, userId);
                TempData["SuccessMessage"] = "Announcement sent successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error sending announcement: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ======================================================================
        // Templates
        // ======================================================================

        // GET: /Announcements/Templates
        [HttpGet]
        public async Task<IActionResult> Templates()
        {
            if (!IsAuthorized())
                return Forbid();

            var templates = await _announcementService.GetTemplatesAsync();
            return View(templates);
        }

        // GET: /Announcements/CreateTemplate
        [HttpGet]
        public IActionResult CreateTemplate()
        {
            if (!IsAuthorized())
                return Forbid();

            ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
            ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();

            var model = new AnnouncementCreateEditViewModel
            {
                IsTemplate = true,
                SendImmediately = false
            };
            return View("Create", model);
        }

        // GET: /Announcements/UseTemplate/5
        [HttpGet]
        public async Task<IActionResult> UseTemplate(int id)
        {
            if (!IsAuthorized())
                return Forbid();

            var template = await _announcementService.GetTemplateByIdAsync(id);
            if (template == null)
                return NotFound();

            var model = new AnnouncementCreateEditViewModel
            {
                Title = template.Title,
                Message = template.Message,
                TargetType = template.TargetType,
                TargetLocationId = template.TargetLocationId,
                TargetRoleId = template.TargetRoleId,
                Priority = template.Priority ?? "Normal",
                IsTemplate = false
            };

            ViewBag.Locations = _context.Locations.OrderBy(l => l.LocationName).ToList();
            ViewBag.Roles = _context.Roles.OrderBy(r => r.RoleName).ToList();

            return View("Create", model);
        }

        // ======================================================================
        // API Endpoints
        // ======================================================================

        // GET: /Announcements/GetDashboard
        [HttpGet]
        public async Task<IActionResult> GetDashboard()
        {
            if (!IsAuthorized())
                return Forbid();

            var dashboard = await _announcementService.GetDashboardDataAsync();
            return Json(dashboard);
        }

        // GET: /Announcements/GetActive
        [HttpGet]
        public async Task<IActionResult> GetActive()
        {
            if (!IsAuthorized())
                return Forbid();

            var announcements = await _announcementService.GetActiveAnnouncementsAsync();
            return Json(announcements);
        }

        // GET: /Announcements/GetScheduled
        [HttpGet]
        public async Task<IActionResult> GetScheduled()
        {
            if (!IsAuthorized())
                return Forbid();

            var announcements = await _announcementService.GetScheduledAnnouncementsAsync();
            return Json(announcements);
        }

        // GET: /Announcements/GetExpired
        [HttpGet]
        public async Task<IActionResult> GetExpired()
        {
            if (!IsAuthorized())
                return Forbid();

            var announcements = await _announcementService.GetExpiredAnnouncementsAsync();
            return Json(announcements);
        }
    }
}
