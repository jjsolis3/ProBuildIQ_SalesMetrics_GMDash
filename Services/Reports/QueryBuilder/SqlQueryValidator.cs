using System.Text.RegularExpressions;
using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Services.Reports.QueryBuilder
{
    /// <summary>
    /// Validates SQL queries for security and correctness
    /// </summary>
    public class SqlQueryValidator
    {
        private static readonly string[] BlockedKeywords = new[]
        {
            "DELETE", "UPDATE", "INSERT", "DROP", "ALTER", "CREATE", "TRUNCATE",
            "EXEC", "EXECUTE", "SP_", "XP_", "OPENROWSET", "OPENDATASOURCE",
            "BULK", "BACKUP", "RESTORE", "GRANT", "REVOKE", "DENY",
            "SHUTDOWN", "KILL", "WAITFOR", "DBCC"
        };

        public async Task<ValidationResult> ValidateAsync(string sql)
        {
            return await Task.Run(() => Validate(sql));
        }

        public ValidationResult Validate(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return ValidationResult.Fail("SQL query cannot be empty");

            // 1. Must start with SELECT
            if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Fail("Only SELECT queries are allowed");

            // 2. Check for blocked keywords
            foreach (var keyword in BlockedKeywords)
            {
                if (Regex.IsMatch(sql, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
                    return ValidationResult.Fail($"Forbidden keyword detected: {keyword}");
            }

            // 3. Check for comment injection attempts
            if (sql.Contains("--") || sql.Contains("/*"))
                return ValidationResult.Fail("SQL comments are not allowed");

            // 4. Check for multiple statements (semicolons in wrong places)
            var semicolonCount = sql.Count(c => c == ';');
            if (semicolonCount > 1 || (semicolonCount == 1 && !sql.TrimEnd().EndsWith(';')))
                return ValidationResult.Fail("Multiple SQL statements are not allowed");

            // 5. Check for dynamic SQL attempts
            if (Regex.IsMatch(sql, @"\bEXEC\s*\(", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(sql, @"\bEXECUTE\s*\(", RegexOptions.IgnoreCase))
                return ValidationResult.Fail("Dynamic SQL execution is not allowed");

            // 6. Warn about potential performance issues
            var warnings = new List<string>();

            if (!Regex.IsMatch(sql, @"\bWHERE\b", RegexOptions.IgnoreCase))
                warnings.Add("Query has no WHERE clause - may return large result set");

            if (Regex.IsMatch(sql, @"SELECT\s+\*", RegexOptions.IgnoreCase))
                warnings.Add("Using SELECT * - consider selecting specific columns");

            if (warnings.Any())
            {
                var result = ValidationResult.Success();
                result.Warnings = warnings;
                return result;
            }

            return ValidationResult.Success();
        }
    }
}
