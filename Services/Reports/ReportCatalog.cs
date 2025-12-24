using System.Collections.Generic;
using System.Linq;

namespace SalesMetrics.Services.Reports
{
    public class ReportCatalog : IReportCatalog
    {
        private readonly List<IReportDefinition> _definitions;

        public ReportCatalog()
        {
            _definitions = new List<IReportDefinition>
            {
                new ReportDefinition
                {
                    Id = "margin-commission-discrepancy-open-invoiced",
                    Name = "Margin & Commission Discrepancy (Open + Invoiced)",
                    Description = "Identify sales orders and invoices that miss commission and target margin thresholds.",
                    AllowedRoles = new List<string> { "1", "3", "4" },
                    AllowedLocations = new List<string> { "LAX", "LSV", "CHN", "PHX", "SND" },
                    ParameterSchema = new List<ReportParameterDefinition>
                    {
                        new() { Name = "FromDate", DisplayName = "From Date", Type = "date", IsRequired = true },
                        new() { Name = "ToDate", DisplayName = "To Date", Type = "date", IsRequired = true },
                        new() { Name = "MinMargin", DisplayName = "Min Margin", Type = "number", IsRequired = true },
                        new() { Name = "MaxMargin", DisplayName = "Max Margin", Type = "number", IsRequired = true },
                        new() { Name = "TargetMargin", DisplayName = "Target Margin", Type = "number", IsRequired = false },
                        new() { Name = "MgmtName", DisplayName = "Management Name", Type = "text", IsRequired = false },
                        new() { Name = "FilterByMgmt", DisplayName = "Filter By Management", Type = "checkbox", IsRequired = false },
                        new() { Name = "WarehouseId", DisplayName = "Warehouse", Type = "number", IsRequired = true }
                    }
                }
            };
        }

        public IEnumerable<IReportDefinition> GetAll() => _definitions;

        public IReportDefinition? GetById(string id)
        {
            return _definitions.FirstOrDefault(r => r.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
