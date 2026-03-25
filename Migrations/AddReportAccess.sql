-- =============================================================
-- Migration: AddReportAccess
-- Purpose  : Per-report user access control for Reports Center
--            and Query Builder reports.
-- =============================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ReportAccess')
BEGIN
    CREATE TABLE [dbo].[ReportAccess] (
        [AccessId]          INT            IDENTITY(1,1) NOT NULL,
        [ReportKey]         NVARCHAR(200)  NOT NULL,           -- "catalog:xxx" | "builder:nnn"
        [Users_ID]          INT            NOT NULL,
        [GrantedDate]       DATETIME       NOT NULL DEFAULT GETDATE(),
        [GrantedByUsers_ID] INT            NULL,

        CONSTRAINT [PK_ReportAccess] PRIMARY KEY ([AccessId]),
        CONSTRAINT [UQ_ReportAccess_Key_User] UNIQUE ([ReportKey], [Users_ID])
    );

    -- Index for fast look-up by report key
    CREATE INDEX [IX_ReportAccess_ReportKey] ON [dbo].[ReportAccess] ([ReportKey]);
    -- Index for look-up by user
    CREATE INDEX [IX_ReportAccess_Users_ID] ON [dbo].[ReportAccess] ([Users_ID]);
END;
GO

-- =============================================================
-- Migration: AddReportRestricted flag to ReportDefinitions
-- Allows each Query Builder report to individually opt-in to
-- per-user access restriction.
-- =============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ReportDefinitions' AND COLUMN_NAME = 'IsAccessRestricted'
)
BEGIN
    ALTER TABLE [dbo].[ReportDefinitions]
        ADD [IsAccessRestricted] BIT NOT NULL DEFAULT 0;
END;
GO
