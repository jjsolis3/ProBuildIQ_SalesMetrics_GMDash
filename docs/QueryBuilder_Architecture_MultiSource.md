# Query Builder Architecture - Multi-Source Data Support

## Executive Summary

This enhanced architecture extends the Query Builder to support multiple data sources:
- **Direct SQL databases** (current CompUFloor ERP)
- **REST/GraphQL APIs** (API-based ERPs)
- **Future ERP systems** (Kudu or others)
- **Hybrid reports** (combining multiple sources)

The key design principle: **Data Source Abstraction Layer** - Query Builder generates logical queries that adapters translate to source-specific implementations.

---

## 1. Architecture Layers (Enhanced)

```
┌────────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                           │
│  ┌──────────────┐  ┌───────────────┐  ┌──────────────┐        │
│  │ Query Builder│  │ Report Config │  │   Preview    │        │
│  │  Interface   │  │      UI       │  │   Results    │        │
│  └──────────────┘  └───────────────┘  └──────────────┘        │
└────────────────────────────────────────────────────────────────┘
                            ↓ ↑
┌────────────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                             │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ ReportBuilderController                                   │ │
│  ├──────────────────────────────────────────────────────────┤ │
│  │ • Query Definition Service (source-agnostic)             │ │
│  │ • Report Definition Service                               │ │
│  │ • Authorization Service                                   │ │
│  └──────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
                            ↓ ↑
┌────────────────────────────────────────────────────────────────┐
│              DATA SOURCE ABSTRACTION LAYER (NEW!)               │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ IDataSourceAdapter (Interface)                            │ │
│  │ • GetAvailableTables()                                    │ │
│  │ • GetTableColumns(tableName)                              │ │
│  │ • ExecuteQuery(queryDefinition) → DataTable               │ │
│  │ • ValidateQuery(queryDefinition) → ValidationResult       │ │
│  │ • GetCapabilities() → SourceCapabilities                  │ │
│  └──────────────────────────────────────────────────────────┘ │
│                              ↓                                  │
│  ┌──────────────┐  ┌─────────────┐  ┌──────────────┐         │
│  │ SQL Adapter  │  │ API Adapter │  │ Kudu Adapter │         │
│  │ (CompUFloor) │  │ (REST/GQL)  │  │  (Future)    │         │
│  └──────────────┘  └─────────────┘  └──────────────┘         │
└────────────────────────────────────────────────────────────────┘
                            ↓ ↑
┌────────────────────────────────────────────────────────────────┐
│                     DATA SOURCES                                │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ CompUFloor  │  │  External    │  │  Kudu ERP    │         │
│  │   SQL DBs   │  │     APIs     │  │   (Future)   │         │
│  └─────────────┘  └──────────────┘  └──────────────┘         │
└────────────────────────────────────────────────────────────────┘
```

---

## 2. Core Abstraction: IDataSourceAdapter

### Interface Definition

```csharp
namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Abstraction layer for different data sources (SQL, API, etc.)
    /// </summary>
    public interface IDataSourceAdapter
    {
        // Metadata Discovery
        Task<List<TableMetadata>> GetAvailableTablesAsync(DataSourceContext context);
        Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context);
        Task<List<RelationshipMetadata>> GetTableRelationshipsAsync(string tableName, DataSourceContext context);

        // Query Execution
        Task<DataTable> ExecuteQueryAsync(QueryDefinition queryDef, ReportParameters parameters, DataSourceContext context);

        // Validation
        Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context);

        // Capabilities
        SourceCapabilities GetCapabilities();

        // Source Info
        string SourceType { get; } // "SQL", "API", "Kudu"
        string SourceName { get; }
        bool IsAvailable { get; } // Check if source is reachable
    }
}
```

### Supporting Models

