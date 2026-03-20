using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesMetrics.Models;
using SalesMetrics.Services;

//Admin interface for viewing errors

namespace SalesMetrics.Controllers
{
    [Authorize] // Adjust role as needed
    public class ErrorLogController : Controller
    {
        private readonly IErrorLoggingService _errorLoggingService;
        private readonly IConfiguration _configuration;

        public ErrorLogController(IErrorLoggingService errorLoggingService, IConfiguration configuration)
        {
            _errorLoggingService = errorLoggingService;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 50, string level = "All")
        {
            var errors = await _errorLoggingService.GetRecentErrorsAsync(pageSize * page);

            if (level != "All")
            {
                errors = errors.Where(e => e.Level == level).ToList();
            }

            ViewBag.CurrentLevel = level;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;

            return View(errors);
        }

        public async Task<IActionResult> Details(long id)
        {
            var errors = await _errorLoggingService.GetRecentErrorsAsync(1000); // Get more to find the specific one
            var error = errors.FirstOrDefault(e => e.ErrorID == id);

            if (error == null)
            {
                return NotFound();
            }

            return View(error);
        }

        [HttpPost]
        public async Task<IActionResult> MarkResolved(long id, string resolutionNotes)
        {
            var username = User.Identity?.Name ?? "Unknown";
            await _errorLoggingService.MarkAsResolvedAsync(id, username, resolutionNotes);

            TempData["Success"] = "Error marked as resolved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Quick close functionality for bulk operations
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> QuickClose(long id, string? returnUrl = null)
        {
            try
            {
                var username = HttpContext.Session.GetString("Username") ?? User.Identity?.Name ?? "Admin";

                await _errorLoggingService.MarkAsResolvedAsync(
                    id,
                    username,
                    "Known issue - Quick Close by Admin"
                );

                // Return JSON for AJAX calls, redirect for regular form posts
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Error resolved successfully" });
                }
                else
                {
                    TempData["Success"] = "Error marked as resolved.";

                    // Return to the specified URL or default locations
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    // Try to determine where to redirect based on referrer
                    var referer = Request.Headers["Referer"].ToString();
                    if (referer.Contains("Dashboard"))
                    {
                        return RedirectToAction(nameof(Dashboard));
                    }

                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't create a recursive loop
                Console.WriteLine($"Error in QuickClose: {ex.Message}");

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Failed to resolve error" });
                }
                else
                {
                    TempData["Error"] = "Failed to resolve error. Please try again.";
                    return RedirectToAction(nameof(Index));
                }
            }
        }

        /// <summary>
        /// Bulk quick close for multiple errors
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BulkQuickClose([FromBody] BulkQuickCloseRequest request)
        {
            try
            {
                if (request?.ErrorIds == null || !request.ErrorIds.Any())
                {
                    return Json(new { success = false, message = "No error IDs provided" });
                }

                var username = HttpContext.Session.GetString("Username") ?? User.Identity?.Name ?? "Admin";
                int successCount = 0;

                foreach (var id in request.ErrorIds)
                {
                    try
                    {
                        await _errorLoggingService.MarkAsResolvedAsync(
                            id,
                            username,
                            "Known issue - Bulk Quick Close by Admin"
                        );
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // Log individual failure but continue
                        Console.WriteLine($"Failed to resolve error {id}: {ex.Message}");
                        continue;
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Successfully resolved {successCount} of {request.ErrorIds.Length} errors"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in BulkQuickClose: {ex.Message}");
                return Json(new { success = false, message = "Bulk operation failed" });
            }
        }

        // Helper class for bulk operations
        public class BulkQuickCloseRequest
        {
            public long[] ErrorIds { get; set; } = Array.Empty<long>();
        }

        public async Task<IActionResult> Dashboard()
        {
            var recentErrors = await _errorLoggingService.GetRecentErrorsAsync(100);

            var dashboard = new ErrorDashboardViewModel
            {
                TotalErrors = recentErrors.Count,
                UnresolvedErrors = recentErrors.Count(e => !e.IsResolved),
                ErrorsByLevel = recentErrors
                    .GroupBy(e => e.Level)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ErrorsByController = recentErrors
                    .Where(e => !string.IsNullOrEmpty(e.Controller))
                    .GroupBy(e => e.Controller!)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count()),
                RecentErrors = recentErrors
                    .Take(10)
                    .Select(e => new ErrorLogEntry
                    {
                        ErrorID = (int)e.ErrorID,
                        Timestamp = e.Timestamp,
                        Level = e.Level,
                        Message = e.Message,
                        Controller = e.Controller ?? string.Empty,
                        IsResolved = e.IsResolved,
                    })
                    .ToList(),
                LoginSecurity = await GetLoginSecurityStatsAsync()
            };

            return View(dashboard);
        }

