using System.Data;

namespace SalesMetrics.Services.Reports
{
    public interface IReportExportService
    {
        byte[] ExportToCsv(DataTable data, out string contentType);
        byte[] ExportToExcel(DataTable data, out string contentType);
        byte[] ExportToExcel(DataTable data, ExcelExportOptions options, out string contentType);
        /// <summary>
        /// Exports multiple DataTables into a single .xlsx file, one sheet per entry.
        /// Each tuple provides the sheet tab name and its data.
        /// </summary>
        byte[] ExportToExcelMultiSheet(IList<(string SheetName, DataTable Data)> sheets, ExcelExportOptions options, out string contentType);
    }

    public class ExcelExportOptions
    {
        public string ReportName { get; set; } = "Report";
        public string? ReportDescription { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyWebsite { get; set; }
        public string? CompanyPhone { get; set; }
        /// <summary>
        /// Absolute path to a local logo image file (PNG).
        /// </summary>
        public string? LogoFilePath { get; set; }
    }
}
