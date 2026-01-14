-- ============================================
-- Query Builder Database Migration
-- Date: 2026-01-14
-- Description: Creates all tables for Multi-Source Query Builder
-- ============================================

USE [SalesMetrics]
GO

-- ============================================
-- 1. Report Definitions Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReportDefinitions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReportDefinitions] (
        [ReportDefinitionId] INT IDENTITY(1,1) PRIMARY KEY,
        [ReportId] NVARCHAR(100) NOT NULL UNIQUE,
        [Name] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [Category] NVARCHAR(50) NULL,

        -- Report Type
        [IsCustom] BIT NOT NULL DEFAULT 0,

        -- Query Definition (for custom reports)
        [QueryDefinitionJson] NVARCHAR(MAX) NULL,
        [GeneratedSql] NVARCHAR(MAX) NULL,

        -- Data Source (Multi-Source Support)
        [DataSourceType] NVARCHAR(50) NOT NULL DEFAULT 'SQL', -- "SQL", "API", "Kudu"
        [DataSourceConfig] NVARCHAR(MAX) NULL,

        -- Authorization
        [AllowedRoles] NVARCHAR(255) NULL,
        [AllowedLocations] NVARCHAR(255) NULL,

        -- Status & Versioning
        [IsActive] BIT NOT NULL DEFAULT 1,
        [Version] INT NOT NULL DEFAULT 1,
        [ParentReportId] INT NULL,

        -- Audit
        [CreatedByUserId] INT NOT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ModifiedByUserId] INT NULL,
        [ModifiedDate] DATETIME NULL,

        -- Scheduling
        [IsScheduled] BIT NOT NULL DEFAULT 0,
        [ScheduleCron] NVARCHAR(100) NULL,
        [LastExecutedDate] DATETIME NULL,

        CONSTRAINT [FK_ReportDefinitions_ParentReport] FOREIGN KEY ([ParentReportId])
            REFERENCES [dbo].[ReportDefinitions]([ReportDefinitionId]),
        CONSTRAINT [FK_ReportDefinitions_CreatedBy] FOREIGN KEY ([CreatedByUserId])
            REFERENCES [dbo].[Users]([Users_ID]),
        CONSTRAINT [FK_ReportDefinitions_ModifiedBy] FOREIGN KEY ([ModifiedByUserId])
            REFERENCES [dbo].[Users]([Users_ID])
    );

    CREATE INDEX [IX_ReportDefinitions_ReportId] ON [dbo].[ReportDefinitions]([ReportId]);
    CREATE INDEX [IX_ReportDefinitions_IsActive] ON [dbo].[ReportDefinitions]([IsActive]);
    CREATE INDEX [IX_ReportDefinitions_CreatedByUserId] ON [dbo].[ReportDefinitions]([CreatedByUserId]);
    CREATE INDEX [IX_ReportDefinitions_DataSourceType] ON [dbo].[ReportDefinitions]([DataSourceType]);

    PRINT 'Table [ReportDefinitions] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [ReportDefinitions] already exists';
END
GO