```csharp
public class DataSourceContext
{
    public string UserId { get; set; }
    public string LocationCode { get; set; }
    public int RoleId { get; set; }
    public Dictionary<string, string> ConnectionProperties { get; set; }
}

public class TableMetadata
{
    public string TableName { get; set; }
    public string SchemaName { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public bool IsVirtual { get; set; } // TRUE for API endpoints that act like tables
    public string SourceType { get; set; } // "Database", "API", "View", "Function"
}

public class ColumnMetadata
{
    public string ColumnName { get; set; }
    public string DisplayName { get; set; }
    public string DataType { get; set; } // Normalized: "string", "number", "date", "boolean"
    public string NativeDataType { get; set; } // Source-specific: "varchar(50)", "int", etc.
    public string Description { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsAggregatable { get; set; }
}

public class RelationshipMetadata
{
    public string FromTable { get; set; }
    public string ToTable { get; set; }
    public string FromColumn { get; set; }
    public string ToColumn { get; set; }
    public string RelationshipType { get; set; }
}

public class SourceCapabilities
{
    public bool SupportsJoins { get; set; }
    public bool SupportsAggregates { get; set; }
    public bool SupportsSubqueries { get; set; }
    public bool SupportsComplexFilters { get; set; }
    public int MaxRowsPerQuery { get; set; }
    public int QueryTimeoutSeconds { get; set; }
    public List<string> SupportedAggregates { get; set; } // ["SUM", "AVG", "COUNT", "MIN", "MAX"]
    public List<string> SupportedOperators { get; set; } // ["=", "!=", ">", "<", "LIKE", "IN"]
}
```

---

## 3. SQL Data Source Adapter (Current CompUFloor)

```csharp
namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    public class SqlDataSourceAdapter : IDataSourceAdapter
    {
        private readonly IConfiguration _config;
        private readonly ErpClientFactory _erpFactory;
        private readonly ILogger<SqlDataSourceAdapter> _logger;

        public string SourceType => "SQL";
        public string SourceName => "CompUFloor ERP";
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
            // Query INFORMATION_SCHEMA.TABLES
            string sql = @"
                SELECT
                    TABLE_SCHEMA,
                    TABLE_NAME,
                    TABLE_TYPE
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                AND TABLE_SCHEMA = 'dbo'
                ORDER BY TABLE_NAME";

            var connection = GetConnection(context.LocationCode);
            var tables = await connection.QueryAsync<TableMetadata>(sql);
            return tables.ToList();
        }

        public async Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context)
        {
            // Query INFORMATION_SCHEMA.COLUMNS
            string sql = @"
                SELECT
                    COLUMN_NAME as ColumnName,
                    DATA_TYPE as NativeDataType,
                    IS_NULLABLE,
                    CHARACTER_MAXIMUM_LENGTH
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                AND TABLE_SCHEMA = 'dbo'
                ORDER BY ORDINAL_POSITION";

            var connection = GetConnection(context.LocationCode);
            var columns = await connection.QueryAsync<ColumnMetadata>(sql, new { TableName = tableName });

            // Normalize data types
            foreach (var col in columns)
            {
                col.DataType = NormalizeDataType(col.NativeDataType);
                col.DisplayName = col.ColumnName; // Default, can be overridden by whitelist
                col.IsFilterable = true;
                col.IsSortable = true;
                col.IsAggregatable = IsNumericType(col.DataType);
            }

            return columns.ToList();
        }

        public async Task<DataTable> ExecuteQueryAsync(
            QueryDefinition queryDef,
            ReportParameters parameters,
            DataSourceContext context)
        {
            // 1. Generate SQL from QueryDefinition
            var sqlGenerator = new SqlQueryGenerator();
            string sql = sqlGenerator.GenerateSQL(queryDef);

            // 2. Validate SQL
            var validator = new SqlQueryValidator();
            var validationResult = validator.Validate(sql);
            if (!validationResult.IsValid)
                throw new InvalidOperationException($"Invalid query: {validationResult.ErrorMessage}");

            // 3. Execute
            var connection = GetConnection(context.LocationCode);
            var dataTable = new DataTable();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandTimeout = 30;

                // Add parameters
                foreach (var param in parameters.GetAll())
                {
                    cmd.Parameters.AddWithValue($"@{param.Key}", param.Value);
                }

                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dataTable);
                }
            }

            return dataTable;
        }

        public async Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context)
        {
            // Validate that all tables exist
            var availableTables = await GetAvailableTablesAsync(context);
            var tableNames = availableTables.Select(t => t.TableName.ToLower()).ToHashSet();

            foreach (var tableRef in queryDef.Tables)
            {
                if (!tableNames.Contains(tableRef.TableName.ToLower()))
                {
                    return ValidationResult.Fail($"Table '{tableRef.TableName}' does not exist in data source");
                }
            }

            // Validate columns exist
            foreach (var col in queryDef.Columns)
            {
                // Extract table name from column expression (e.g., "Orders.OrderAmount")
                var parts = col.Expression.Split('.');
                if (parts.Length == 2)
                {
                    var tableName = parts[0];
                    var columnName = parts[1];

                    var columns = await GetTableColumnsAsync(tableName, context);
                    if (!columns.Any(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return ValidationResult.Fail($"Column '{columnName}' does not exist in table '{tableName}'");
                    }
                }
            }

            return ValidationResult.Success();
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
                SupportedAggregates = new List<string> { "SUM", "AVG", "COUNT", "MIN", "MAX", "COUNT_DISTINCT" },
                SupportedOperators = new List<string> { "=", "!=", ">", "<", ">=", "<=", "LIKE", "IN", "BETWEEN", "IS NULL", "IS NOT NULL" }
            };
        }

        private string NormalizeDataType(string sqlDataType)
        {
            return sqlDataType.ToLower() switch
            {
                "varchar" or "nvarchar" or "char" or "nchar" or "text" or "ntext" => "string",
                "int" or "bigint" or "smallint" or "tinyint" => "number",
                "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney" => "number",
                "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" => "date",
                "bit" => "boolean",
                _ => "string"
            };
        }

        private bool IsNumericType(string normalizedType) => normalizedType == "number";

        private IDbConnection GetConnection(string locationCode)
        {
            var connectionString = _config.GetConnectionString(locationCode);
            return new SqlConnection(connectionString);
        }

        private bool CheckDatabaseConnection()
        {
            try
            {
                var conn = GetConnection("LAX");
                conn.Open();
                conn.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
```

