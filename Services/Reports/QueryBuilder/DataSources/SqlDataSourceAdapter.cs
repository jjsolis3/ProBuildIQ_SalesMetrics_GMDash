using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesMetrics.Models.Reports;
using SalesMetrics.Models.Reports.QueryBuilder;
using SalesMetrics.Services.Erp;

namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Data source adapter for SQL Server databases (CompUFloor ERP)
    /// </summary>
    public class SqlDataSourceAdapter : IDataSourceAdapter
    {
        private readonly IConfiguration _config;
        private readonly ErpClientFactory _erpFactory;
        private readonly ILogger<SqlDataSourceAdapter> _logger;

        public string SourceType => "SQL";
        public string SourceName => "CompUFloor ERP (SQL Server)";
        public bool IsAvailable => CheckDatabaseConnection();

        public SqlDataSourceAdapter(
            IConfiguration config,
            ErpClientFactory erpFactory,
            ILogger<SqlDataSourceAdapter> logger)
        {
            _config = config;
            _erpFactory = erpFactory;
            _logger = logger;
        }

        public async Task<List<TableMetadata>> GetAvailableTablesAsync(DataSourceContext context)
        {
            try
            {
                string sql = @"
                    SELECT
                        TABLE_SCHEMA as SchemaName,
                        TABLE_NAME as TableName,
                        TABLE_TYPE as SourceType
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    AND TABLE_SCHEMA = 'dbo'
                    ORDER BY TABLE_NAME";

                using var connection = GetConnection(context.LocationCode);
                var tables = await connection.QueryAsync<TableMetadata>(sql);

                // Set display names (default to table name, can be overridden by whitelist)
                foreach (var table in tables)
                {
                    table.DisplayName = FormatTableName(table.TableName);
                    table.IsVirtual = false;
                }

                return tables.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tables from SQL database for location {Location}", context.LocationCode);
                throw;
            }
        }

        public async Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context)
        {
            try
            {
                string sql = @"
                    SELECT
                        COLUMN_NAME as ColumnName,
                        DATA_TYPE as NativeDataType,
                        IS_NULLABLE as IsNullable,
                        CHARACTER_MAXIMUM_LENGTH as MaxLength,
                        ORDINAL_POSITION as OrdinalPosition
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName
                    AND TABLE_SCHEMA = 'dbo'
                    ORDER BY ORDINAL_POSITION";

                using var connection = GetConnection(context.LocationCode);
                var rawColumns = await connection.QueryAsync(sql, new { TableName = tableName });

                var columns = new List<ColumnMetadata>();
                foreach (var col in rawColumns)
                {
                    var columnMeta = new ColumnMetadata
                    {
                        ColumnName = col.ColumnName,
                        DisplayName = FormatColumnName(col.ColumnName),
                        NativeDataType = col.NativeDataType,
                        DataType = NormalizeDataType(col.NativeDataType),
                        IsFilterable = true,
                        IsSortable = true,
                        IsAggregatable = IsNumericType(col.NativeDataType),
                        IsSensitive = IsSensitiveColumn(col.ColumnName)
                    };

                    columns.Add(columnMeta);
                }

                return columns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching columns for table {TableName} in location {Location}",
                    tableName, context.LocationCode);
                throw;
            }
        }

        public async Task<List<RelationshipMetadata>> GetTableRelationshipsAsync(string tableName, DataSourceContext context)
        {
            try
            {
                // Query SQL Server foreign key metadata
                string sql = @"
                    SELECT
                        fk.name AS ConstraintName,
                        tp.name AS FromTable,
                        cp.name AS FromColumn,
                        tr.name AS ToTable,
                        cr.name AS ToColumn
                    FROM sys.foreign_keys AS fk
                    INNER JOIN sys.foreign_key_columns AS fkc
                        ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.tables AS tp
                        ON fkc.parent_object_id = tp.object_id
                    INNER JOIN sys.columns AS cp
                        ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
                    INNER JOIN sys.tables AS tr
                        ON fkc.referenced_object_id = tr.object_id
                    INNER JOIN sys.columns AS cr
                        ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
                    WHERE tp.name = @TableName OR tr.name = @TableName
                    ORDER BY tp.name, tr.name";

                using var connection = GetConnection(context.LocationCode);
                var rawRelationships = await connection.QueryAsync(sql, new { TableName = tableName });

                var relationships = new List<RelationshipMetadata>();
                foreach (var rel in rawRelationships)
                {
                    relationships.Add(new RelationshipMetadata
                    {
                        FromTable = rel.FromTable,
                        FromColumn = rel.FromColumn,
                        ToTable = rel.ToTable,
                        ToColumn = rel.ToColumn,
                        RelationshipType = "ONE_TO_MANY", // Could be enhanced to detect type
                        DisplayName = $"{rel.FromTable}.{rel.FromColumn} → {rel.ToTable}.{rel.ToColumn}"
                    });
                }

                return relationships;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching relationships for table {TableName}", tableName);
                return new List<RelationshipMetadata>(); // Return empty list on error
            }
        }

        public async Task<DataTable> ExecuteQueryAsync(
            QueryDefinition queryDef,
            ReportParameters parameters,
            DataSourceContext context)
        {
            try
            {
                // 1. Generate SQL from QueryDefinition
                var sqlGenerator = new SqlQueryGenerator();
                string sql = sqlGenerator.GenerateSQL(queryDef);

                _logger.LogInformation("Generated SQL: {Sql}", sql);

                // 2. Validate SQL
                var validator = new SqlQueryValidator();
                var validationResult = await validator.ValidateAsync(sql);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Invalid query: {validationResult.ErrorMessage}");
                }

                // 3. Execute query
                var dataTable = new DataTable();
                using var connection = GetConnection(context.LocationCode);
                using var command = connection.CreateCommand();

                command.CommandText = sql;
                command.CommandTimeout = GetCapabilities().QueryTimeoutSeconds;

                // Add parameters
                AddParameters(command, parameters);

                connection.Open();
                using var adapter = new SqlDataAdapter((SqlCommand)command);
                adapter.Fill(dataTable);

                _logger.LogInformation("Query executed successfully. Returned {RowCount} rows", dataTable.Rows.Count);

                return dataTable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query for report {ReportId}", queryDef.ReportId);
                throw;
            }
        }

        public async Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context)
        {
            try
            {
                // 1. Validate all tables exist
                var availableTables = await GetAvailableTablesAsync(context);
                var tableNames = availableTables.Select(t => t.TableName.ToLower()).ToHashSet();

                foreach (var tableRef in queryDef.Tables)
                {
                    if (!tableNames.Contains(tableRef.TableName.ToLower()))
                    {
                        return ValidationResult.Fail($"Table '{tableRef.TableName}' does not exist in database");
                    }
                }

                // 2. Validate columns exist
                foreach (var col in queryDef.Columns)
                {
                    if (col.Expression.Contains('.'))
                    {
                        var parts = col.Expression.Split('.');
                        if (parts.Length >= 2)
                        {
                            var tableName = parts[0];
                            var columnName = parts[1];

                            // Extract table name from alias if needed
                            var table = queryDef.Tables.FirstOrDefault(t =>
                                t.Alias.Equals(tableName, StringComparison.OrdinalIgnoreCase));

                            if (table != null)
                            {
                                var columns = await GetTableColumnsAsync(table.TableName, context);
                                if (!columns.Any(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    return ValidationResult.Fail($"Column '{columnName}' does not exist in table '{table.TableName}'");
                                }
                            }
                        }
                    }
                }

                // 3. Validate aggregate functions are supported
                var capabilities = GetCapabilities();
                foreach (var col in queryDef.Columns)
                {
                    if (col.AggregateFunction != null &&
                        !capabilities.SupportedAggregates.Contains(col.AggregateFunction.ToUpper()))
                    {
                        return ValidationResult.Fail($"Aggregate function '{col.AggregateFunction}' is not supported");
                    }
                }

                // 4. Validate operators
                foreach (var filter in queryDef.Filters)
                {
                    if (!capabilities.SupportedOperators.Contains(filter.Operator.ToUpper()))
                    {
                        return ValidationResult.Fail($"Operator '{filter.Operator}' is not supported");
                    }
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating query");
                return ValidationResult.Fail($"Validation error: {ex.Message}");
            }
        }

        public SourceCapabilities GetCapabilities()
        {
            return new SourceCapabilities
            {
                SupportsJoins = true,
                SupportsAggregates = true,
                SupportsSubqueries = true,
                SupportsComplexFilters = true,
                MaxRowsPerQuery = 10000,
                QueryTimeoutSeconds = 30,
                SupportedAggregates = new List<string>
                {
                    "SUM", "AVG", "COUNT", "MIN", "MAX", "COUNT_DISTINCT", "STDEV", "VAR"
                },
                SupportedOperators = new List<string>
                {
                    "=", "!=", "<>", ">", "<", ">=", "<=", "LIKE", "NOT LIKE",
                    "IN", "NOT IN", "BETWEEN", "IS NULL", "IS NOT NULL"
                }
            };
        }

        #region Helper Methods

        private IDbConnection GetConnection(string locationCode)
        {
            var connectionString = _config.GetConnectionString(locationCode);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string not found for location: {locationCode}");
            }
            return new SqlConnection(connectionString);
        }

        private bool CheckDatabaseConnection()
        {
            try
            {
                // Try to connect to default location (LAX)
                using var conn = GetConnection("LAX");
                conn.Open();
                conn.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string NormalizeDataType(string sqlDataType)
        {
            return sqlDataType.ToLower() switch
            {
                "varchar" or "nvarchar" or "char" or "nchar" or "text" or "ntext" => "string",
                "int" or "bigint" or "smallint" or "tinyint" => "number",
                "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney" => "number",
                "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or "time" => "date",
                "bit" => "boolean",
                "uniqueidentifier" => "string",
                "varbinary" or "binary" or "image" => "binary",
                _ => "string"
            };
        }

        private bool IsNumericType(string sqlDataType)
        {
            var normalized = NormalizeDataType(sqlDataType);
            return normalized == "number";
        }

        private bool IsSensitiveColumn(string columnName)
        {
            var sensitiveKeywords = new[]
            {
                "password", "pwd", "secret", "ssn", "social", "creditcard",
                "cvv", "pin", "salt", "hash", "token", "apikey"
            };

            return sensitiveKeywords.Any(keyword =>
                columnName.ToLower().Contains(keyword));
        }

        private string FormatTableName(string tableName)
        {
            // Convert PascalCase or snake_case to "Readable Name"
            // Example: "CustomerOrders" -> "Customer Orders"
            // Example: "customer_orders" -> "Customer Orders"

            if (tableName.Contains('_'))
            {
                return string.Join(" ", tableName.Split('_')
                    .Select(part => char.ToUpper(part[0]) + part.Substring(1).ToLower()));
            }

            // Insert space before capital letters
            return System.Text.RegularExpressions.Regex.Replace(tableName, "([A-Z])", " $1").Trim();
        }

        private string FormatColumnName(string columnName)
        {
            return FormatTableName(columnName); // Same logic for now
        }

        private void AddParameters(IDbCommand command, ReportParameters parameters)
        {
            if (parameters == null) return;

            // Add FromDate
            if (parameters.FromDate.HasValue)
            {
                var param = command.CreateParameter();
                param.ParameterName = "@FromDate";
                param.Value = parameters.FromDate.Value;
                command.Parameters.Add(param);
            }

            // Add ToDate
            if (parameters.ToDate.HasValue)
            {
                var param = command.CreateParameter();
                param.ParameterName = "@ToDate";
                param.Value = parameters.ToDate.Value;
                command.Parameters.Add(param);
            }

            // Add MinMargin
            if (parameters.MinMargin.HasValue)
            {
                var param = command.CreateParameter();
                param.ParameterName = "@MinMargin";
                param.Value = parameters.MinMargin.Value;
                command.Parameters.Add(param);
            }

            // Add TargetMargin
            if (parameters.TargetMargin.HasValue)
            {
                var param = command.CreateParameter();
                param.ParameterName = "@TargetMargin";
                param.Value = parameters.TargetMargin.Value;
                command.Parameters.Add(param);
            }

            // Add Location
            if (!string.IsNullOrEmpty(parameters.Location))
            {
                var param = command.CreateParameter();
                param.ParameterName = "@Location";
                param.Value = parameters.Location;
                command.Parameters.Add(param);
            }

            // Add WarehouseId
            if (parameters.WarehouseId.HasValue)
            {
                var param = command.CreateParameter();
                param.ParameterName = "@WarehouseId";
                param.Value = parameters.WarehouseId.Value;
                command.Parameters.Add(param);
            }
        }

        #endregion
    }
}