-- ============================================
-- 2. Report Column Definitions Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReportColumnDefinitions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReportColumnDefinitions] (
        [ColumnDefinitionId] INT IDENTITY(1,1) PRIMARY KEY,
        [ReportDefinitionId] INT NOT NULL,

        -- Column Identity
        [ColumnName] NVARCHAR(100) NOT NULL,
        [DisplayName] NVARCHAR(255) NOT NULL,

        -- Display Configuration
        [DataType] NVARCHAR(50) NOT NULL DEFAULT 'text',
        [FormatString] NVARCHAR(50) NULL,
        [IsVisible] BIT NOT NULL DEFAULT 1,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [Width] INT NULL,

        -- Aggregation
        [AggregateFunction] NVARCHAR(20) NULL,

        -- Conditional Formatting
        [ConditionalFormattingJson] NVARCHAR(MAX) NULL,

        CONSTRAINT [FK_ReportColumnDefinitions_ReportDefinition] FOREIGN KEY ([ReportDefinitionId])
            REFERENCES [dbo].[ReportDefinitions]([ReportDefinitionId]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_ReportColumnDefinitions_ReportId] ON [dbo].[ReportColumnDefinitions]([ReportDefinitionId]);

    PRINT 'Table [ReportColumnDefinitions] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [ReportColumnDefinitions] already exists';
END
GO

-- ============================================
-- 3. Query Table References Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[QueryTableReferences]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[QueryTableReferences] (
        [TableReferenceId] INT IDENTITY(1,1) PRIMARY KEY,
        [ReportDefinitionId] INT NOT NULL,

        -- Table Info
        [TableName] NVARCHAR(100) NOT NULL,
        [TableAlias] NVARCHAR(50) NOT NULL,
        [IsBaseTable] BIT NOT NULL DEFAULT 0,

        -- Join Configuration
        [JoinType] NVARCHAR(20) NULL,
        [JoinCondition] NVARCHAR(500) NULL,
        [JoinToTableId] INT NULL,

        [DisplayOrder] INT NOT NULL DEFAULT 0,

        CONSTRAINT [FK_QueryTableReferences_ReportDefinition] FOREIGN KEY ([ReportDefinitionId])
            REFERENCES [dbo].[ReportDefinitions]([ReportDefinitionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_QueryTableReferences_JoinToTable] FOREIGN KEY ([JoinToTableId])
            REFERENCES [dbo].[QueryTableReferences]([TableReferenceId])
    );

    CREATE INDEX [IX_QueryTableReferences_ReportId] ON [dbo].[QueryTableReferences]([ReportDefinitionId]);

    PRINT 'Table [QueryTableReferences] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [QueryTableReferences] already exists';
END
GO

-- ============================================
-- 4. Query Filters Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[QueryFilters]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[QueryFilters] (
        [FilterId] INT IDENTITY(1,1) PRIMARY KEY,
        [ReportDefinitionId] INT NOT NULL,

        -- Filter Definition
        [ColumnName] NVARCHAR(100) NOT NULL,
        [Operator] NVARCHAR(20) NOT NULL DEFAULT '=',
        [Value] NVARCHAR(500) NULL,

        -- Logical Grouping
        [GroupLevel] INT NOT NULL DEFAULT 0,
        [LogicalOperator] NVARCHAR(10) NULL,

        -- Dynamic Parameters
        [IsParameter] BIT NOT NULL DEFAULT 0,
        [ParameterName] NVARCHAR(50) NULL,
        [ParameterType] NVARCHAR(20) NULL,
        [IsRequired] BIT NOT NULL DEFAULT 0,
        [DefaultValue] NVARCHAR(255) NULL,

        [DisplayOrder] INT NOT NULL DEFAULT 0,

        CONSTRAINT [FK_QueryFilters_ReportDefinition] FOREIGN KEY ([ReportDefinitionId])
            REFERENCES [dbo].[ReportDefinitions]([ReportDefinitionId]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_QueryFilters_ReportId] ON [dbo].[QueryFilters]([ReportDefinitionId]);

    PRINT 'Table [QueryFilters] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [QueryFilters] already exists';
END
GO

-- ============================================
-- 5. Allowed Tables Table (Security Whitelist)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AllowedTables]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AllowedTables] (
        [AllowedTableId] INT IDENTITY(1,1) PRIMARY KEY,
        [TableName] NVARCHAR(100) NOT NULL,
        [SchemaName] NVARCHAR(50) NOT NULL DEFAULT 'dbo',
        [DisplayName] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [Category] NVARCHAR(50) NULL,

        -- Data Source Support (Multi-Source)
        [DataSourceType] NVARCHAR(50) NOT NULL DEFAULT 'SQL', -- "SQL", "API", "Kudu"
        [ApiEndpoint] NVARCHAR(500) NULL,
        [ApiMethod] NVARCHAR(10) NULL,

        -- Security
        [RequiresRoleId] INT NULL,
        [RequiresLocation] NVARCHAR(255) NULL,

        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [FK_AllowedTables_RequiresRole] FOREIGN KEY ([RequiresRoleId])
            REFERENCES [dbo].[Roles]([RoleID]),
        CONSTRAINT [UQ_AllowedTables] UNIQUE ([TableName], [SchemaName], [DataSourceType])
    );

    CREATE INDEX [IX_AllowedTables_DataSourceType] ON [dbo].[AllowedTables]([DataSourceType]);
    CREATE INDEX [IX_AllowedTables_IsActive] ON [dbo].[AllowedTables]([IsActive]);

    PRINT 'Table [AllowedTables] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [AllowedTables] already exists';
END
GO

-- ============================================
-- 6. Allowed Columns Table (Column-Level Security)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AllowedColumns]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AllowedColumns] (
        [AllowedColumnId] INT IDENTITY(1,1) PRIMARY KEY,
        [AllowedTableId] INT NOT NULL,
        [ColumnName] NVARCHAR(100) NOT NULL,
        [DisplayName] NVARCHAR(255) NOT NULL,
        [DataType] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(500) NULL,

        -- Security
        [IsSensitive] BIT NOT NULL DEFAULT 0,

        -- Query Building Hints
        [IsFilterable] BIT NOT NULL DEFAULT 1,
        [IsSortable] BIT NOT NULL DEFAULT 1,
        [IsAggregatable] BIT NOT NULL DEFAULT 0,

        [IsActive] BIT NOT NULL DEFAULT 1,

        CONSTRAINT [FK_AllowedColumns_AllowedTable] FOREIGN KEY ([AllowedTableId])
            REFERENCES [dbo].[AllowedTables]([AllowedTableId]) ON DELETE CASCADE,
        CONSTRAINT [UQ_AllowedColumns] UNIQUE ([AllowedTableId], [ColumnName])
    );

    CREATE INDEX [IX_AllowedColumns_AllowedTableId] ON [dbo].[AllowedColumns]([AllowedTableId]);

    PRINT 'Table [AllowedColumns] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [AllowedColumns] already exists';
