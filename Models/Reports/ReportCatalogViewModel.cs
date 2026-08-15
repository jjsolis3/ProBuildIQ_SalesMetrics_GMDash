using System.Collections.Generic;
using SalesMetrics.Services.Reports;

namespace SalesMetrics.Models.Reports
{
    public class ReportCatalogViewModel
    {
        public List<IReportDefinition> AvailableReports { get; set; } = new();

        /// <summary>True if the current user is an admin (RoleId 1) — controls visibility of access mgmt button.</summary>
        public bool IsAdmin { get; set; }
    }
}
