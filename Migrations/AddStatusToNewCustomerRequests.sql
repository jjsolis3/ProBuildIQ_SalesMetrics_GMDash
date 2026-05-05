IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[NewCustomerRequests]') AND name = 'Status'
)
BEGIN
    ALTER TABLE [dbo].[NewCustomerRequests]
    ADD [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_NewCustomerRequests_Status DEFAULT 'Pending';
    PRINT 'Added Status to NewCustomerRequests';
END
GO
