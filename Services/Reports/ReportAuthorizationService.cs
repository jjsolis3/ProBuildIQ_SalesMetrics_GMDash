using System.Linq;
using SalesMetrics.Models.Reports;

namespace SalesMetrics.Services.Reports
{
    public class ReportAuthorizationService : IReportAuthorizationService
    {
        public bool IsUserAuthorized(IReportDefinition definition, ReportUserContext userContext)
        {
            if (definition == null || userContext == null)
            {
                return false;
            }

            var roleAllowed = !definition.AllowedRoles.Any() || definition.AllowedRoles.Contains(userContext.RoleId);
            var locationAllowed = !definition.AllowedLocations.Any() || definition.AllowedLocations.Contains(userContext.OfficeLocation);

            return roleAllowed && locationAllowed;
        }
    }
}
