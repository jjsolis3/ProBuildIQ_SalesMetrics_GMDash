-- Migration: Add PropertyName to SignEnvelope
-- Date: 2026-02-19
-- Description: Adds a free-text PropertyName column to SignEnvelope so that
--              staff can still capture the property name when no matching ERP
--              record is found during envelope creation (e.g. order not yet entered).

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[SignEnvelope]') AND name = 'PropertyName')
BEGIN
    ALTER TABLE [dbo].[SignEnvelope]
    ADD [PropertyName] nvarchar(200) NULL;
END
GO

PRINT 'Migration AddPropertyNameToSignEnvelope completed successfully';
GO
