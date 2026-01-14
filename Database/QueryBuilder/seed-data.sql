-- ============================================
-- Query Builder Seed Data
-- Date: 2026-01-14
-- Description: Initial data for Query Builder feature
-- ============================================

USE [SalesMetrics]
GO

PRINT 'Starting Query Builder seed data...';
GO

-- ============================================
-- 1. Feature Permissions
-- ============================================
PRINT '1. Adding Feature Permissions...';

-- Check if Query Builder features already exist
IF NOT EXISTS (SELECT 1 FROM Features WHERE FeatureCode = 'REPORT_BUILDER_ACCESS')
BEGIN
    INSERT INTO Features (FeatureCode, FeatureName, Description, Category, IsActive, DisplayOrder)
    VALUES
        ('REPORT_BUILDER_ACCESS', 'Query Builder - Access', 'Access the query builder interface', 'Reports', 1, 100),
        ('REPORT_BUILDER_CREATE', 'Query Builder - Create Reports', 'Create new custom reports', 'Reports', 1, 101),
        ('REPORT_BUILDER_EDIT', 'Query Builder - Edit Reports', 'Edit existing custom reports', 'Reports', 1, 102),
        ('REPORT_BUILDER_DELETE', 'Query Builder - Delete Reports', 'Delete custom reports', 'Reports', 1, 103),
        ('REPORT_BUILDER_SHARE', 'Query Builder - Share Reports', 'Share reports with other users', 'Reports', 1, 104),
        ('REPORT_BUILDER_ADMIN', 'Query Builder - Admin', 'Manage table whitelist and templates', 'Reports', 1, 105);

    PRINT '  ✓ Feature permissions added';
END
ELSE
BEGIN
    PRINT '  - Feature permissions already exist';
END
GO

-- ============================================
-- 2. Data Source Configurations
-- ============================================
PRINT '2. Adding Data Source Configurations...';

-- Get first admin user ID (or use 1 as default)
DECLARE @AdminUserId INT = (SELECT TOP 1 Users_ID FROM Users WHERE RoleID = 1);
IF @AdminUserId IS NULL SET @AdminUserId = 1;

IF NOT EXISTS (SELECT 1 FROM DataSourceConfigurations WHERE DataSourceType = 'SQL' AND ConfigName = 'CompUFloor')
BEGIN
    INSERT INTO DataSourceConfigurations (DataSourceType, ConfigName, DisplayName, IsActive, IsDefault, TimeoutSeconds, MaxRowsPerQuery, CreatedByUserId)
    VALUES
        ('SQL', 'CompUFloor', 'CompUFloor ERP (SQL Server)', 1, 1, 30, 10000, @AdminUserId),
        ('API', 'ExternalAPI', 'External API (REST)', 0, 0, 60, 1000, @AdminUserId),
        ('Kudu', 'KuduERP', 'Kudu ERP (Future)', 0, 0, 45, 5000, @AdminUserId);

    PRINT '  ✓ Data source configurations added';
END
ELSE
BEGIN
    PRINT '  - Data source configurations already exist';
END
GO

-- ============================================
-- 3. Allowed Tables (Security Whitelist)
-- ============================================
PRINT '3. Adding Allowed Tables...';

-- Add common ERP tables (adjust based on your actual CompUFloor schema)
IF NOT EXISTS (SELECT 1 FROM AllowedTables WHERE TableName = 'Orders')
BEGIN
    INSERT INTO AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
    VALUES
        ('Orders', 'dbo', 'Sales Orders', 'Customer sales orders', 'Sales', 'SQL', 1),
        ('Invoices', 'dbo', 'Invoices', 'Invoice records', 'Sales', 'SQL', 1),
        ('OrderDetails', 'dbo', 'Order Details', 'Line items for orders', 'Sales', 'SQL', 1),
        ('Customers', 'dbo', 'Customers', 'Customer information', 'Sales', 'SQL', 1),
        ('Properties', 'dbo', 'Properties', 'Customer properties', 'Sales', 'SQL', 1),
        ('Salesmen', 'dbo', 'Sales Team', 'Sales team members', 'Sales', 'SQL', 1),
        ('Items', 'dbo', 'Inventory Items', 'Product catalog', 'Operations', 'SQL', 1),
        ('Warehouses', 'dbo', 'Warehouses', 'Warehouse locations', 'Operations', 'SQL', 1),
        ('Vendors', 'dbo', 'Vendors', 'Supplier information', 'Procurement', 'SQL', 1),
        ('PurchaseOrders', 'dbo', 'Purchase Orders', 'Orders to vendors', 'Procurement', 'SQL', 1);

    PRINT '  ✓ Allowed tables added';
