namespace SalesMetrics.Models.Reports.QueryBuilder
{
    /// <summary>
    /// Describes what features a data source supports
    /// </summary>
    public class SourceCapabilities
    {
        public bool SupportsJoins { get; set; } = true;
        public bool SupportsAggregates { get; set; } = true;
        public bool SupportsSubqueries { get; set; } = false;
        public bool SupportsComplexFilters { get; set; } = true;
        public int MaxRowsPerQuery { get; set; } = 10000;
        public int QueryTimeoutSeconds { get; set; } = 30;

        public List<string> SupportedAggregates { get; set; } = new()
        {
            "SUM", "AVG", "COUNT", "MIN", "MAX"
        };

        public List<string> SupportedOperators { get; set; } = new()
        {
            "=", "!=", ">", "<", ">=", "<=", "LIKE", "IN", "BETWEEN", "IS NULL", "IS NOT NULL"
        };
    }
}
