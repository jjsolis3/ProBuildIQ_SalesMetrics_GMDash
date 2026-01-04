-- Migration: Add OrderNumber and Tenant Skip Tracking
-- Date: 2026-01-04
-- Description: Adds OrderNumber field and tenant skip tracking fields to SignEnvelope table

-- ===================================================================
-- 1. Add OrderNumber column to SignEnvelope table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SignEnvelope]') AND name = 'OrderNumber')
BEGIN
    ALTER TABLE [dbo].[SignEnvelope]
    ADD [OrderNumber] nvarchar(50) NULL;
END
GO

-- ===================================================================
-- 2. Add TenantSkipped column to SignEnvelope table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SignEnvelope]') AND name = 'TenantSkipped')
BEGIN
    ALTER TABLE [dbo].[SignEnvelope]
    ADD [TenantSkipped] bit NOT NULL DEFAULT 0;
END
GO

-- ===================================================================
-- 3. Add TenantSkippedByName column to SignEnvelope table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SignEnvelope]') AND name = 'TenantSkippedByName')
BEGIN
    ALTER TABLE [dbo].[SignEnvelope]
    ADD [TenantSkippedByName] nvarchar(200) NULL;
END
GO

-- ===================================================================
-- 4. Add TenantSkippedAtUtc column to SignEnvelope table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SignEnvelope]') AND name = 'TenantSkippedAtUtc')
BEGIN
    ALTER TABLE [dbo].[SignEnvelope]
    ADD [TenantSkippedAtUtc] datetime NULL;
END
GO

-- ===================================================================
-- 5. Create index on OrderNumber for faster lookups
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[SignEnvelope]') AND name = 'IX_SignEnvelope_OrderNumber')
BEGIN
    CREATE INDEX [IX_SignEnvelope_OrderNumber] ON [dbo].[SignEnvelope] ([OrderNumber]);
END
GO

PRINT 'Migration AddOrderNumberAndTenantSkipped completed successfully';
GO