END
ELSE
BEGIN
    PRINT '  - Allowed tables already exist';
END
GO

-- ============================================
-- 4. Allowed Columns (Sample for Orders table)
-- ============================================
PRINT '4. Adding Sample Allowed Columns...';

DECLARE @OrdersTableId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Orders' AND SchemaName = 'dbo');

IF @OrdersTableId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM AllowedColumns WHERE AllowedTableId = @OrdersTableId)
BEGIN
    INSERT INTO AllowedColumns (AllowedTableId, ColumnName, DisplayName, DataType, Description, IsSensitive, IsFilterable, IsSortable, IsAggregatable, IsActive)
    VALUES
        (@OrdersTableId, 'OrderID', 'Order ID', 'number', 'Unique order identifier', 0, 1, 1, 0, 1),
        (@OrdersTableId, 'OrderNumber', 'Order Number', 'string', 'Order reference number', 0, 1, 1, 0, 1),
        (@OrdersTableId, 'OrderDate', 'Order Date', 'date', 'Date order was placed', 0, 1, 1, 0, 1),
        (@OrdersTableId, 'CustomerID', 'Customer ID', 'number', 'Customer identifier', 0, 1, 1, 0, 1),
        (@OrdersTableId, 'SalesmanNumber', 'Salesman', 'string', 'Assigned salesman', 0, 1, 1, 0, 1),
        (@OrdersTableId, 'PropertyID', 'Property ID', 'number', 'Property identifier', 0, 1, 1, 0, 1),
        (@OrdersTableId, 'OrderAmount', 'Order Amount', 'number', 'Total order value', 0, 1, 1, 1, 1),
        (@OrdersTableId, 'CostAmount', 'Cost', 'number', 'Order cost', 0, 1, 1, 1, 1),
        (@OrdersTableId, 'Status', 'Status', 'string', 'Order status', 0, 1, 1, 0, 1),
        (@OrdersTableId, 'WarehouseID', 'Warehouse', 'number', 'Fulfillment warehouse', 0, 1, 1, 0, 1);

    PRINT '  ✓ Sample columns for Orders table added';
END
ELSE IF @OrdersTableId IS NULL
BEGIN
    PRINT '  - Orders table not found, skipping columns';
END
ELSE
BEGIN
    PRINT '  - Columns already exist';
END
GO

-- ============================================
-- 5. Sample Table Relationships
-- ============================================
PRINT '5. Adding Sample Table Relationships...';

DECLARE @OrdersId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Orders');
DECLARE @InvoicesId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Invoices');
DECLARE @OrderDetailsId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'OrderDetails');
DECLARE @CustomersId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Customers');
DECLARE @PropertiesId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Properties');
DECLARE @SalesmenId INT = (SELECT AllowedTableId FROM AllowedTables WHERE TableName = 'Salesmen');

