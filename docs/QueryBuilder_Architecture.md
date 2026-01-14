# Query Builder Architecture - Design Document

## Executive Summary

This document outlines the architecture for a custom Query Builder system that allows administrators to create dynamic reports from ERP database tables without writing SQL code. The solution leverages open-source components (no licensing costs) combined with custom security and integration layers.

---

## 1. System Overview

### 1.1 Goals
- ✅ Enable non-technical admins to create reports via drag-and-drop interface
- ✅ Maintain strict security controls over database access
- ✅ Integrate seamlessly with existing reporting infrastructure
- ✅ Use only open-source/free components (no licenses required)
- ✅ Support role-based and location-based access control
- ✅ Provide column visibility, formatting, and export configuration

### 1.2 Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                        │
│  ┌────────────────┐  ┌──────────────┐  ┌─────────────┐     │
│  │ Query Builder  │  │ Report Config│  │  Preview    │     │
│  │   Interface    │  │     UI       │  │   Results   │     │
│  └────────────────┘  └──────────────┘  └─────────────┘     │
└─────────────────────────────────────────────────────────────┘
                            ↓ ↑
┌─────────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                          │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ ReportsBuilderController                             │   │
│  ├─────────────────────────────────────────────────────┤   │
│  │ • Schema Discovery Service                           │   │
│  │ • Query Generator Service                            │   │
│  │ • Query Validation Service                           │   │
│  │ • Report Definition Service                          │   │
│  │ • Authorization Service (Enhanced)                   │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            ↓ ↑
┌─────────────────────────────────────────────────────────────┐
│                     DATA LAYER                               │
│  ┌──────────────────┐  ┌──────────────────────────────┐    │
│  │ SalesMetricsDB   │  │    ERP Databases             │    │
│  │ (Report Config)  │  │    (Read-Only Access)        │    │
│  │                  │  │                              │    │
│  │ • ReportDefs     │  │ • CompUFloorLA (LAX)         │    │
│  │ • ColumnDefs     │  │ • CompUFloorLV (LSV)         │    │
│  │ • TableWhitelist │  │ • CompUFloorChino (CHN)      │    │
│  │ • QueryTemplates │  │ • CompUFloorSD (SND)         │    │
│  │ • AuditLog       │  │ • CompUFloorPHX (PHX)        │    │
│  └──────────────────┘  └──────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Technology Stack (100% Open-Source)

### 2.1 Frontend Query Builder Components

#### **Primary Option: jQuery Query Builder**
- **Library**: https://github.com/mistic100/jQuery-QueryBuilder
- **License**: MIT (Free)
- **Features**:
  - Visual query builder with drag-and-drop
  - Support for complex conditions (AND/OR logic)
  - Custom validators and filters
  - JSON output format
  - Bootstrap integration (matches your existing UI)
  - Active maintenance

**Installation**:
```html
<!-- CSS -->
<link href="https://cdn.jsdelivr.net/npm/jQuery-QueryBuilder@2.7.3/dist/css/query-builder.default.min.css" rel="stylesheet">

<!-- JS -->
<script src="https://cdn.jsdelivr.net/npm/jQuery-QueryBuilder@2.7.3/dist/js/query-builder.standalone.min.js"></script>
```

#### **Alternative Option: React-QueryBuilder** (if migrating to React)
- **Library**: https://github.com/react-querybuilder/react-querybuilder
- **License**: MIT (Free)
- For future consideration if frontend modernizes

### 2.2 Visual SQL Designer Components

#### **SQL Designer (Table Relationship UI)**
- **Library**: https://github.com/ondras/wwwsqldesigner
- **License**: New BSD License (Free)
- **Features**:
  - Visual table selection
  - Drag-and-drop join creation
  - Relationship visualization
  - Export to SQL

#### **jsPlumb (Connection Visualizer)**
- **Library**: https://github.com/jsplumb/jsplumb
- **License**: MIT/GPL (Free)
- **Use Case**: Draw visual connections between tables for joins

### 2.3 Backend Technologies

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **ORM** | Entity Framework Core | Already in use, continue using |
| **SQL Parser** | Microsoft.SqlServer.Management.SqlParser | Free, validate SQL safety |
| **JSON** | System.Text.Json | Already in .NET, serialize query definitions |
| **Validation** | FluentValidation | Open-source validation library |

### 2.4 Database

- **SQL Server** (Already in use)
- New tables in `SalesMetrics` database for report definitions

---

## 3. Database Schema Design

### 3.1 New Tables Required

