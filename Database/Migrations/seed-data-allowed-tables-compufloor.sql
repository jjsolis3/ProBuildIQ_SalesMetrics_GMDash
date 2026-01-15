-- =============================================
-- Seed Data: CompUFloor Allowed Tables for Query Builder
-- Description: Whitelist of real CompUFloor tables extracted from database schema
-- Date: 2026-01-15
-- =============================================

USE SalesMetrics;
GO

-- Clear existing placeholder data
DELETE FROM QueryBuilder.AllowedTables;
GO

-- =============================================
-- CATEGORY: Sales Orders
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('SALES_HEADER', 'dbo', 'Sales Orders', 'Main sales order header information including customer, dates, and totals', 'Sales Orders', 'SQL', 1),
('SALES_DETAIL', 'dbo', 'Sales Order Lines', 'Line item details for each sales order including products, quantities, and pricing', 'Sales Orders', 'SQL', 1),
('SALES_ORDER_STATUS', 'dbo', 'Sales Order Status', 'Status tracking for sales orders (e.g., Open, Completed, Cancelled)', 'Sales Orders', 'SQL', 1),
('SALES_ORDER_DEPOSITS', 'dbo', 'Sales Order Deposits', 'Deposit payments applied to sales orders', 'Sales Orders', 'SQL', 1),
('SALES_ORDER_INVOICES', 'dbo', 'Sales Order Invoices', 'Invoice history linked to sales orders', 'Sales Orders', 'SQL', 1),
('SALES_ORDER_LOG', 'dbo', 'Sales Order Activity Log', 'Audit trail of changes and activities on sales orders', 'Sales Orders', 'SQL', 1),
('SALES_SURVEY', 'dbo', 'Sales Surveys', 'Customer survey information and site measurements', 'Sales Orders', 'SQL', 1),
('SALES_PAYMENT_TERMS', 'dbo', 'Payment Terms', 'Payment terms and conditions for sales orders', 'Sales Orders', 'SQL', 1),
('SALES_JOB_TYPE', 'dbo', 'Job Types', 'Types of jobs (e.g., Commercial, Residential)', 'Sales Orders', 'SQL', 1),
('SALES_TYPE_OF_WORK', 'dbo', 'Work Types', 'Categories of work performed (e.g., Installation, Repair)', 'Sales Orders', 'SQL', 1),
('SalesOrderComments', 'dbo', 'Sales Order Comments', 'Notes and comments attached to sales orders', 'Sales Orders', 'SQL', 1),
('SalesDetailAllocations', 'dbo', 'Sales Detail Allocations', 'Inventory allocations for sales order line items', 'Sales Orders', 'SQL', 1);

-- =============================================
-- CATEGORY: Customers
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('CUSTOMER_MASTER', 'dbo', 'Customers', 'Master customer information including contact details and credit terms', 'Customers', 'SQL', 1),
('CUSTOMER_SHIP_LOCATION', 'dbo', 'Customer Ship-To Addresses', 'Shipping addresses for customers', 'Customers', 'SQL', 1),
('CUSTOMER_COMMENTS', 'dbo', 'Customer Comments', 'Notes and comments about customers', 'Customers', 'SQL', 1);

-- =============================================
-- CATEGORY: Invoicing
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('INVOICE_HEADER', 'dbo', 'Invoice Headers', 'Main invoice header information', 'Invoicing', 'SQL', 1),
('INVOICE_DETAIL', 'dbo', 'Invoice Line Items', 'Detailed line items for each invoice', 'Invoicing', 'SQL', 1),
('INVOICE_REGISTER', 'dbo', 'Invoice Register', 'Complete invoice register with accounting details', 'Invoicing', 'SQL', 1),
('INVOICE_DEPOSIT', 'dbo', 'Invoice Deposits', 'Deposit payments applied to invoices', 'Invoicing', 'SQL', 1),
('INVOICE', 'dbo', 'Invoices (Summary)', 'Invoice summary data', 'Invoicing', 'SQL', 1);

