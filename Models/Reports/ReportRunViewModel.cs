using System.Collections.Generic;
using SalesMetrics.Services.Reports;

namespace SalesMetrics.Models.Reports
{
    public class ReportRunViewModel
    {
        public IReportDefinition? Definition { get; set; }
        public ReportParameters Parameters { get; set; } = new();
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalRows { get; set; }
        public string? ErrorMessage { get; set; }
        public bool ShowResults { get; set; }
    }
}