---

## 4. API Data Source Adapter (For API-based ERPs)

```csharp
namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    public class ApiDataSourceAdapter : IDataSourceAdapter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<ApiDataSourceAdapter> _logger;

        public string SourceType => "API";
        public string SourceName => "External API";
        public bool IsAvailable => CheckApiAvailability().Result;

        public ApiDataSourceAdapter(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<ApiDataSourceAdapter> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public async Task<List<TableMetadata>> GetAvailableTablesAsync(DataSourceContext context)
        {
            // Option 1: Call API's metadata endpoint
            // GET /api/v1/metadata/tables

            var client = _httpClientFactory.CreateClient("ErpApi");
            var response = await client.GetAsync("/api/v1/metadata/tables");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to fetch tables: {response.StatusCode}");

            var tables = await response.Content.ReadFromJsonAsync<List<TableMetadata>>();

            // Mark all as virtual (API endpoints, not real tables)
            foreach (var table in tables)
            {
                table.IsVirtual = true;
                table.SourceType = "API";
            }

            return tables;
        }

        public async Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context)
        {
            // GET /api/v1/metadata/tables/{tableName}/columns

            var client = _httpClientFactory.CreateClient("ErpApi");
            var response = await client.GetAsync($"/api/v1/metadata/tables/{tableName}/columns");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to fetch columns: {response.StatusCode}");

            return await response.Content.ReadFromJsonAsync<List<ColumnMetadata>>();
        }

        public async Task<DataTable> ExecuteQueryAsync(
            QueryDefinition queryDef,
            ReportParameters parameters,
            DataSourceContext context)
        {
            // Convert QueryDefinition to API query format
            var apiQuery = ConvertToApiQuery(queryDef, parameters);

            // POST /api/v1/query
            var client = _httpClientFactory.CreateClient("ErpApi");
            var response = await client.PostAsJsonAsync("/api/v1/query", apiQuery);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Query failed: {response.StatusCode}");

            // API returns JSON array of objects
            var jsonResult = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(jsonResult);

            // Convert to DataTable
            return ConvertToDataTable(results, queryDef);
        }

        public async Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context)
        {
            // APIs typically have more limited query capabilities
            var capabilities = GetCapabilities();

            // Check if query uses unsupported features
            if (queryDef.Tables.Count > 1 && !capabilities.SupportsJoins)
            {
                return ValidationResult.Fail("This data source does not support joins");
            }

            // Check aggregates
            foreach (var col in queryDef.Columns)
            {
                if (col.AggregateFunction != null && !capabilities.SupportedAggregates.Contains(col.AggregateFunction))
                {
                    return ValidationResult.Fail($"Aggregate function '{col.AggregateFunction}' is not supported by this data source");
                }
            }

            // Validate tables and columns exist
            var tables = await GetAvailableTablesAsync(context);
            foreach (var tableRef in queryDef.Tables)
            {
                if (!tables.Any(t => t.TableName.Equals(tableRef.TableName, StringComparison.OrdinalIgnoreCase)))
                {
                    return ValidationResult.Fail($"Endpoint '{tableRef.TableName}' does not exist in API");
                }
            }

            return ValidationResult.Success();
        }

        public SourceCapabilities GetCapabilities()
        {
            return new SourceCapabilities
            {
                SupportsJoins = false, // Most APIs don't support joins - must join client-side
                SupportsAggregates = true, // API might support aggregation
                SupportsSubqueries = false,
                SupportsComplexFilters = false,
                MaxRowsPerQuery = 1000, // API pagination limit
                QueryTimeoutSeconds = 60,
                SupportedAggregates = new List<string> { "SUM", "AVG", "COUNT", "MIN", "MAX" },
                SupportedOperators = new List<string> { "=", "!=", ">", "<", ">=", "<=", "IN" }
            };
        }

        private object ConvertToApiQuery(QueryDefinition queryDef, ReportParameters parameters)
        {
            // Convert QueryDefinition to API-specific query format
            // Example: OData, GraphQL, custom REST format

            return new
            {
                resource = queryDef.Tables.FirstOrDefault()?.TableName,
                select = queryDef.Columns.Select(c => c.ColumnName).ToList(),
                filter = ConvertFiltersToOData(queryDef.Filters),
                orderBy = queryDef.OrderBy?.Select(o => $"{o.ColumnName} {o.Direction}").ToList(),
                top = 1000
            };
        }

        private string ConvertFiltersToOData(List<FilterDefinition> filters)
        {
            // Convert filters to OData $filter syntax
            // Example: "OrderDate ge 2025-01-01 and OrderDate le 2025-12-31"

            var filterStrings = new List<string>();
            foreach (var filter in filters)
            {
                var op = filter.Operator switch
                {
                    "=" => "eq",
                    "!=" => "ne",
                    ">" => "gt",
                    "<" => "lt",
                    ">=" => "ge",
                    "<=" => "le",
                    _ => "eq"
                };

                filterStrings.Add($"{filter.ColumnName} {op} {filter.Value}");
            }

            return string.Join(" and ", filterStrings);
        }

        private DataTable ConvertToDataTable(List<Dictionary<string, object>> results, QueryDefinition queryDef)
        {
            var dataTable = new DataTable();

            if (results.Count == 0)
                return dataTable;

            // Add columns
            foreach (var key in results[0].Keys)
            {
                dataTable.Columns.Add(key, typeof(object));
            }

            // Add rows
            foreach (var item in results)
            {
                var row = dataTable.NewRow();
                foreach (var kvp in item)
                {
                    row[kvp.Key] = kvp.Value ?? DBNull.Value;
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        private async Task<bool> CheckApiAvailability()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("ErpApi");
                var response = await client.GetAsync("/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
```