-- =============================================
-- CATEGORY: Inventory
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('ITEM_MASTER', 'dbo', 'Products/Items', 'Master product catalog with descriptions, costs, and specifications', 'Inventory', 'SQL', 1),
('ITEM_PRICE', 'dbo', 'Item Pricing', 'Pricing information for products including cost and sell prices', 'Inventory', 'SQL', 1),
('ITEM_TRANSACTION', 'dbo', 'Inventory Transactions', 'All inventory movements (receipts, issues, adjustments)', 'Inventory', 'SQL', 1),
('ITEM_TYPE', 'dbo', 'Item Types', 'Product type classifications', 'Inventory', 'SQL', 1),
('ITEM_VENDOR', 'dbo', 'Item Vendors', 'Vendor associations for products', 'Inventory', 'SQL', 1),
('WAREHOUSE_ITEM', 'dbo', 'Warehouse Inventory', 'Inventory quantities by warehouse location', 'Inventory', 'SQL', 1),
('WAREHOUSE_MASTER', 'dbo', 'Warehouses', 'Warehouse location master data', 'Inventory', 'SQL', 1),
('STOCK_REGISTER', 'dbo', 'Stock Register', 'Complete inventory register with transaction history', 'Inventory', 'SQL', 1),
('STOCK_REQUIRED', 'dbo', 'Stock Requirements', 'Inventory requirements and reservations', 'Inventory', 'SQL', 1),
('INVENTORY_CLASSIFICATION', 'dbo', 'Inventory Classifications', 'Product classification codes', 'Inventory', 'SQL', 1),
('ITEM_COMPONENTS', 'dbo', 'Item Components', 'Bill of materials for composite items', 'Inventory', 'SQL', 1),
('ITEM_SUBSTITUTES', 'dbo', 'Item Substitutes', 'Substitute products and alternatives', 'Inventory', 'SQL', 1);

-- =============================================
-- CATEGORY: Purchase Orders
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('PURCHASE_ORDER_HEADER', 'dbo', 'Purchase Orders', 'Purchase order header information', 'Purchase Orders', 'SQL', 1),
('PURCHASE_ORDER_DETAIL', 'dbo', 'Purchase Order Lines', 'Line item details for purchase orders', 'Purchase Orders', 'SQL', 1),
('PURCHASE_ORDER_RECEIVED', 'dbo', 'PO Receipts', 'Receipt history for purchase orders', 'Purchase Orders', 'SQL', 1),
('PurchaseOrderReceivedBatch', 'dbo', 'PO Receipt Batches', 'Batch receipt processing for purchase orders', 'Purchase Orders', 'SQL', 1);

-- =============================================
-- CATEGORY: Accounts Payable
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('AP_OPEN_ITEM', 'dbo', 'AP Open Items', 'Outstanding accounts payable invoices', 'Accounts Payable', 'SQL', 1),
('AP_TRANSACTION', 'dbo', 'AP Transactions', 'All AP transaction history', 'Accounts Payable', 'SQL', 1),
('AP_CHECK', 'dbo', 'AP Checks', 'Check payment history', 'Accounts Payable', 'SQL', 1),
('AP_VENDOR_HISTORY', 'dbo', 'AP Vendor History', 'Historical AP activity by vendor', 'Accounts Payable', 'SQL', 1),
('AP_DISTRIBUTION', 'dbo', 'AP Distributions', 'GL account distributions for AP transactions', 'Accounts Payable', 'SQL', 1);

-- =============================================
-- CATEGORY: Accounts Receivable
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('AR_OPEN_ITEM', 'dbo', 'AR Open Items', 'Outstanding accounts receivable invoices', 'Accounts Receivable', 'SQL', 1),
('AR_TRANSACTION_FILE', 'dbo', 'AR Transactions', 'All AR transaction history including payments', 'Accounts Receivable', 'SQL', 1),
('AGING_INFO', 'dbo', 'AR Aging Information', 'Customer aging bucket details', 'Accounts Receivable', 'SQL', 1);

-- =============================================
-- CATEGORY: General Ledger
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('CHART_OF_ACCOUNTS', 'dbo', 'Chart of Accounts', 'Complete chart of accounts with account numbers and descriptions', 'General Ledger', 'SQL', 1),
('GL_JOURNAL', 'dbo', 'GL Journal Entries', 'General ledger journal entry details', 'General Ledger', 'SQL', 1),
('GL_CASH_ACCOUNTS', 'dbo', 'Cash Accounts', 'Cash account definitions', 'General Ledger', 'SQL', 1),
('GL_SALES_ACCOUNTS', 'dbo', 'Sales Accounts', 'Revenue account definitions', 'General Ledger', 'SQL', 1),
('GL_EXPENSE_ACCOUNTS', 'dbo', 'Expense Accounts', 'Expense account definitions', 'General Ledger', 'SQL', 1),
('CASH_JOURNAL', 'dbo', 'Cash Journal', 'Cash receipt and disbursement journal', 'General Ledger', 'SQL', 1);

