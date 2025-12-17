using System.Data;
using System.Threading.Tasks;
using SalesMetrics.Models.Reports;

namespace SalesMetrics.Services.Reports
{
    public interface IReportRunner
    {
        Task<DataTable> RunAsync(string definitionId, ReportParameters parameters, ReportUserContext userContext);
    }
}