---

## 5. Kudu ERP Adapter (Future Implementation)

```csharp
namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    public class KuduDataSourceAdapter : IDataSourceAdapter
    {
        private readonly IKuduApiClient _kuduClient; // To be implemented when Kudu is deployed
        private readonly ILogger<KuduDataSourceAdapter> _logger;

        public string SourceType => "Kudu";
        public string SourceName => "Kudu ERP";
        public bool IsAvailable => _kuduClient?.IsConnected ?? false;

        public KuduDataSourceAdapter(IKuduApiClient kuduClient, ILogger<KuduDataSourceAdapter> logger)
        {
            _kuduClient = kuduClient;
            _logger = logger;
        }

        public async Task<List<TableMetadata>> GetAvailableTablesAsync(DataSourceContext context)
        {
            // Call Kudu-specific metadata API
            return await _kuduClient.GetEntitiesAsync(context);
        }

        public async Task<List<ColumnMetadata>> GetTableColumnsAsync(string tableName, DataSourceContext context)
        {
            return await _kuduClient.GetEntityFieldsAsync(tableName, context);
        }

        public async Task<DataTable> ExecuteQueryAsync(
            QueryDefinition queryDef,
            ReportParameters parameters,
            DataSourceContext context)
        {
            // Convert to Kudu query format
            var kuduQuery = ConvertToKuduQuery(queryDef);
            var results = await _kuduClient.ExecuteQueryAsync(kuduQuery, context);
            return ConvertToDataTable(results);
        }

        public async Task<ValidationResult> ValidateQueryAsync(QueryDefinition queryDef, DataSourceContext context)
        {
            // Kudu-specific validation
            return await _kuduClient.ValidateQueryAsync(queryDef, context);
        }

        public SourceCapabilities GetCapabilities()
        {
            return new SourceCapabilities
            {
                SupportsJoins = true, // Depends on Kudu's capabilities
                SupportsAggregates = true,
                SupportsSubqueries = false,
                SupportsComplexFilters = true,
                MaxRowsPerQuery = 5000,
                QueryTimeoutSeconds = 45,
                SupportedAggregates = new List<string> { "SUM", "AVG", "COUNT", "MIN", "MAX" },
                SupportedOperators = new List<string> { "=", "!=", ">", "<", ">=", "<=", "LIKE", "IN", "BETWEEN" }
            };
        }

        private object ConvertToKuduQuery(QueryDefinition queryDef)
        {
            // Convert QueryDefinition to Kudu-specific format
            // This will depend on Kudu's API design
            throw new NotImplementedException("Kudu adapter pending Kudu ERP deployment");
        }

        private DataTable ConvertToDataTable(object results)
        {
            // Convert Kudu results to DataTable
            throw new NotImplementedException("Kudu adapter pending Kudu ERP deployment");
        }
    }
}
```

