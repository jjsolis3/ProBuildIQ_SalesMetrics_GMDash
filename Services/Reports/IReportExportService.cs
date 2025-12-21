using System.Data;

namespace SalesMetrics.Services.Reports
{
    public interface IReportExportService
    {
        byte[] ExportToCsv(DataTable data, out string contentType);
        byte[] ExportToExcel(DataTable data, out string contentType);
    }
}