```sql
-- ============================================
-- Report Builder Schema
-- ============================================

-- Main report definitions (enhanced from existing)
CREATE TABLE ReportDefinitions (
    ReportDefinitionId INT IDENTITY(1,1) PRIMARY KEY,
    ReportId NVARCHAR(100) NOT NULL UNIQUE, -- Friendly ID like "sales-by-region"
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(1000),
    Category NVARCHAR(50), -- "Sales", "Operations", "Financial"

    -- Report Type
    IsCustom BIT NOT NULL DEFAULT 0, -- TRUE = User-created, FALSE = System/Hardcoded

    -- Query Definition (for custom reports)
    QueryDefinitionJson NVARCHAR(MAX), -- JSON structure of query
    GeneratedSql NVARCHAR(MAX), -- Generated SQL (cached)

    -- Authorization
    AllowedRoles NVARCHAR(255), -- Comma-separated: "1,3,4"
    AllowedLocations NVARCHAR(255), -- Comma-separated: "LAX,LSV,CHN"

    -- Status & Versioning
    IsActive BIT NOT NULL DEFAULT 1,
    Version INT NOT NULL DEFAULT 1,
    ParentReportId INT NULL, -- For versioning, links to previous version

    -- Audit
    CreatedByUserId INT NOT NULL,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
    ModifiedByUserId INT NULL,
    ModifiedDate DATETIME NULL,

    -- Scheduling
    IsScheduled BIT NOT NULL DEFAULT 0,
    ScheduleCron NVARCHAR(100), -- Cron expression for scheduled execution
    LastExecutedDate DATETIME NULL,

    FOREIGN KEY (CreatedByUserId) REFERENCES Users(Users_ID),
    FOREIGN KEY (ModifiedByUserId) REFERENCES Users(Users_ID),
    FOREIGN KEY (ParentReportId) REFERENCES ReportDefinitions(ReportDefinitionId)
);

-- Column definitions for reports
CREATE TABLE ReportColumnDefinitions (
    ColumnDefinitionId INT IDENTITY(1,1) PRIMARY KEY,
    ReportDefinitionId INT NOT NULL,

    -- Column Identity
    ColumnName NVARCHAR(100) NOT NULL, -- SQL column name
    DisplayName NVARCHAR(255) NOT NULL, -- User-friendly name

    -- Display Configuration
    DataType NVARCHAR(50) NOT NULL, -- "text", "number", "date", "currency", "percentage", "boolean"
    FormatString NVARCHAR(50), -- e.g., "MM/DD/YYYY", "$#,##0.00", "0.00%"
    IsVisible BIT NOT NULL DEFAULT 1,
    DisplayOrder INT NOT NULL DEFAULT 0,
    Width INT, -- Column width in pixels (optional)

    -- Aggregation (if applicable)
    AggregateFunction NVARCHAR(20), -- "SUM", "AVG", "COUNT", "MIN", "MAX"

    -- Conditional Formatting
    ConditionalFormattingJson NVARCHAR(MAX), -- JSON rules for highlighting

    FOREIGN KEY (ReportDefinitionId) REFERENCES ReportDefinitions(ReportDefinitionId) ON DELETE CASCADE
);

-- Query definition structure (what tables, joins, filters)
CREATE TABLE QueryTableReferences (
    TableReferenceId INT IDENTITY(1,1) PRIMARY KEY,
    ReportDefinitionId INT NOT NULL,

    -- Table Info
    TableName NVARCHAR(100) NOT NULL, -- e.g., "Orders"
    TableAlias NVARCHAR(50) NOT NULL, -- e.g., "o"
    IsBaseTable BIT NOT NULL DEFAULT 0, -- Primary table (FROM clause)

    -- Join Configuration
    JoinType NVARCHAR(20), -- "INNER", "LEFT", "RIGHT", "FULL"
    JoinCondition NVARCHAR(500), -- e.g., "o.OrderID = i.OrderID"
    JoinToTableId INT NULL, -- References another TableReferenceId

    DisplayOrder INT NOT NULL DEFAULT 0,

    FOREIGN KEY (ReportDefinitionId) REFERENCES ReportDefinitions(ReportDefinitionId) ON DELETE CASCADE,
    FOREIGN KEY (JoinToTableId) REFERENCES QueryTableReferences(TableReferenceId)
);

-- Where clause filters
CREATE TABLE QueryFilters (
    FilterId INT IDENTITY(1,1) PRIMARY KEY,
    ReportDefinitionId INT NOT NULL,

    -- Filter Definition
    ColumnName NVARCHAR(100) NOT NULL,
    Operator NVARCHAR(20) NOT NULL, -- "=", "!=", ">", "<", ">=", "<=", "LIKE", "IN", "BETWEEN", "IS NULL"
    Value NVARCHAR(500), -- Filter value (or parameter name)

    -- Logical Grouping
    GroupLevel INT NOT NULL DEFAULT 0, -- For parentheses grouping
    LogicalOperator NVARCHAR(10), -- "AND" or "OR"

    -- Dynamic Parameters
    IsParameter BIT NOT NULL DEFAULT 0, -- If TRUE, value comes from user input
    ParameterName NVARCHAR(50), -- e.g., "FromDate", "ToDate"
    ParameterType NVARCHAR(20), -- "date", "text", "number", "select"
    IsRequired BIT NOT NULL DEFAULT 0,
    DefaultValue NVARCHAR(255),

    DisplayOrder INT NOT NULL DEFAULT 0,

    FOREIGN KEY (ReportDefinitionId) REFERENCES ReportDefinitions(ReportDefinitionId) ON DELETE CASCADE
);

-- Whitelist of allowed tables (security)
CREATE TABLE AllowedTables (
    AllowedTableId INT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(100) NOT NULL UNIQUE,
    SchemaName NVARCHAR(50) NOT NULL DEFAULT 'dbo',
    DisplayName NVARCHAR(255) NOT NULL,
    Description NVARCHAR(500),
    Category NVARCHAR(50), -- "Sales", "Financial", "Operations"

    -- Security
    RequiresRoleId INT, -- If set, only specific roles can use this table
    RequiresLocation NVARCHAR(255), -- If set, only specific locations can access

    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),

    FOREIGN KEY (RequiresRoleId) REFERENCES Roles(RoleID)
);

-- Whitelist of allowed columns per table
CREATE TABLE AllowedColumns (
    AllowedColumnId INT IDENTITY(1,1) PRIMARY KEY,
    AllowedTableId INT NOT NULL,
    ColumnName NVARCHAR(100) NOT NULL,
    DisplayName NVARCHAR(255) NOT NULL,
    DataType NVARCHAR(50) NOT NULL, -- SQL data type
    Description NVARCHAR(500),

    -- Security: Hide sensitive columns from UI
    IsSensitive BIT NOT NULL DEFAULT 0, -- If TRUE, hide from query builder

    -- Query Building Hints
    IsFilterable BIT NOT NULL DEFAULT 1,
    IsSortable BIT NOT NULL DEFAULT 1,
    IsAggregatable BIT NOT NULL DEFAULT 0,

    IsActive BIT NOT NULL DEFAULT 1,

    FOREIGN KEY (AllowedTableId) REFERENCES AllowedTables(AllowedTableId) ON DELETE CASCADE,
    UNIQUE (AllowedTableId, ColumnName)
);

-- Table relationships (for automatic join suggestions)
CREATE TABLE TableRelationships (
    RelationshipId INT IDENTITY(1,1) PRIMARY KEY,
    FromTableId INT NOT NULL,
    ToTableId INT NOT NULL,

    -- Join Definition
    FromColumnName NVARCHAR(100) NOT NULL,
    ToColumnName NVARCHAR(100) NOT NULL,
    RelationshipType NVARCHAR(20) NOT NULL, -- "ONE_TO_ONE", "ONE_TO_MANY", "MANY_TO_ONE"

    -- Display
    DisplayName NVARCHAR(255),
    Description NVARCHAR(500),

    -- Auto-suggest
    IsSuggestedJoin BIT NOT NULL DEFAULT 1, -- Show as recommended join in UI

    IsActive BIT NOT NULL DEFAULT 1,

    FOREIGN KEY (FromTableId) REFERENCES AllowedTables(AllowedTableId),
    FOREIGN KEY (ToTableId) REFERENCES AllowedTables(AllowedTableId),
    UNIQUE (FromTableId, ToTableId, FromColumnName, ToColumnName)
);

-- Report execution history & audit
CREATE TABLE ReportExecutionLog (
    ExecutionLogId INT IDENTITY(1,1) PRIMARY KEY,
    ReportDefinitionId INT NOT NULL,

    -- Execution Details
    ExecutedByUserId INT NOT NULL,
    ExecutedDate DATETIME NOT NULL DEFAULT GETDATE(),
    ExecutionTimeMs INT, -- Performance tracking

    -- Parameters used
    ParametersJson NVARCHAR(MAX), -- JSON of parameter values

    -- Results
    RowCount INT,
    Success BIT NOT NULL,
    ErrorMessage NVARCHAR(MAX),

    -- Export
    ExportFormat NVARCHAR(20), -- "excel", "csv", "pdf", NULL if just viewed

    FOREIGN KEY (ReportDefinitionId) REFERENCES ReportDefinitions(ReportDefinitionId),
    FOREIGN KEY (ExecutedByUserId) REFERENCES Users(Users_ID)
);

-- Report templates for common patterns
CREATE TABLE ReportTemplates (
    TemplateId INT IDENTITY(1,1) PRIMARY KEY,
    TemplateName NVARCHAR(255) NOT NULL,
    Description NVARCHAR(1000),
    Category NVARCHAR(50),

    -- Template Structure
    QueryDefinitionJson NVARCHAR(MAX) NOT NULL,

    -- Display
    IconClass NVARCHAR(50), -- Bootstrap icon class
    DisplayOrder INT NOT NULL DEFAULT 0,

    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);

-- Report sharing & favorites (optional future enhancement)
CREATE TABLE ReportSharing (
    SharingId INT IDENTITY(1,1) PRIMARY KEY,
    ReportDefinitionId INT NOT NULL,
    SharedByUserId INT NOT NULL,
    SharedWithUserId INT, -- NULL = shared with all
    SharedWithRoleId INT,
    SharedDate DATETIME NOT NULL DEFAULT GETDATE(),

    FOREIGN KEY (ReportDefinitionId) REFERENCES ReportDefinitions(ReportDefinitionId) ON DELETE CASCADE,
    FOREIGN KEY (SharedByUserId) REFERENCES Users(Users_ID),
    FOREIGN KEY (SharedWithUserId) REFERENCES Users(Users_ID),
    FOREIGN KEY (SharedWithRoleId) REFERENCES Roles(RoleID)
);

-- User favorites
CREATE TABLE UserFavoriteReports (
    FavoriteId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    ReportDefinitionId INT NOT NULL,
    AddedDate DATETIME NOT NULL DEFAULT GETDATE(),

    FOREIGN KEY (UserId) REFERENCES Users(Users_ID) ON DELETE CASCADE,
    FOREIGN KEY (ReportDefinitionId) REFERENCES ReportDefinitions(ReportDefinitionId) ON DELETE CASCADE,
    UNIQUE (UserId, ReportDefinitionId)
);

-- Indexes for performance
CREATE INDEX IX_ReportDefinitions_ReportId ON ReportDefinitions(ReportId);
CREATE INDEX IX_ReportDefinitions_IsActive ON ReportDefinitions(IsActive);
CREATE INDEX IX_ReportDefinitions_CreatedByUserId ON ReportDefinitions(CreatedByUserId);
CREATE INDEX IX_ReportColumnDefinitions_ReportId ON ReportColumnDefinitions(ReportDefinitionId);
CREATE INDEX IX_QueryTableReferences_ReportId ON QueryTableReferences(ReportDefinitionId);
CREATE INDEX IX_QueryFilters_ReportId ON QueryFilters(ReportDefinitionId);
CREATE INDEX IX_ReportExecutionLog_ReportId ON ReportExecutionLog(ReportDefinitionId);
CREATE INDEX IX_ReportExecutionLog_ExecutedByUserId ON ReportExecutionLog(ExecutedByUserId);
CREATE INDEX IX_ReportExecutionLog_ExecutedDate ON ReportExecutionLog(ExecutedDate);
```

