namespace SalesMetrics.Models.Reports.QueryBuilder
{
    /// <summary>
    /// Metadata about relationships between tables for auto-suggesting joins
    /// </summary>
    public class RelationshipMetadata
    {
        public string FromTable { get; set; } = string.Empty;
        public string ToTable { get; set; } = string.Empty;
        public string FromColumn { get; set; } = string.Empty;
        public string ToColumn { get; set; } = string.Empty;
        public string RelationshipType { get; set; } = "ONE_TO_MANY";
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
    }
}
