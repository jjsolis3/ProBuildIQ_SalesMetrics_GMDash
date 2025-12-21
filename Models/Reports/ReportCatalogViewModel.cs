using System.Collections.Generic;
using SalesMetrics.Services.Reports;

namespace SalesMetrics.Models.Reports
{
    public class ReportCatalogViewModel
    {
        public List<IReportDefinition> AvailableReports { get; set; } = new();
    }
}