### 3.2 Sample Data - Initial Whitelist

```sql
-- Example: Whitelist common ERP tables
INSERT INTO AllowedTables (TableName, SchemaName, DisplayName, Description, Category, IsActive)
VALUES
    ('Orders', 'dbo', 'Sales Orders', 'Customer sales orders', 'Sales', 1),
    ('Invoices', 'dbo', 'Invoices', 'Invoice records', 'Sales', 1),
    ('Properties', 'dbo', 'Properties', 'Customer property information', 'Sales', 1),
    ('Salesmen', 'dbo', 'Sales Team', 'Sales team members', 'Sales', 1),
    ('PriceBooks', 'dbo', 'Price Books', 'Pricing information', 'Sales', 1),
    ('Items', 'dbo', 'Inventory Items', 'Product catalog', 'Operations', 1),
    ('Warehouses', 'dbo', 'Warehouses', 'Warehouse locations', 'Operations', 1);

-- Example: Define columns for Orders table
DECLARE @OrdersTableId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Orders');

INSERT INTO AllowedColumns (AllowedTableId, ColumnName, DisplayName, DataType, Description, IsSensitive, IsFilterable, IsSortable, IsAggregatable, IsActive)
VALUES
    (@OrdersTableId, 'OrderID', 'Order ID', 'int', 'Unique order identifier', 0, 1, 1, 0, 1),
    (@OrdersTableId, 'OrderDate', 'Order Date', 'datetime', 'Date order was placed', 0, 1, 1, 0, 1),
    (@OrdersTableId, 'OrderAmount', 'Order Amount', 'decimal', 'Total order value', 0, 1, 1, 1, 1),
    (@OrdersTableId, 'Status', 'Status', 'varchar', 'Order status', 0, 1, 1, 0, 1),
    (@OrdersTableId, 'SalesmanNumber', 'Salesman', 'varchar', 'Assigned salesman', 0, 1, 1, 0, 1),
    (@OrdersTableId, 'PropertyName', 'Property', 'varchar', 'Customer property name', 0, 1, 1, 0, 1),
    (@OrdersTableId, 'CostAmount', 'Cost', 'decimal', 'Order cost', 0, 1, 1, 1, 1);

-- Example: Define relationships
DECLARE @OrdersTableId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Orders');
DECLARE @InvoicesTableId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Invoices');

INSERT INTO TableRelationships (FromTableId, ToTableId, FromColumnName, ToColumnName, RelationshipType, DisplayName, Description, IsSuggestedJoin, IsActive)
VALUES
    (@OrdersTableId, @InvoicesTableId, 'OrderID', 'OrderID', 'ONE_TO_MANY', 'Orders to Invoices', 'One order can have multiple invoices', 1, 1);
```