        private async Task<LoginSecurityStats> GetLoginSecurityStatsAsync()
        {
            var stats = new LoginSecurityStats();

            try
            {
                var connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Failed logins in last 24 h
                using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM LoginHistory
                    WHERE Success = 'Failed' AND LoginTime >= DATEADD(HOUR, -24, GETDATE())", conn))
                {
                    stats.FailedLoginsLast24h = (int)await cmd.ExecuteScalarAsync();
                }

                // Failed logins in last 7 days
                using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM LoginHistory
                    WHERE Success = 'Failed' AND LoginTime >= DATEADD(DAY, -7, GETDATE())", conn))
                {
                    stats.FailedLoginsLast7d = (int)await cmd.ExecuteScalarAsync();
                }

                // 20 most recent failures
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 20 LoginTime, UserName, IPAddress, office, ErrorLog
                    FROM LoginHistory
                    WHERE Success = 'Failed'
                    ORDER BY LoginTime DESC", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        stats.RecentFailures.Add(new LoginFailureEntry
                        {
                            LoginTime = reader.GetDateTime(reader.GetOrdinal("LoginTime")),
                            UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? "" : reader.GetString(reader.GetOrdinal("UserName")),
                            IpAddress = reader.IsDBNull(reader.GetOrdinal("IPAddress")) ? null : reader.GetString(reader.GetOrdinal("IPAddress")),
                            Office = reader.IsDBNull(reader.GetOrdinal("office")) ? null : reader.GetString(reader.GetOrdinal("office")),
                            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorLog")) ? null : reader.GetString(reader.GetOrdinal("ErrorLog"))
                        });
                    }
                }

                // Top IPs by failure count (last 7 days)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 10 IPAddress, COUNT(*) AS FailureCount
                    FROM LoginHistory
                    WHERE Success = 'Failed'
                      AND LoginTime >= DATEADD(DAY, -7, GETDATE())
                      AND IPAddress IS NOT NULL
                    GROUP BY IPAddress
                    ORDER BY FailureCount DESC", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        stats.TopFailedIps.Add(new TopFailedIpEntry
                        {
                            IpAddress = reader.GetString(reader.GetOrdinal("IPAddress")),
                            FailureCount = reader.GetInt32(reader.GetOrdinal("FailureCount"))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash the dashboard if LoginHistory is unavailable
                Console.WriteLine($"[ErrorLogController] Failed to load login security stats: {ex.Message}");
            }

            return stats;
        }
    }
}

// Extension methods for easier usage throughout your application
namespace SalesMetrics.Extensions
{
    public static class ControllerExtensions
    {
        public static async Task LogErrorAsync(this Controller controller, Exception exception, string? additionalData = null)
        {
            var errorLoggingService = controller.HttpContext.RequestServices.GetRequiredService<IErrorLoggingService>();
            await errorLoggingService.LogErrorAsync(exception, controller.HttpContext, additionalData);
        }

        public static async Task LogErrorAsync(this Controller controller, string message, string level = "Error", string? additionalData = null)
        {
            var errorLoggingService = controller.HttpContext.RequestServices.GetRequiredService<IErrorLoggingService>();
            await errorLoggingService.LogErrorAsync(message, level, controller.HttpContext, additionalData);
        }

        public static async Task LogWarningAsync(this Controller controller, string message, string? additionalData = null)
        {
            var errorLoggingService = controller.HttpContext.RequestServices.GetRequiredService<IErrorLoggingService>();
            await errorLoggingService.LogWarningAsync(message, controller.HttpContext, additionalData);
        }
    }
}