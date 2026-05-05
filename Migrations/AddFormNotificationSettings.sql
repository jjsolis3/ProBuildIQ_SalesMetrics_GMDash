IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FormNotificationSettings]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[FormNotificationSettings] (
        [FormNotificationSettingsId]  INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [LocationCode]                NVARCHAR(10)  NULL,
        [LocationName]                NVARCHAR(100) NOT NULL,
        [NotificationEmail]           NVARCHAR(255) NULL,
        [IsEnabled]                   BIT           NOT NULL CONSTRAINT DF_FormNotificationSettings_IsEnabled DEFAULT 1,
        [LastModifiedDate]            DATETIME      NOT NULL CONSTRAINT DF_FormNotificationSettings_LastModifiedDate DEFAULT GETDATE(),
        [LastModifiedByUserId]        INT           NOT NULL
    );

    -- Seed default rows (one per location + company-wide)
    INSERT INTO [dbo].[FormNotificationSettings] (LocationCode, LocationName, NotificationEmail, IsEnabled, LastModifiedDate, LastModifiedByUserId)
    VALUES
        (NULL,  'Company (All Branches)', NULL, 1, GETDATE(), 1),
        ('LAX', 'Los Angeles',            NULL, 1, GETDATE(), 1),
        ('LSV', 'Las Vegas',              NULL, 1, GETDATE(), 1),
        ('CHN', 'Chino',                  NULL, 1, GETDATE(), 1),
        ('PHX', 'Phoenix',                NULL, 1, GETDATE(), 1),
        ('SND', 'San Diego',              NULL, 1, GETDATE(), 1);

    PRINT 'Created FormNotificationSettings table with default rows';
END
GO