---

## 6. Data Source Registry & Selection

### Registry Service

```csharp
namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    public interface IDataSourceRegistry
    {
        IDataSourceAdapter GetAdapter(string sourceType);
        IDataSourceAdapter GetDefaultAdapter();
        List<DataSourceInfo> GetAvailableDataSources();
        void RegisterAdapter(string sourceType, IDataSourceAdapter adapter);
    }

    public class DataSourceRegistry : IDataSourceRegistry
    {
        private readonly Dictionary<string, IDataSourceAdapter> _adapters = new();
        private readonly ILogger<DataSourceRegistry> _logger;

        public DataSourceRegistry(
            IEnumerable<IDataSourceAdapter> adapters,
            ILogger<DataSourceRegistry> logger)
        {
            _logger = logger;

            // Auto-register all injected adapters
            foreach (var adapter in adapters)
            {
                RegisterAdapter(adapter.SourceType, adapter);
                _logger.LogInformation($"Registered data source adapter: {adapter.SourceType} - {adapter.SourceName}");
            }
        }

        public IDataSourceAdapter GetAdapter(string sourceType)
        {
            if (!_adapters.ContainsKey(sourceType))
                throw new ArgumentException($"Data source adapter '{sourceType}' not registered");

            return _adapters[sourceType];
        }

        public IDataSourceAdapter GetDefaultAdapter()
        {
            // Default to SQL (current CompUFloor)
            return GetAdapter("SQL");
        }

        public List<DataSourceInfo> GetAvailableDataSources()
        {
            return _adapters.Values
                .Where(a => a.IsAvailable)
                .Select(a => new DataSourceInfo
                {
                    SourceType = a.SourceType,
                    SourceName = a.SourceName,
                    IsAvailable = a.IsAvailable,
                    Capabilities = a.GetCapabilities()
                })
                .ToList();
        }

        public void RegisterAdapter(string sourceType, IDataSourceAdapter adapter)
        {
            _adapters[sourceType] = adapter;
        }
    }

    public class DataSourceInfo
    {
        public string SourceType { get; set; }
        public string SourceName { get; set; }
        public bool IsAvailable { get; set; }
        public SourceCapabilities Capabilities { get; set; }
    }
}
```

---

## 7. Enhanced Database Schema

### Add DataSource columns to existing tables:

```sql
-- Add DataSource column to ReportDefinitions
ALTER TABLE ReportDefinitions
ADD DataSourceType NVARCHAR(50) NOT NULL DEFAULT 'SQL', -- "SQL", "API", "Kudu", "Hybrid"
    DataSourceConfig NVARCHAR(MAX); -- JSON config for source-specific settings

-- Add DataSource info to AllowedTables
ALTER TABLE AllowedTables
ADD DataSourceType NVARCHAR(50) NOT NULL DEFAULT 'SQL',
    ApiEndpoint NVARCHAR(500), -- For API sources: "/api/v1/orders"
    ApiMethod NVARCHAR(10); -- "GET", "POST"

-- New table for data source configurations
CREATE TABLE DataSourceConfigurations (
    DataSourceConfigId INT IDENTITY(1,1) PRIMARY KEY,
    DataSourceType NVARCHAR(50) NOT NULL, -- "SQL", "API", "Kudu"
    ConfigName NVARCHAR(100) NOT NULL,
    DisplayName NVARCHAR(255) NOT NULL,

    -- Connection Info
    ConnectionString NVARCHAR(1000), -- For SQL sources
    BaseUrl NVARCHAR(500), -- For API sources
    AuthenticationType NVARCHAR(50), -- "None", "Basic", "Bearer", "OAuth2", "ApiKey"
    AuthenticationConfig NVARCHAR(MAX), -- JSON with auth details

    -- Settings
    IsActive BIT NOT NULL DEFAULT 1,
    IsDefault BIT NOT NULL DEFAULT 0,
    TimeoutSeconds INT NOT NULL DEFAULT 30,
    MaxRowsPerQuery INT NOT NULL DEFAULT 10000,

    -- Audit
    CreatedByUserId INT NOT NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    ModifiedByUserId INT NULL,
    ModifiedDate DATETIME NULL,

    FOREIGN KEY (CreatedByUserId) REFERENCES Users(Users_ID),
    FOREIGN KEY (ModifiedByUserId) REFERENCES Users(Users_ID),
    UNIQUE (DataSourceType, ConfigName)
);

-- Seed data
INSERT INTO DataSourceConfigurations (DataSourceType, ConfigName, DisplayName, IsActive, IsDefault, CreatedByUserId)
VALUES
    ('SQL', 'CompUFloor', 'CompUFloor ERP (SQL)', 1, 1, 1),
    ('API', 'ExternalAPI', 'External API (REST)', 1, 0, 1),
    ('Kudu', 'KuduERP', 'Kudu ERP (Future)', 0, 0, 1);
```

---

## 8. Enhanced Query Definition JSON

```json
{
  "reportId": "multi-source-sales-report",
  "version": 1,
  "dataSource": {
    "type": "SQL",
    "configName": "CompUFloor",
    "fallbackSources": ["API"] // Optional: try API if SQL fails
  },
  "tables": [
    {
      "tableId": 1,
      "tableName": "Orders",
      "alias": "o",
      "isBaseTable": true,
      "sourceType": "SQL" // Explicitly mark source
    },
    {
      "tableId": 2,
      "tableName": "Salesmen",
      "alias": "s",
      "joinType": "INNER",
      "joinCondition": "o.SalesmanNumber = s.SalesmanNumber",
      "sourceType": "SQL"
    }
  ],
  "columns": [
    {
      "expression": "s.SalesmanName",
      "alias": "Salesman",
      "sourceType": "SQL"
    },
    {
      "expression": "SUM(o.OrderAmount)",
      "alias": "TotalSales",
      "dataType": "currency"
    }
  ]
}
```

---

## 9. Report Builder UI - Data Source Selection

### Enhanced Create Report Flow:

```
Step 1: Select Data Source
┌─────────────────────────────────────────────────────┐
│  Choose Data Source for This Report                 │
│                                                      │
│  ○ CompUFloor ERP (SQL) [Default] ✓ Available      │
│    - Full query support, fastest performance        │
│                                                      │
│  ○ External API (REST) ✓ Available                 │
│    - Limited query features, slower                  │
│                                                      │
│  ○ Kudu ERP (Future) ✗ Not Available               │
│    - Coming soon when Kudu is deployed               │
│                                                      │
│  [Continue]                                          │
└─────────────────────────────────────────────────────┘

Step 2: Select Tables (source-specific)
┌─────────────────────────────────────────────────────┐
│  Available Tables from CompUFloor ERP               │
│  ✓ Orders                                           │
│  ✓ Invoices                                         │
│  ✓ Salesmen                                         │
│  ... etc                                            │
└─────────────────────────────────────────────────────┘

Step 3-7: Same as before (columns, joins, filters, etc.)
```

---

## 10. Dependency Injection Setup (Program.cs)

```csharp
// Register all data source adapters
builder.Services.AddScoped<SqlDataSourceAdapter>();
builder.Services.AddScoped<ApiDataSourceAdapter>();
builder.Services.AddScoped<KuduDataSourceAdapter>();

// Register as IEnumerable for auto-discovery
builder.Services.AddScoped<IDataSourceAdapter, SqlDataSourceAdapter>();
builder.Services.AddScoped<IDataSourceAdapter, ApiDataSourceAdapter>();
builder.Services.AddScoped<IDataSourceAdapter, KuduDataSourceAdapter>();

// Register registry
builder.Services.AddSingleton<IDataSourceRegistry, DataSourceRegistry>();

// Register HttpClient for API adapter
builder.Services.AddHttpClient("ErpApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ExternalApi:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Existing services
builder.Services.AddSingleton<IReportCatalog, ReportCatalog>();
builder.Services.AddSingleton<IReportRunner, ReportRunner>();
// ... etc
```