-- =============================================
-- CATEGORY: Vendors
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('VENDOR_MASTER', 'dbo', 'Vendors', 'Vendor master information including contact and payment terms', 'Vendors', 'SQL', 1),
('VENDOR_INVOICES', 'dbo', 'Vendor Invoices', 'Vendor invoice details', 'Vendors', 'SQL', 1),
('VENDOR_COMMENTS', 'dbo', 'Vendor Comments', 'Notes and comments about vendors', 'Vendors', 'SQL', 1);

-- =============================================
-- CATEGORY: Salesperson
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('SALESMAN_MASTER', 'dbo', 'Salespeople', 'Salesperson master data', 'Salesperson', 'SQL', 1),
('SALESMAN_COMMENTS', 'dbo', 'Salesperson Comments', 'Notes about salespeople', 'Salesperson', 'SQL', 1),
('COMMISSION_GROSS_DOLLAR', 'dbo', 'Commissions (Gross Dollar)', 'Commission calculations based on gross dollars', 'Salesperson', 'SQL', 1),
('COMMISSION_GROSS_MARGIN', 'dbo', 'Commissions (Gross Margin)', 'Commission calculations based on gross margin', 'Salesperson', 'SQL', 1),
('Salesman_Division_Commission', 'dbo', 'Division Commission Rates', 'Commission rates by salesperson and division', 'Salesperson', 'SQL', 1);

-- =============================================
-- CATEGORY: Installers & Work Orders
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('INSTALLER_MASTER', 'dbo', 'Installers', 'Installer contractor master data', 'Installers', 'SQL', 1),
('INSTALLER_LABOR', 'dbo', 'Installer Labor Rates', 'Labor rates and pricing for installers', 'Installers', 'SQL', 1),
('InstallerSchedule', 'dbo', 'Installer Schedule', 'Installation job scheduling', 'Installers', 'SQL', 1),
('WORK_ORDER_STATUS', 'dbo', 'Work Order Status', 'Status tracking for work orders', 'Work Orders', 'SQL', 1),
('WORK_ORDER_FLOORREPAIR', 'dbo', 'Floor Repair Work Orders', 'Work orders for floor repair jobs', 'Work Orders', 'SQL', 1);

-- =============================================
-- CATEGORY: Company & Configuration
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('COMPANY_INFORMATION', 'dbo', 'Company Information', 'Company master data and branch information', 'Company', 'SQL', 1),
('CONTROL_INFORMATION', 'dbo', 'Control Settings', 'System control parameters and settings', 'Company', 'SQL', 1),
('WAREHOUSE_MASTER', 'dbo', 'Branch Locations', 'Branch/location master data', 'Company', 'SQL', 1);

-- =============================================
-- CATEGORY: Quotes & Projects
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('Quote', 'dbo', 'Quotes', 'Sales quotes and estimates', 'Quotes', 'SQL', 1),
('QuoteDetail', 'dbo', 'Quote Line Items', 'Line item details for quotes', 'Quotes', 'SQL', 1),
('Jobs', 'dbo', 'Jobs', 'Job/project master data', 'Projects', 'SQL', 1),
('JobTypes', 'dbo', 'Job Types', 'Job type classifications', 'Projects', 'SQL', 1);

-- =============================================
-- CATEGORY: Reporting & Analytics
-- =============================================
INSERT INTO QueryBuilder.AllowedTables (TableName, SchemaName, DisplayName, Description, Category, DataSourceType, IsActive)
VALUES
('DAILY_SALES_SUMMARY', 'dbo', 'Daily Sales Summary', 'Daily sales summary and metrics', 'Reporting', 'SQL', 1),
('SalesHistoryData', 'dbo', 'Sales History', 'Historical sales data for reporting', 'Reporting', 'SQL', 1),
('BatchInvoiceReportData', 'dbo', 'Batch Invoice Report Data', 'Pre-aggregated invoice reporting data', 'Reporting', 'SQL', 1);

GO

PRINT 'Successfully inserted ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' CompUFloor tables into AllowedTables';
GO
