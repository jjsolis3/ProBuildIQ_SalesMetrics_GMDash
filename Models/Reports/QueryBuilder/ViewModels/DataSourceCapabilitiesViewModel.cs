namespace SalesMetrics.Models.Reports.QueryBuilder.ViewModels
{
    /// <summary>
    /// ViewModel for data source capabilities display
    /// </summary>
    public class DataSourceCapabilitiesViewModel
    {
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public SourceCapabilities Capabilities { get; set; } = new();

        // Feature Flags
        public List<FeatureFlag> Features { get; set; } = new();
    }

    public class FeatureFlag
    {
        public string Name { get; set; } = string.Empty;
        public bool IsSupported { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-check-circle"; // Bootstrap icon
    }
}
