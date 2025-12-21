using SalesMetrics.Models.Reports;

namespace SalesMetrics.Services.Reports
{
    public interface IReportAuthorizationService
    {
        bool IsUserAuthorized(IReportDefinition definition, ReportUserContext userContext);
    }
}
