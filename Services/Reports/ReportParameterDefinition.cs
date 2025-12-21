namespace SalesMetrics.Services.Reports
{
    public class ReportParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public bool IsRequired { get; set; }
        public string? Description { get; set; }
    }
}
