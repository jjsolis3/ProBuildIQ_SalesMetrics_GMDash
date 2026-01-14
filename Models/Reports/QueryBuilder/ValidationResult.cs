namespace SalesMetrics.Models.Reports.QueryBuilder
{
    /// <summary>
    /// Result of query validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Fail(string errorMessage) => new()
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };

        public static ValidationResult Warning(string warningMessage) => new()
        {
            IsValid = true,
            Warnings = new List<string> { warningMessage }
        };
    }
}
