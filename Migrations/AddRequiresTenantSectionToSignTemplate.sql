-- Migration: AddRequiresTenantSectionToSignTemplate
-- Adds RequiresTenantSection flag to SignTemplate.
-- When false, the signing page will NOT show the Tenant Information capture block.
-- Defaults to 1 (true) for backward compatibility with all existing templates.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SignTemplate') AND name = 'RequiresTenantSection'
)
BEGIN
    ALTER TABLE [dbo].[SignTemplate]
        ADD [RequiresTenantSection] BIT NOT NULL DEFAULT 1;
END
