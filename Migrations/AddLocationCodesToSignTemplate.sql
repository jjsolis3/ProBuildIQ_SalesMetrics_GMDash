-- Adds LocationCodes column to SignTemplate for multi-branch visibility.
-- Comma-separated list of branch codes (e.g. "LAX,CHN,SND"); NULL/empty means visible to all branches.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[SignTemplate]') AND name = 'LocationCodes'
)
BEGIN
    ALTER TABLE [dbo].[SignTemplate]
    ADD [LocationCodes] NVARCHAR(100) NULL;
    PRINT 'Added LocationCodes to SignTemplate';
END
GO
