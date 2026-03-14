-- Adds TemplateType column to SignTemplate.
-- "Consent" = signature required; "Communication" = tracked delivery, no signature.
-- Defaults to "Consent" for all existing templates.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[SignTemplate]') AND name = 'TemplateType'
)
BEGIN
    ALTER TABLE [dbo].[SignTemplate]
    ADD [TemplateType] NVARCHAR(20) NOT NULL DEFAULT 'Consent';
    PRINT 'Added TemplateType to SignTemplate';
END
GO