---

## 4. Security Model

### 4.1 Multi-Layer Security Approach

```
┌────────────────────────────────────────────────────────┐
│ Layer 1: Authentication                                 │
│ • User must be logged in                               │
│ • Session validation                                   │
└────────────────────────────────────────────────────────┘
                        ↓
┌────────────────────────────────────────────────────────┐
│ Layer 2: Role-Based Access Control                     │
│ • Check if user's role has "Query Builder" permission  │
│ • Admin-only feature initially                         │
└────────────────────────────────────────────────────────┘
                        ↓
┌────────────────────────────────────────────────────────┐
│ Layer 3: Table-Level Whitelist                         │
│ • Only pre-approved tables can be queried              │
│ • No system tables, no sensitive tables                │
│ • Location-specific restrictions                       │
└────────────────────────────────────────────────────────┘
                        ↓
┌────────────────────────────────────────────────────────┐
│ Layer 4: Column-Level Filtering                        │
│ • Hide sensitive columns (passwords, SSN, etc.)        │
│ • Only whitelisted columns shown in UI                 │
└────────────────────────────────────────────────────────┘
                        ↓
┌────────────────────────────────────────────────────────┐
│ Layer 5: SQL Injection Prevention                      │
│ • All queries generated programmatically               │
│ • No direct SQL input accepted                         │
│ • Parameterized queries only                           │
└────────────────────────────────────────────────────────┘
                        ↓
┌────────────────────────────────────────────────────────┐
│ Layer 6: Query Validation                              │
│ • Parse generated SQL                                  │
│ • Reject DELETE, UPDATE, INSERT, DROP, ALTER           │
│ • Only SELECT allowed                                  │
│ • Timeout limits (30 seconds)                          │
│ • Row limits (max 10,000 rows)                         │
└────────────────────────────────────────────────────────┘
                        ↓
┌────────────────────────────────────────────────────────┐
│ Layer 7: Audit Logging                                 │
│ • Log all query executions                             │
│ • Track who, what, when                                │
│ • Monitor for suspicious patterns                      │
└────────────────────────────────────────────────────────┘
```

### 4.2 Permission Management

Add new feature permissions to existing `Features` table:

```sql
INSERT INTO Features (FeatureCode, FeatureName, Description, Category, IsActive, DisplayOrder)
VALUES
    ('REPORT_BUILDER_ACCESS', 'Query Builder - Access', 'Access the query builder interface', 'Reports', 1, 100),
    ('REPORT_BUILDER_CREATE', 'Query Builder - Create Reports', 'Create new custom reports', 'Reports', 1, 101),
    ('REPORT_BUILDER_EDIT', 'Query Builder - Edit Reports', 'Edit existing custom reports', 'Reports', 1, 102),
    ('REPORT_BUILDER_DELETE', 'Query Builder - Delete Reports', 'Delete custom reports', 'Reports', 1, 103),
    ('REPORT_BUILDER_SHARE', 'Query Builder - Share Reports', 'Share reports with other users', 'Reports', 1, 104),
    ('REPORT_BUILDER_ADMIN', 'Query Builder - Admin', 'Manage table whitelist and templates', 'Reports', 1, 105);
```

### 4.3 SQL Validation Rules

**Blocked Keywords**:
```csharp
private static readonly string[] BlockedKeywords = {
    "DELETE", "UPDATE", "INSERT", "DROP", "ALTER", "CREATE", "TRUNCATE",
    "EXEC", "EXECUTE", "SP_", "XP_", "OPENROWSET", "OPENDATASOURCE",
    "BULK", "BACKUP", "RESTORE", "GRANT", "REVOKE", "DENY"
};
```

**Allowed Patterns**:
- Must start with `SELECT`
- Can only contain `FROM`, `JOIN`, `WHERE`, `GROUP BY`, `HAVING`, `ORDER BY`
- Aggregates: `SUM`, `AVG`, `COUNT`, `MIN`, `MAX`
- Functions: Common string, date, math functions (whitelist)

---

## 5. Implementation Phases

### **Phase 1: Foundation (Weeks 1-2)**

**Goals**: Setup database schema, basic security, table whitelist

**Tasks**:
1. Create database migration with all new tables
2. Seed `AllowedTables` with safe ERP tables
3. Seed `AllowedColumns` for each table
4. Define `TableRelationships` for common joins
5. Create feature permissions
6. Build `IDatabaseSchemaService` to query metadata
7. Build `IQueryValidationService` with security checks

**Deliverables**:
- Database schema deployed
- Schema discovery API working
- Basic validation framework

---

### **Phase 2: Query Builder UI (Weeks 3-4)**

**Goals**: Build visual query builder interface

**Tasks**:
1. Create new controller: `ReportBuilderController.cs`
2. Create views:
   - `/Views/ReportBuilder/Index.cshtml` - List custom reports
   - `/Views/ReportBuilder/Create.cshtml` - Query builder interface
   - `/Views/ReportBuilder/Edit.cshtml` - Edit existing report
   - `/Views/ReportBuilder/Preview.cshtml` - Test query results
3. Integrate jQuery QueryBuilder library
4. Build table selector UI (drag-and-drop or dropdown)
5. Build join configurator (visual or form-based)
6. Implement column selector with:
   - Display name override
   - Format selection (date, currency, percentage)
   - Visibility toggle
   - Aggregate function selection
7. Add filter builder (using jQuery QueryBuilder)

**Deliverables**:
- Working UI to build queries visually
- JSON output of query definition
- Preview functionality (show first 100 rows)

---

### **Phase 3: Query Generation Engine (Week 5)**

**Goals**: Convert JSON query definition to safe SQL

**Tasks**:
1. Create `IQueryGeneratorService`
2. Implement `GenerateSQL(QueryDefinition definition)` method
3. Build SQL generation for:
   - SELECT clause (columns + aggregates)
   - FROM clause (base table)
   - JOIN clauses (with proper join types)
   - WHERE clause (from filters)
   - GROUP BY clause (if aggregates used)
   - ORDER BY clause
4. Implement parameterization for user inputs
5. Add query optimization hints
6. Validate generated SQL against security rules

**Deliverables**:
- Query generator service
- Unit tests for SQL generation
- Integration with validation service

---

### **Phase 4: Report Configuration & Persistence (Week 6)**

**Goals**: Save, load, and manage custom reports

**Tasks**:
1. Create `IReportDefinitionService` for CRUD operations
2. Implement save functionality:
   - Save `ReportDefinitions` row
   - Save `ReportColumnDefinitions` rows
   - Save `QueryTableReferences` rows
   - Save `QueryFilters` rows (if needed)
3. Implement load functionality:
   - Load report by ID
   - Reconstruct query definition JSON
4. Add versioning support (save new version on edit)
5. Implement report deletion (soft delete)
6. Build report listing page with:
   - Search/filter
   - Categories
   - Created by / date
   - Edit/Delete actions

**Deliverables**:
- Persistent storage of custom reports
- Report management interface
- Version history

---

### **Phase 5: Execution & Display (Week 7)**

**Goals**: Execute custom reports and display results

**Tasks**:
1. Enhance `IReportRunner` to support custom reports
2. Add execution logic:
   - Load report definition
   - Generate SQL
   - Execute against appropriate database (by location)
   - Apply timeout limits
   - Apply row limits
   - Return results as DataTable
3. Create dynamic report view:
   - Generate DataTables configuration from column definitions
   - Apply formatting rules
   - Apply conditional formatting (if configured)
4. Integrate with existing export service
5. Add parameter input form (if report has parameters)

**Deliverables**:
- Custom reports executable from catalog
- Results displayed with proper formatting
- Excel/CSV/PDF export working

---

### **Phase 6: Authorization & Audit (Week 8)**

**Goals**: Apply security controls and tracking

