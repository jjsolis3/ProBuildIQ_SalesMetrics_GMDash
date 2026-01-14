namespace SalesMetrics.Models.Reports.QueryBuilder
{
    /// <summary>
    /// Context information for data source operations
    /// </summary>
    public class DataSourceContext
    {
        public string UserId { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public Dictionary<string, string> ConnectionProperties { get; set; } = new();
    }
}