END
GO

-- ============================================
-- 7. Table Relationships Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TableRelationships]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TableRelationships] (
        [RelationshipId] INT IDENTITY(1,1) PRIMARY KEY,
        [FromTableId] INT NOT NULL,
        [ToTableId] INT NOT NULL,

        -- Join Definition
        [FromColumnName] NVARCHAR(100) NOT NULL,
        [ToColumnName] NVARCHAR(100) NOT NULL,
        [RelationshipType] NVARCHAR(20) NOT NULL DEFAULT 'ONE_TO_MANY',

        -- Display
        [DisplayName] NVARCHAR(255) NULL,
        [Description] NVARCHAR(500) NULL,

        -- Auto-suggest
        [IsSuggestedJoin] BIT NOT NULL DEFAULT 1,

        [IsActive] BIT NOT NULL DEFAULT 1,

        CONSTRAINT [FK_TableRelationships_FromTable] FOREIGN KEY ([FromTableId])
            REFERENCES [dbo].[AllowedTables]([AllowedTableId]),
        CONSTRAINT [FK_TableRelationships_ToTable] FOREIGN KEY ([ToTableId])
            REFERENCES [dbo].[AllowedTables]([AllowedTableId]),
        CONSTRAINT [UQ_TableRelationships] UNIQUE ([FromTableId], [ToTableId], [FromColumnName], [ToColumnName])
    );

    PRINT 'Table [TableRelationships] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [TableRelationships] already exists';
END
GO

-- ============================================
-- 8. Report Execution Log Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReportExecutionLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReportExecutionLog] (
        [ExecutionLogId] INT IDENTITY(1,1) PRIMARY KEY,
        [ReportDefinitionId] INT NOT NULL,

        -- Execution Details
        [ExecutedByUserId] INT NOT NULL,
        [ExecutedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ExecutionTimeMs] INT NULL,

        -- Parameters used
        [ParametersJson] NVARCHAR(MAX) NULL,

        -- Results
        [RowCount] INT NULL,
        [Success] BIT NOT NULL DEFAULT 1,
        [ErrorMessage] NVARCHAR(MAX) NULL,

        -- Export
        [ExportFormat] NVARCHAR(20) NULL,

        CONSTRAINT [FK_ReportExecutionLog_ReportDefinition] FOREIGN KEY ([ReportDefinitionId])
            REFERENCES [dbo].[ReportDefinitions]([ReportDefinitionId]),
        CONSTRAINT [FK_ReportExecutionLog_ExecutedBy] FOREIGN KEY ([ExecutedByUserId])
            REFERENCES [dbo].[Users]([Users_ID])
    );

    CREATE INDEX [IX_ReportExecutionLog_ReportId] ON [dbo].[ReportExecutionLog]([ReportDefinitionId]);
    CREATE INDEX [IX_ReportExecutionLog_ExecutedByUserId] ON [dbo].[ReportExecutionLog]([ExecutedByUserId]);
    CREATE INDEX [IX_ReportExecutionLog_ExecutedDate] ON [dbo].[ReportExecutionLog]([ExecutedDate]);

    PRINT 'Table [ReportExecutionLog] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [ReportExecutionLog] already exists';
END
GO

-- ============================================
-- 9. Report Templates Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReportTemplates]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReportTemplates] (
        [TemplateId] INT IDENTITY(1,1) PRIMARY KEY,
        [TemplateName] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [Category] NVARCHAR(50) NULL,

        -- Template Structure
        [QueryDefinitionJson] NVARCHAR(MAX) NOT NULL,

        -- Display
        [IconClass] NVARCHAR(50) NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,

        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE()
    );

    PRINT 'Table [ReportTemplates] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [ReportTemplates] already exists';
END
GO