**Tasks**:
1. Enhance `IReportAuthorizationService` for custom reports
2. Implement permission checks:
   - Query builder access
   - Create/Edit/Delete permissions
   - Report execution permissions
3. Build audit logging:
   - Log all report creations
   - Log all report executions
   - Log all exports
4. Create admin dashboard:
   - View execution history
   - Monitor popular reports
   - Identify slow queries
5. Add query performance monitoring

**Deliverables**:
- Full authorization framework
- Audit trail for all operations
- Admin monitoring tools

---

### **Phase 7: Advanced Features (Weeks 9-10)** *(Optional)*

**Goals**: Add power-user features

**Tasks**:
1. **Templates**:
   - Pre-built query templates (e.g., "Sales by Month")
   - One-click report creation from template
2. **Scheduling**:
   - Schedule reports to run automatically
   - Email delivery of results
3. **Sharing**:
   - Share reports with specific users/roles
   - Public vs private reports
4. **Advanced Formatting**:
   - Conditional highlighting rules
   - Custom CSS classes
   - Charts/graphs (optional)
5. **Query Optimization**:
   - Suggest indexes for slow queries
   - Cache frequently-run reports

**Deliverables**:
- Template library
- Scheduled execution engine
- Sharing functionality

---

## 6. Integration with Existing System

### 6.1 Modifications to Existing Code

#### **ReportCatalog.cs**
```csharp
public class ReportCatalog : IReportCatalog
{
    private readonly SalesMetricsDbContext _dbContext;

    public IEnumerable<IReportDefinition> GetAllReports()
    {
        // Existing hardcoded reports
        yield return new ReportDefinition { /* margin commission report */ };

        // PLUS: Load custom reports from database
        var customReports = _dbContext.ReportDefinitions
            .Where(r => r.IsActive && r.IsCustom)
            .ToList();

        foreach (var custom in customReports)
        {
            yield return MapToReportDefinition(custom);
        }
    }
}
```

#### **ReportRunner.cs**
```csharp
public async Task<DataTable> ExecuteAsync(string reportId, ReportParameters parameters)
{
    // Check if it's a custom report
    var reportDef = await _dbContext.ReportDefinitions
        .FirstOrDefaultAsync(r => r.ReportId == reportId && r.IsCustom);

    if (reportDef != null)
    {
        // Generate SQL from definition
        var sql = _queryGenerator.GenerateSQL(
            JsonSerializer.Deserialize<QueryDefinition>(reportDef.QueryDefinitionJson)
        );

        // Validate
        _validator.ValidateQuery(sql);

        // Execute
        return await ExecuteCustomReportAsync(sql, parameters);
    }

    // Otherwise, fall back to hardcoded reports
    return reportId switch
    {
        "margin-commission-discrepancy-open-invoiced" => await ExecuteMarginCommissionReport(parameters),
        _ => throw new ArgumentException($"Unknown report: {reportId}")
    };
}
```

#### **ReportsController.cs**
- No major changes needed!
- Custom reports will flow through existing `Run` action
- Export already handles DataTable, so it'll work automatically

### 6.2 New Controllers

#### **ReportBuilderController.cs**
```csharp
[Authorize]
[RequireFeature("REPORT_BUILDER_ACCESS")]
public class ReportBuilderController : Controller
{
    public IActionResult Index() { /* List custom reports */ }
    public IActionResult Create() { /* Query builder UI */ }
    public IActionResult Edit(int id) { /* Edit existing */ }

    [HttpPost]
    public async Task<IActionResult> SaveReport(ReportDefinitionViewModel model) { /* Save */ }

    [HttpPost]
    public async Task<IActionResult> PreviewQuery(QueryDefinitionViewModel model) { /* Test query */ }

    [HttpGet]
    public async Task<IActionResult> GetTables() { /* Return allowed tables */ }

    [HttpGet]
    public async Task<IActionResult> GetColumns(int tableId) { /* Return columns */ }

    [HttpGet]
    public async Task<IActionResult> GetRelationships(int tableId) { /* Suggest joins */ }
}
```

### 6.3 Routing Changes

Add to `Program.cs`:
```csharp
app.MapControllerRoute(
    name: "reportBuilder",
    pattern: "reports/builder/{action=Index}/{id?}",
    defaults: new { controller = "ReportBuilder" });
```

### 6.4 Menu/Navigation Updates

Add to sidebar (if user has permission):
```html
<li class="nav-item">
    <a class="nav-link" asp-controller="ReportBuilder" asp-action="Index">
        <i class="bi bi-tools"></i> Query Builder
    </a>
</li>
```

---

## 7. Example User Workflows

### **Workflow 1: Admin Creates "Top Sales by Region" Report**

1. Navigate to `/reports/builder`
2. Click "Create New Report"
3. **Step 1: Select Tables**
   - Choose "Orders" as base table
   - Add "Salesmen" table (suggested join)
   - Add "Properties" table (suggested join)
4. **Step 2: Configure Joins**
   - System auto-suggests joins based on relationships
   - Admin confirms: `Orders.SalesmanNumber = Salesmen.SalesmanNumber`
   - Admin confirms: `Orders.PropertyID = Properties.PropertyID`
5. **Step 3: Select Columns**
   - Salesmen.SalesmanName (Display as "Salesman")
   - Properties.State (Display as "Region")
   - Orders.OrderAmount (Display as "Total Sales", Aggregate: SUM, Format: Currency)
   - Orders.OrderID (Display as "# of Orders", Aggregate: COUNT)
