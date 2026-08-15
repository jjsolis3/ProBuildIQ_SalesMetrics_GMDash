using System.Text;
using System.Text.RegularExpressions;
using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Services.Reports.QueryBuilder
{
    /// <summary>
    /// Generates SQL queries from QueryDefinition objects.
    /// All structural identifiers (table names, column names, aliases) are validated
    /// against a safe-identifier whitelist before being interpolated into SQL to
    /// prevent SQL injection via user-controlled query definitions.
    /// </summary>
    public class SqlQueryGenerator
    {
        // Allows: letters, digits, underscore, dot (schema.table), square brackets
        private static readonly Regex _safeIdentifier =
            new(@"^\[?[a-zA-Z0-9_]+\]?(\.\[?[a-zA-Z0-9_]+\]?)*$", RegexOptions.Compiled);

        // JOIN types whitelist
        private static readonly HashSet<string> _allowedJoinTypes =
            new(StringComparer.OrdinalIgnoreCase) { "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "LEFT OUTER", "RIGHT OUTER", "FULL OUTER" };

        // Aggregate functions whitelist
        private static readonly HashSet<string> _allowedAggregateFunctions =
            new(StringComparer.OrdinalIgnoreCase) { "COUNT", "SUM", "AVG", "MIN", "MAX", "COUNT_BIG", "STDEV", "STDEVP", "VAR", "VARP" };

        // ORDER BY direction whitelist
        private static readonly HashSet<string> _allowedDirections =
            new(StringComparer.OrdinalIgnoreCase) { "ASC", "DESC" };

        // Comparison operator whitelist
        private static readonly HashSet<string> _allowedOperators =
            new(StringComparer.OrdinalIgnoreCase) { "=", "!=", "<>", "<", ">", "<=", ">=", "LIKE", "NOT LIKE", "IN", "NOT IN", "BETWEEN", "IS NULL", "IS NOT NULL" };

        private static string ValidateIdentifier(string value, string context)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Empty identifier is not allowed in {context}.");
            if (!_safeIdentifier.IsMatch(value))
                throw new ArgumentException($"Unsafe identifier '{value}' rejected in {context}. Only alphanumeric characters, underscores, dots, and square brackets are permitted.");
            return value;
        }

        public string GenerateSQL(QueryDefinition queryDef)
        {
            var sql = new StringBuilder();

            // SELECT clause
            sql.AppendLine("SELECT");
            sql.AppendLine(string.Join(",\n    ", queryDef.Columns.Select(BuildColumnExpression)));

            // FROM clause
            var baseTable = queryDef.Tables.FirstOrDefault(t => t.IsBaseTable);
            if (baseTable == null)
                throw new InvalidOperationException("No base table specified in query definition");

            ValidateIdentifier(baseTable.TableName, "FROM table");
            ValidateIdentifier(baseTable.Alias, "FROM alias");
            sql.AppendLine($"FROM {baseTable.TableName} AS {baseTable.Alias}");

            // JOIN clauses
            var joinTables = queryDef.Tables.Where(t => !t.IsBaseTable).OrderBy(t => t.TableId);
            foreach (var table in joinTables)
            {
                if (!string.IsNullOrEmpty(table.JoinType) && !string.IsNullOrEmpty(table.JoinCondition))
                {
                    var joinType = table.JoinType.ToUpperInvariant();
                    if (!_allowedJoinTypes.Contains(joinType))
                        throw new ArgumentException($"Unsupported JOIN type '{table.JoinType}'.");

                    ValidateIdentifier(table.TableName, "JOIN table");
                    ValidateIdentifier(table.Alias, "JOIN alias");

                    // JoinCondition is a structured expression (e.g. "a.ID = b.ID").
                    // Validate each dot-separated token individually.
                    ValidateJoinCondition(table.JoinCondition);

                    sql.AppendLine($"{joinType} JOIN {table.TableName} AS {table.Alias} ON {table.JoinCondition}");
                }
            }

            // WHERE clause
            if (queryDef.Filters != null && queryDef.Filters.Any())
            {
                sql.AppendLine("WHERE");
                sql.AppendLine(BuildWhereClause(queryDef.Filters));
            }

            // GROUP BY clause
            if (queryDef.GroupBy != null && queryDef.GroupBy.Any())
            {
                foreach (var col in queryDef.GroupBy)
                    ValidateIdentifier(col, "GROUP BY");
                sql.AppendLine($"GROUP BY {string.Join(", ", queryDef.GroupBy)}");
            }

            // ORDER BY clause
            if (queryDef.OrderBy != null && queryDef.OrderBy.Any())
            {
                var orderClauses = queryDef.OrderBy.Select(o =>
                {
                    ValidateIdentifier(o.ColumnName, "ORDER BY");
                    var dir = o.Direction?.ToUpperInvariant() ?? "ASC";
                    if (!_allowedDirections.Contains(dir))
                        throw new ArgumentException($"Invalid ORDER BY direction '{o.Direction}'.");
                    return $"{o.ColumnName} {dir}";
                });
                sql.AppendLine("ORDER BY " + string.Join(", ", orderClauses));
            }

            return sql.ToString();
        }

        private string BuildColumnExpression(ColumnDefinition col)
        {
            // Expression may be "alias.column" — validate identifier
            ValidateIdentifier(col.Expression, "SELECT column expression");

            string expression = col.Expression;

            // Apply aggregate function if specified
            if (!string.IsNullOrEmpty(col.AggregateFunction))
            {
                var func = col.AggregateFunction.ToUpperInvariant();
                if (!_allowedAggregateFunctions.Contains(func))
                    throw new ArgumentException($"Unsupported aggregate function '{col.AggregateFunction}'.");
                expression = $"{func}({expression})";
            }

            // Add alias
            if (!string.IsNullOrEmpty(col.Alias) && col.Alias != col.Expression)
            {
                ValidateIdentifier(col.Alias, "SELECT column alias");
                expression += $" AS {col.Alias}";
            }

            return $"    {expression}";
        }

        private string BuildWhereClause(List<FilterDefinition> filters)
        {
            var conditions = new List<string>();

            foreach (var filter in filters.OrderBy(f => f.GroupLevel).ThenBy(f => f.ColumnName))
            {
                string condition = BuildCondition(filter);
                conditions.Add(condition);
            }

            return "    " + string.Join($"\n    AND ", conditions);
        }

        private string BuildCondition(FilterDefinition filter)
        {
            ValidateIdentifier(filter.ColumnName, "WHERE column");

            string columnName = filter.ColumnName;
            string op = filter.Operator.ToUpperInvariant();

            if (!_allowedOperators.Contains(op))
                throw new ArgumentException($"Unsupported filter operator '{filter.Operator}'.");

            // Parameter names must also be safe identifiers (alphanumeric + underscore)
            if (filter.IsParameter && !Regex.IsMatch(filter.ParameterName ?? "", @"^[a-zA-Z0-9_]+$"))
                throw new ArgumentException($"Unsafe parameter name '{filter.ParameterName}' in WHERE clause.");

            string value = filter.IsParameter ? $"@{filter.ParameterName}" : FormatValue(filter.Value);

            return op switch
            {
                "BETWEEN"     => $"{columnName} BETWEEN {value}",
                "IS NULL"     => $"{columnName} IS NULL",
                "IS NOT NULL" => $"{columnName} IS NOT NULL",
                "IN"          => $"{columnName} IN ({value})",
                "NOT IN"      => $"{columnName} NOT IN ({value})",
                "LIKE" or "NOT LIKE" => $"{columnName} {op} {value}",
                _             => $"{columnName} {op} {value}"
            };
        }

        private static void ValidateJoinCondition(string condition)
        {
            // A join condition looks like: "alias.column = alias.column"
            // Split on whitespace and validate each token that looks like an identifier.
            var tokens = condition.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                // Skip comparison operators between identifiers
                if (token is "=" or "<>" or "!=" or "<" or ">" or "<=" or ">=" or "AND" or "OR" or "ON")
                    continue;
                // Validate everything else as a safe identifier
                if (!_safeIdentifier.IsMatch(token))
                    throw new ArgumentException($"Unsafe token '{token}' in JOIN condition.");
            }
        }

        private string FormatValue(string? value)
        {
            if (value == null) return "NULL";
            if (decimal.TryParse(value, out _) || int.TryParse(value, out _))
                return value;
            return $"'{value.Replace("'", "''")}'"; // Escape single quotes
        }
    }
}
