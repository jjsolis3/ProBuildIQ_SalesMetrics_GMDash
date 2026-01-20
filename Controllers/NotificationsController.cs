using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Services.Notifications;

namespace SalesMetrics.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly SalesMetricsDbContext _context;

        public NotificationsController(INotificationService notificationService, SalesMetricsDbContext context)
        {
            _notificationService = notificationService;
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

        // GET: /Notifications/GetNotifications
        [HttpGet]
        public async Task<IActionResult> GetNotifications(bool unreadOnly = false, int limit = 20)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly, limit);
            return Json(notifications);
        }

        // GET: /Notifications/GetUnreadCount
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { count });
        }

        // POST: /Notifications/MarkAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] NotificationMarkReadRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            await _notificationService.MarkAsReadAsync(request.NotificationId, userId);
            return Json(new { success = true });
        }

        // POST: /Notifications/ToggleRead
        [HttpPost]
        public async Task<IActionResult> ToggleRead([FromBody] NotificationMarkReadRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            // Get current notification status
            var recipient = await _context.NotificationRecipients
                .FirstOrDefaultAsync(nr => nr.NotificationId == request.NotificationId && nr.UserId == userId);

            if (recipient != null)
            {
                recipient.IsRead = request.IsRead;
                recipient.ReadDate = request.IsRead ? DateTime.Now : null;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // POST: /Notifications/MarkAllAsRead
        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(userId);
            return Json(new { success = true });
        }

        // POST: /Notifications/Delete
        [HttpPost]
        public async Task<IActionResult> Delete([FromBody] NotificationDeleteRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return Unauthorized();

            await _notificationService.DeleteNotificationAsync(request.NotificationId, userId);
            return Json(new { success = true });
        }

        // GET: /Notifications/Index - Notification Center Page
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var roleId = GetCurrentRoleId();
            var locationId = int.Parse(User.FindFirst("LocationId")?.Value ?? "0");

            // Get regular notifications (exclude broadcast messages)
            var allNotifications = await _notificationService.GetUserNotificationsAsync(userId, false, 100);
            var notifications = allNotifications.Where(n => n.NotificationType != "BroadcastMessage").ToList();

            // Get announcements (broadcast messages)
            var announcements = await _notificationService.GetBroadcastMessagesAsync(locationId, roleId);

            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            var viewModel = new NotificationListViewModel
            {
                Notifications = notifications,
                UnreadCount = unreadCount,
                TotalCount = notifications.Count
            };

            ViewBag.Announcements = announcements;

            return View(viewModel);
        }

        // GET: /Notifications/BroadcastMessages - Admin only
        public async Task<IActionResult> BroadcastMessages()
        {
            var roleId = GetCurrentRoleId();
            // Only Admin (RoleID = 1) and GM (RoleID = 4) can access
            if (roleId != 1 && roleId != 4)
                return Forbid();

            var messages = await _notificationService.GetActiveBroadcastMessagesAsync();
            return View(messages);
        }

        // GET: /Notifications/CreateBroadcast - Admin only
        public IActionResult CreateBroadcast()
        {
            var roleId = GetCurrentRoleId();
            // Only Admin (RoleID = 1) and GM (RoleID = 4) can access
            if (roleId != 1 && roleId != 4)
                return Forbid();

            // Get locations and roles for dropdown
            ViewBag.Locations = _context.Locations.Where(l => l.IsActive).ToList();
            ViewBag.Roles = _context.Roles.ToList();

            return View();
        }

        // POST: /Notifications/CreateBroadcast - Admin only
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBroadcast(BroadcastMessageCreateViewModel model)
        {
            var roleId = GetCurrentRoleId();
            // Only Admin (RoleID = 1) and GM (RoleID = 4) can access
            if (roleId != 1 && roleId != 4)
                return Forbid();

            if (!ModelState.IsValid)
            {
                ViewBag.Locations = await _context.Locations.Where(l => l.IsActive).ToListAsync();
                ViewBag.Roles = await _context.Roles.ToListAsync();
                return View(model);
            }

            var userId = GetCurrentUserId();
            await _notificationService.SendBroadcastMessageAsync(model, userId);

            TempData["SuccessMessage"] = "Broadcast message sent successfully!";
            return RedirectToAction(nameof(BroadcastMessages));
        }
    }
}