6. **Step 4: Add Filters** (optional)
   - Make "Date Range" a parameter (FromDate, ToDate)
   - Add WHERE: `Orders.OrderDate BETWEEN @FromDate AND @ToDate`
7. **Step 5: Configure Display**
   - Set "Total Sales" visibility: visible
   - Set formatting: Currency
   - Set conditional formatting: Highlight if > $100,000
8. **Step 6: Preview**
   - Click "Preview" - shows first 100 rows
   - Verify results look correct
9. **Step 7: Save**
   - Report Name: "Top Sales by Region"
   - Description: "Summarize sales performance by salesman and region"
   - Category: "Sales"
   - Allowed Roles: Admin, Manager
   - Allowed Locations: LAX, LSV, CHN
   - Click "Save Report"
10. **Result**: Report now appears in Reports catalog for authorized users

### **Workflow 2: User Runs Custom Report**

1. Navigate to `/reports` (existing catalog page)
2. See both system reports and custom reports
3. Click "Top Sales by Region"
4. Enter parameters (FromDate, ToDate)
5. Click "Run Report"
6. Results display in DataTable
7. Click "Excel" to export
8. Excel contains only visible columns with proper formatting

---

## 8. Technical Implementation Details

### 8.1 Query Definition JSON Structure

```json
{
  "reportId": "top-sales-by-region",
  "version": 1,
  "tables": [
    {
      "tableId": 1,
      "tableName": "Orders",
      "alias": "o",
      "isBaseTable": true
    },
    {
      "tableId": 2,
      "tableName": "Salesmen",
      "alias": "s",
      "joinType": "INNER",
      "joinCondition": "o.SalesmanNumber = s.SalesmanNumber"
    },
    {
      "tableId": 3,
      "tableName": "Properties",
      "alias": "p",
      "joinType": "LEFT",
      "joinCondition": "o.PropertyID = p.PropertyID"
    }
  ],
  "columns": [
    {
      "expression": "s.SalesmanName",
      "alias": "Salesman",
      "displayName": "Salesman",
      "dataType": "text",
      "isVisible": true,
      "displayOrder": 1
    },
    {
      "expression": "p.State",
      "alias": "Region",
      "displayName": "Region",
      "dataType": "text",
      "isVisible": true,
      "displayOrder": 2
    },
    {
      "expression": "SUM(o.OrderAmount)",
      "alias": "TotalSales",
      "displayName": "Total Sales",
      "dataType": "currency",
      "formatString": "$#,##0.00",
      "isVisible": true,
      "displayOrder": 3,
      "conditionalFormatting": [
        {
          "condition": "> 100000",
          "cssClass": "table-success"
        }
      ]
    },
    {
      "expression": "COUNT(o.OrderID)",
      "alias": "OrderCount",
      "displayName": "# of Orders",
      "dataType": "number",
      "isVisible": true,
      "displayOrder": 4
    }
  ],
  "filters": [
    {
      "columnName": "o.OrderDate",
      "operator": "BETWEEN",
      "isParameter": true,
      "parameterName": "FromDate",
      "parameterType": "date",
      "required": true
    },
    {
      "columnName": "o.OrderDate",
      "operator": "BETWEEN",
      "isParameter": true,
      "parameterName": "ToDate",
      "parameterType": "date",
      "required": true
    }
  ],
  "groupBy": ["s.SalesmanName", "p.State"],
  "orderBy": [
    {
      "columnName": "TotalSales",
      "direction": "DESC"
    }
  ]
}
```

### 8.2 SQL Generation Example

**From JSON above, generate**:
```sql
SELECT
    s.SalesmanName AS Salesman,
    p.State AS Region,
    SUM(o.OrderAmount) AS TotalSales,
    COUNT(o.OrderID) AS OrderCount
FROM Orders o
INNER JOIN Salesmen s ON o.SalesmanNumber = s.SalesmanNumber
LEFT JOIN Properties p ON o.PropertyID = p.PropertyID
WHERE o.OrderDate BETWEEN @FromDate AND @ToDate
GROUP BY s.SalesmanName, p.State
ORDER BY TotalSales DESC
```

### 8.3 Security Validation Steps

```csharp
public class QueryValidationService : IQueryValidationService
{
    public ValidationResult ValidateQuery(string sql)
    {
        // 1. Check for blocked keywords
        if (ContainsBlockedKeywords(sql))
            return ValidationResult.Fail("Query contains forbidden operations");

        // 2. Must start with SELECT
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Fail("Only SELECT queries are allowed");

        // 3. Parse SQL using SqlParser
        var parseResult = SqlParser.Parse(sql);
        if (!parseResult.Success)
            return ValidationResult.Fail($"Invalid SQL syntax: {parseResult.Error}");

        // 4. Verify all tables are in whitelist
        var tables = ExtractTableNames(parseResult.Tree);
        foreach (var table in tables)
        {
            if (!_whitelistService.IsTableAllowed(table))
                return ValidationResult.Fail($"Table '{table}' is not allowed");
        }

        // 5. Verify all columns belong to whitelisted tables
        var columns = ExtractColumns(parseResult.Tree);
        foreach (var column in columns)
        {
            if (!_whitelistService.IsColumnAllowed(column.Table, column.Column))
                return ValidationResult.Fail($"Column '{column}' is not allowed");
        }

        // 6. Check for dynamic SQL or dangerous functions
        if (ContainsDangerousFunctions(sql))
            return ValidationResult.Fail("Query contains dangerous functions");

        return ValidationResult.Success();
    }
}
```

