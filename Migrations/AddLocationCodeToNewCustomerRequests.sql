IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[NewCustomerRequests]') AND name = 'LocationCode'
)
BEGIN
    ALTER TABLE [dbo].[NewCustomerRequests]
    ADD [LocationCode] NVARCHAR(10) NULL;
    PRINT 'Added LocationCode to NewCustomerRequests';
END
GO
