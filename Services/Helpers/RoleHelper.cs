using System.Security.Claims;

namespace SalesMetrics.Services.Helpers
{
    public static class RoleHelper
    {
        public static bool CanSwitchLocation(ClaimsPrincipal user)
        {
            var roleId = user.FindFirst("RoleId")?.Value;
            return roleId == "1" || roleId == "4" || user.IsInRole("Admin") || user.IsInRole("General Manager");
        }
    }

}
