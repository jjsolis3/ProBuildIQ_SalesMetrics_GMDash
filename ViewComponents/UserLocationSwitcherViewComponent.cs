using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Services.Helpers;
using System.Security.Claims;

namespace SalesMetrics.ViewComponents
{
    public class UserLocationSwitcherViewComponent : ViewComponent
    {
        private readonly SalesMetricsDbContext _context;

        public UserLocationSwitcherViewComponent(SalesMetricsDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync(string viewName = "Default")
        {
            var userId = int.Parse(HttpContext.User.FindFirstValue("Users_ID") ?? "0");
            var currentLocation = HttpContext.Session.GetString("OfficeLocation");

            var locations = _context.UserLocationAssignments
                .Where(a => a.UserID == userId && a.IsActive == "YES")
                .Select(a => a.LocationID)
                .Distinct()
                .ToList();

            var model = new UserLocationSwitcherViewModel
            {
                CurrentLocation = currentLocation ?? "LAX",
                AvailableLocations = locations.Select(loc => new SelectListItem
                {
                    Value = LocationHelper.GetBranchCode(loc), // e.g., "LAX", "PHX"
                    Text = LocationHelper.GetLocationName(loc), // e.g., "Los Angeles"
                    Selected = LocationHelper.GetBranchCode(loc) == currentLocation
                }).ToList()
            };

            //Console.WriteLine($"[DEBUG] Loaded {locations.Count} locations for user {userId}");

            return View(viewName, model);
        }
    }
}