-- ============================================
-- 10. Report Sharing Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ReportSharing]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ReportSharing] (
        [SharingId] INT IDENTITY(1,1) PRIMARY KEY,
        [ReportDefinitionId] INT NOT NULL,
        [SharedByUserId] INT NOT NULL,
        [SharedWithUserId] INT NULL,
        [SharedWithRoleId] INT NULL,
        [SharedDate] DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [FK_ReportSharing_ReportDefinition] FOREIGN KEY ([ReportDefinitionId])
            REFERENCES [dbo].[ReportDefinitions]([ReportDefinitionId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReportSharing_SharedBy] FOREIGN KEY ([SharedByUserId])
            REFERENCES [dbo].[Users]([Users_ID]),
        CONSTRAINT [FK_ReportSharing_SharedWithUser] FOREIGN KEY ([SharedWithUserId])
            REFERENCES [dbo].[Users]([Users_ID]),
        CONSTRAINT [FK_ReportSharing_SharedWithRole] FOREIGN KEY ([SharedWithRoleId])
            REFERENCES [dbo].[Roles]([RoleID])
    );

    PRINT 'Table [ReportSharing] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [ReportSharing] already exists';
END
GO

-- ============================================
-- 11. User Favorite Reports Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserFavoriteReports]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserFavoriteReports] (
        [FavoriteId] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NOT NULL,
        [ReportDefinitionId] INT NOT NULL,
        [AddedDate] DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [FK_UserFavoriteReports_User] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[Users]([Users_ID]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserFavoriteReports_ReportDefinition] FOREIGN KEY ([ReportDefinitionId])
            REFERENCES [dbo].[ReportDefinitions]([ReportDefinitionId]) ON DELETE CASCADE,
        CONSTRAINT [UQ_UserFavoriteReports] UNIQUE ([UserId], [ReportDefinitionId])
    );

    PRINT 'Table [UserFavoriteReports] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [UserFavoriteReports] already exists';
END
GO

-- ============================================
-- 12. Data Source Configurations Table
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DataSourceConfigurations]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DataSourceConfigurations] (
        [DataSourceConfigId] INT IDENTITY(1,1) PRIMARY KEY,
        [DataSourceType] NVARCHAR(50) NOT NULL, -- "SQL", "API", "Kudu"
        [ConfigName] NVARCHAR(100) NOT NULL,
        [DisplayName] NVARCHAR(255) NOT NULL,

        -- Connection Info
        [ConnectionString] NVARCHAR(1000) NULL,
        [BaseUrl] NVARCHAR(500) NULL,
        [AuthenticationType] NVARCHAR(50) NULL,
        [AuthenticationConfig] NVARCHAR(MAX) NULL,

        -- Settings
        [IsActive] BIT NOT NULL DEFAULT 1,
        [IsDefault] BIT NOT NULL DEFAULT 0,
        [TimeoutSeconds] INT NOT NULL DEFAULT 30,
        [MaxRowsPerQuery] INT NOT NULL DEFAULT 10000,

        -- Audit
        [CreatedByUserId] INT NOT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ModifiedByUserId] INT NULL,
        [ModifiedDate] DATETIME NULL,

        CONSTRAINT [FK_DataSourceConfigurations_CreatedBy] FOREIGN KEY ([CreatedByUserId])
            REFERENCES [dbo].[Users]([Users_ID]),
        CONSTRAINT [FK_DataSourceConfigurations_ModifiedBy] FOREIGN KEY ([ModifiedByUserId])
            REFERENCES [dbo].[Users]([Users_ID]),
        CONSTRAINT [UQ_DataSourceConfigurations] UNIQUE ([DataSourceType], [ConfigName])
    );

    PRINT 'Table [DataSourceConfigurations] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [DataSourceConfigurations] already exists';
END
GO

-- ============================================
-- Migration Summary
-- ============================================
PRINT '';
PRINT '==============================================';
PRINT 'Query Builder Migration Completed Successfully';
PRINT '==============================================';
PRINT 'Created 12 tables:';
PRINT '  1. ReportDefinitions';
PRINT '  2. ReportColumnDefinitions';
PRINT '  3. QueryTableReferences';
PRINT '  4. QueryFilters';
PRINT '  5. AllowedTables';
PRINT '  6. AllowedColumns';
PRINT '  7. TableRelationships';
PRINT '  8. ReportExecutionLog';
PRINT '  9. ReportTemplates';
PRINT ' 10. ReportSharing';
PRINT ' 11. UserFavoriteReports';
PRINT ' 12. DataSourceConfigurations';
PRINT '==============================================';
GO