---

## 11. Migration Path: CompUFloor → API → Kudu

### Phase 1: Current (Immediate)
- Build Query Builder with SQL adapter
- All reports use direct SQL queries
- Architecture designed for multi-source from day 1

### Phase 2: API Integration (When API is ready)
- Implement API adapter
- Migrate selected reports to use API
- Run both SQL and API in parallel (gradual migration)
- Compare performance and results

### Phase 3: Kudu ERP (Future)
- Implement Kudu adapter
- Add Kudu as data source option
- Migrate reports one-by-one
- Eventually deprecate CompUFloor SQL adapter

### Zero Downtime Migration:
```csharp
// Report definitions can specify fallback sources
{
  "dataSource": {
    "type": "Kudu",
    "fallbackSources": ["API", "SQL"] // If Kudu fails, try API, then SQL
  }
}
```

---

## 12. Benefits of This Architecture

| Benefit | Description |
|---------|-------------|
| ✅ **Future-Proof** | Easy to add new data sources without refactoring |
| ✅ **Gradual Migration** | Migrate reports incrementally (SQL → API → Kudu) |
| ✅ **Zero Downtime** | Run multiple sources in parallel during migration |
| ✅ **Hybrid Reports** | Combine data from multiple sources (future enhancement) |
| ✅ **Source-Agnostic UI** | Query Builder UI works with any data source |
| ✅ **Consistent Security** | Same authorization model across all sources |
| ✅ **Easy Testing** | Mock adapters for unit testing |
| ✅ **Extensible** | Add Salesforce, SAP, custom APIs easily |

---

## 13. Example: Creating Report with API Source

```csharp
// User creates report in Query Builder
var reportDef = new ReportDefinition
{
    ReportId = "api-sales-summary",
    Name = "Sales Summary (via API)",
    DataSourceType = "API", // <-- Specify API source
    // ... rest of definition
};

// When report runs:
var registry = serviceProvider.GetService<IDataSourceRegistry>();
var adapter = registry.GetAdapter(reportDef.DataSourceType); // Gets ApiDataSourceAdapter

var results = await adapter.ExecuteQueryAsync(reportDef.QueryDefinition, parameters, context);
// Results are returned as DataTable regardless of source!
```

---

## 14. Hybrid Reports (Advanced Future Feature)

For truly complex scenarios, you could combine multiple sources in one report:

```json
{
  "reportId": "hybrid-sales-with-external-pricing",
  "dataSources": [
    {
      "id": "internal",
      "type": "SQL",
      "tables": ["Orders", "Salesmen"]
    },
    {
      "id": "external",
      "type": "API",
      "tables": ["PricingData"]
    }
  ],
  "joins": [
    {
      "leftSource": "internal",
      "leftTable": "Orders",
      "leftColumn": "ProductId",
      "rightSource": "external",
      "rightTable": "PricingData",
      "rightColumn": "ProductId",
      "joinType": "LEFT"
    }
  ]
}
```

This would require client-side join logic, but the architecture supports it.

---

## 15. Implementation Priority

### Must Have (Phase 1):
- ✅ Abstraction layer (IDataSourceAdapter interface)
- ✅ SQL adapter (CompUFloor)
- ✅ Data source registry
- ✅ UI to select data source when creating report

### Nice to Have (Phase 2):
- ✅ API adapter skeleton (even if no API exists yet)
- ✅ Kudu adapter stub (returns "not implemented")
- ✅ Fallback source logic

### Future (Phase 3+):
- ✅ Hybrid reports (multiple sources in one report)
- ✅ Client-side join logic for non-SQL sources
- ✅ Data source performance monitoring
- ✅ Smart source selection (automatically choose fastest source)

---

## Conclusion

This multi-source architecture adds **minimal complexity now** but provides **massive flexibility later**. When Kudu ERP is ready, you'll just:

1. Implement `IDataSourceAdapter` for Kudu
2. Register it in DI
3. Users can immediately create reports using Kudu

**No refactoring of existing code required!** 🎉
