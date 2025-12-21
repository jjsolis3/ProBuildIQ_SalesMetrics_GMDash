using System.Collections.Generic;

namespace SalesMetrics.Services.Reports
{
    public interface IReportDefinition
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        IReadOnlyCollection<string> AllowedRoles { get; }
        IReadOnlyCollection<string> AllowedLocations { get; }
        IReadOnlyCollection<ReportParameterDefinition> ParameterSchema { get; }
    }
}
