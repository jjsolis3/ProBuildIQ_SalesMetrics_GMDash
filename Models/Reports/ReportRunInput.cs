namespace SalesMetrics.Models.Reports
{
    public class ReportRunInput
    {
        public string DefinitionId { get; set; } = string.Empty;
        public ReportParameters Parameters { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string ExportFormat { get; set; } = "csv";
    }
}