---

## 9. Cost & Effort Estimation

### 9.1 Resource Requirements

| Resource | Quantity | Notes |
|----------|----------|-------|
| **Backend Developer** | 1 FTE | C# / ASP.NET Core / SQL Server |
| **Frontend Developer** | 0.5 FTE | JavaScript / jQuery / Bootstrap |
| **QA Tester** | 0.25 FTE | Security testing critical |
| **DBA** | 0.25 FTE | Schema design, performance tuning |

### 9.2 Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1: Foundation | 2 weeks | 2 weeks |
| Phase 2: UI | 2 weeks | 4 weeks |
| Phase 3: Query Engine | 1 week | 5 weeks |
| Phase 4: Persistence | 1 week | 6 weeks |
| Phase 5: Execution | 1 week | 7 weeks |
| Phase 6: Security & Audit | 1 week | 8 weeks |
| **MVP Total** | **8 weeks** | |
| Phase 7: Advanced Features | 2 weeks | 10 weeks |
| **Full System** | **10 weeks** | |

**Assumptions**:
- Developer familiar with codebase
- No major architectural changes needed
- Open-source libraries work as expected
- Limited scope creep

### 9.3 Ongoing Costs

| Item | Cost | Notes |
|------|------|-------|
| **Software Licenses** | $0 | All open-source |
| **Hosting** | $0 | Uses existing SQL Server |
| **Maintenance** | Low | Mostly static after deployment |

---

## 10. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **SQL Injection** | Medium | Critical | Multi-layer validation, parameterization, whitelist |
| **Performance Issues** | High | Medium | Query timeout limits, row limits, indexing |
| **User Creates Invalid Join** | Medium | Low | Suggest joins based on relationships, validation |
| **Scope Creep** | High | Medium | Strict phased approach, MVP first |
| **Users Bypass Security** | Low | Critical | Audit logging, regular security reviews |
| **Complex Queries Timeout** | Medium | Medium | 30-second limit, guide users to simpler queries |
| **User Confusion** | Medium | Low | Templates, documentation, training |

---

## 11. Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Adoption Rate** | 50% of admins create at least 1 report | Track ReportDefinitions.CreatedByUserId |
| **Report Execution** | 100+ executions/month | Track ReportExecutionLog |
| **Export Usage** | 30% of executions result in export | Track ExportFormat field |
| **Average Query Time** | < 5 seconds | Monitor ExecutionTimeMs |
| **Security Incidents** | 0 | Audit ReportExecutionLog for suspicious queries |
| **User Satisfaction** | > 4/5 rating | Survey after 1 month |

---

## 12. Future Enhancements (Post-MVP)

1. **Visual Query Builder** (like SQL Designer)
   - Drag-and-drop table icons
   - Visual line drawing for joins
   - More intuitive for non-technical users

2. **Natural Language Interface**
   - "Show me sales by region for last month"
   - AI translates to query definition
   - Uses GPT/Claude API (if approved)

3. **Calculated Fields**
   - User defines custom formulas
   - e.g., `Margin = (OrderAmount - CostAmount) / OrderAmount * 100`

4. **Advanced Visualizations**
   - Charts (bar, line, pie)
   - Dashboards with multiple reports
   - Interactive drill-down

5. **Mobile Support**
   - Responsive design
   - Touch-friendly query builder
   - Mobile app (optional)

6. **Data Catalog**
   - Searchable data dictionary
   - Column descriptions and examples
   - Usage statistics (popular columns)

7. **Query Marketplace**
   - Users share useful queries
   - Rating and comments
   - Featured reports

8. **Version Control for Reports**
   - Git-like branching
   - Rollback to previous versions
   - Compare versions

---

## 13. Appendix: Open-Source Component Details

### A. jQuery QueryBuilder
- **GitHub**: https://github.com/mistic100/jQuery-QueryBuilder
- **Demo**: https://querybuilder.js.org/
- **License**: MIT
- **Features**:
  - 50+ built-in validation types
  - Bootstrap 3/4/5 support
  - Powerful API
  - JSON export/import
  - Internationalization

### B. SQL Designer
- **GitHub**: https://github.com/ondras/wwwsqldesigner
- **Demo**: https://ondras.zarovi.cz/sql/demo/
- **License**: BSD
- **Note**: Can be integrated as embedded iframe or adapted

### C. FluentValidation
- **GitHub**: https://github.com/FluentValidation/FluentValidation
- **NuGet**: https://www.nuget.org/packages/FluentValidation
- **License**: Apache 2.0
- **Use**: Validate report definitions, parameters

### D. AngleSharp (HTML/XML Parser)
- **GitHub**: https://github.com/AngleSharp/AngleSharp
- **License**: MIT
- **Use**: Parse and validate HTML in descriptions (prevent XSS)

---

## 14. Conclusion

This architecture provides a **secure, scalable, and cost-effective** solution for building a custom query builder system. By leveraging 100% open-source components and integrating seamlessly with your existing infrastructure, you can empower your sales team and admins to create powerful reports without writing SQL.

**Key Advantages**:
- ✅ Zero licensing costs
- ✅ Multi-layer security model
- ✅ Seamless integration with existing reports
- ✅ Phased implementation reduces risk
- ✅ Audit trail for compliance
- ✅ Extensible for future enhancements

**Next Steps**:
1. Review and approve this architecture
2. Prioritize phases (MVP = Phases 1-6)
3. Allocate development resources
4. Begin Phase 1 implementation
