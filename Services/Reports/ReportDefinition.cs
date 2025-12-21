using System.Collections.Generic;

namespace SalesMetrics.Services.Reports
{
    public class ReportDefinition : IReportDefinition
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public IReadOnlyCollection<string> AllowedRoles { get; init; } = new List<string>();
        public IReadOnlyCollection<string> AllowedLocations { get; init; } = new List<string>();
        public IReadOnlyCollection<ReportParameterDefinition> ParameterSchema { get; init; } = new List<ReportParameterDefinition>();
    }
}
