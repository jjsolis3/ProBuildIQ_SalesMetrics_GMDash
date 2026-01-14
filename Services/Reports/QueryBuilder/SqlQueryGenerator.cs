using System.Text;
using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Services.Reports.QueryBuilder
{
    /// <summary>
    /// Generates SQL queries from QueryDefinition objects
    /// </summary>
    public class SqlQueryGenerator
    {
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

            sql.AppendLine($"FROM {baseTable.TableName} AS {baseTable.Alias}");

            // JOIN clauses
            var joinTables = queryDef.Tables.Where(t => !t.IsBaseTable).OrderBy(t => t.TableId);
            foreach (var table in joinTables)
            {
                if (!string.IsNullOrEmpty(table.JoinType) && !string.IsNullOrEmpty(table.JoinCondition))
                {
                    sql.AppendLine($"{table.JoinType} JOIN {table.TableName} AS {table.Alias} ON {table.JoinCondition}");
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
                sql.AppendLine($"GROUP BY {string.Join(", ", queryDef.GroupBy)}");
            }

            // ORDER BY clause
            if (queryDef.OrderBy != null && queryDef.OrderBy.Any())
            {
                sql.AppendLine("ORDER BY " + string.Join(", ",
                    queryDef.OrderBy.Select(o => $"{o.ColumnName} {o.Direction}")));
            }

            return sql.ToString();
        }

        private string BuildColumnExpression(ColumnDefinition col)
        {
            string expression = col.Expression;

            // Apply aggregate function if specified
            if (!string.IsNullOrEmpty(col.AggregateFunction))
            {
                expression = $"{col.AggregateFunction.ToUpper()}({expression})";
            }

            // Add alias
            if (!string.IsNullOrEmpty(col.Alias) && col.Alias != col.Expression)
            {
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
            string columnName = filter.ColumnName;
            string op = filter.Operator.ToUpper();
            string value = filter.IsParameter ? $"@{filter.ParameterName}" : FormatValue(filter.Value);

            return op switch
            {
                "BETWEEN" => $"{columnName} BETWEEN {value}",
                "IS NULL" => $"{columnName} IS NULL",
                "IS NOT NULL" => $"{columnName} IS NOT NULL",
                "IN" => $"{columnName} IN ({value})",
                "NOT IN" => $"{columnName} NOT IN ({value})",
                "LIKE" or "NOT LIKE" => $"{columnName} {op} {value}",
                _ => $"{columnName} {op} {value}"
            };
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
