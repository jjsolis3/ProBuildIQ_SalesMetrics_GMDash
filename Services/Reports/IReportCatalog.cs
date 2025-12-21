using System.Collections.Generic;

namespace SalesMetrics.Services.Reports
{
    public interface IReportCatalog
    {
        IEnumerable<IReportDefinition> GetAll();
        IReportDefinition? GetById(string id);
    }
}