IF @OrdersId IS NOT NULL AND @InvoicesId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TableRelationships WHERE FromTableId = @OrdersId AND ToTableId = @InvoicesId)
BEGIN
    INSERT INTO TableRelationships (FromTableId, ToTableId, FromColumnName, ToColumnName, RelationshipType, DisplayName, Description, IsSuggestedJoin, IsActive)
    VALUES
        (@OrdersId, @InvoicesId, 'OrderID', 'OrderID', 'ONE_TO_MANY', 'Orders → Invoices', 'One order can have multiple invoices', 1, 1),
        (@OrdersId, @OrderDetailsId, 'OrderID', 'OrderID', 'ONE_TO_MANY', 'Orders → Order Details', 'One order has many line items', 1, 1),
        (@OrdersId, @CustomersId, 'CustomerID', 'CustomerID', 'MANY_TO_ONE', 'Orders → Customers', 'Many orders belong to one customer', 1, 1),
        (@OrdersId, @PropertiesId, 'PropertyID', 'PropertyID', 'MANY_TO_ONE', 'Orders → Properties', 'Many orders for one property', 1, 1),
        (@OrdersId, @SalesmenId, 'SalesmanNumber', 'SalesmanNumber', 'MANY_TO_ONE', 'Orders → Salesmen', 'Many orders by one salesman', 1, 1);

    PRINT '  ✓ Sample table relationships added';
END
ELSE IF @OrdersId IS NULL OR @InvoicesId IS NULL
BEGIN
    PRINT '  - Required tables not found, skipping relationships';
END
ELSE
BEGIN
    PRINT '  - Relationships already exist';
END
GO

-- ============================================
-- 6. Sample Report Template
-- ============================================
PRINT '6. Adding Sample Report Template...';

IF NOT EXISTS (SELECT 1 FROM ReportTemplates WHERE TemplateName = 'Sales Summary by Date Range')
BEGIN
    INSERT INTO ReportTemplates (TemplateName, Description, Category, QueryDefinitionJson, IconClass, DisplayOrder, IsActive)
    VALUES (
        'Sales Summary by Date Range',
        'Simple sales summary report grouped by date',
        'Sales',
        '{
  "reportId": "template-sales-summary",
  "version": 1,
  "dataSource": { "type": "SQL", "configName": "CompUFloor" },
  "tables": [
    { "tableId": 1, "tableName": "Orders", "alias": "o", "isBaseTable": true }
  ],
  "columns": [
    { "expression": "o.OrderDate", "alias": "OrderDate", "displayName": "Date", "dataType": "date", "isVisible": true, "displayOrder": 1 },
    { "expression": "COUNT(o.OrderID)", "alias": "OrderCount", "displayName": "# Orders", "dataType": "number", "isVisible": true, "displayOrder": 2, "aggregateFunction": "COUNT" },
    { "expression": "SUM(o.OrderAmount)", "alias": "TotalSales", "displayName": "Total Sales", "dataType": "currency", "formatString": "$#,##0.00", "isVisible": true, "displayOrder": 3, "aggregateFunction": "SUM" }
  ],
  "filters": [
    { "columnName": "o.OrderDate", "operator": "BETWEEN", "isParameter": true, "parameterName": "FromDate", "parameterType": "date", "required": true },
    { "columnName": "o.OrderDate", "operator": "BETWEEN", "isParameter": true, "parameterName": "ToDate", "parameterType": "date", "required": true }
  ],
  "groupBy": ["o.OrderDate"],
  "orderBy": [{ "columnName": "o.OrderDate", "direction": "DESC" }]
}',
        'bi-calendar3',
        1,
        1
    );

    PRINT '  ✓ Sample report template added';
END
ELSE
BEGIN
    PRINT '  - Sample template already exists';
END
GO

-- ============================================
-- Seed Data Summary
-- ============================================
PRINT '';
PRINT '==============================================';
PRINT 'Query Builder Seed Data Completed';
PRINT '==============================================';
PRINT 'Added:';
PRINT '  ✓ 6 Feature Permissions';
PRINT '  ✓ 3 Data Source Configurations';
PRINT '  ✓ 10 Allowed Tables';
PRINT '  ✓ Sample Columns for Orders table';
PRINT '  ✓ 5 Table Relationships';
PRINT '  ✓ 1 Sample Report Template';
PRINT '==============================================';
PRINT 'Next Steps:';
PRINT '  1. Run AddQueryBuilderTables.sql migration';
PRINT '  2. Run this seed data script';
PRINT '  3. Register services in Program.cs';
PRINT '  4. Grant permissions to admin users';
PRINT '  5. Start building Query Builder UI';
PRINT '==============================================';
GO
