-- Migration: Add Envelope Notification Settings Table
-- Date: 2026-02-17
-- Description: Creates EnvelopeNotificationSettings table to store company-wide and
--              branch-specific email addresses for envelope completion notifications.

-- ===================================================================
-- 1. Create EnvelopeNotificationSettings table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EnvelopeNotificationSettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[EnvelopeNotificationSettings] (
        [EnvelopeNotificationSettingsId] int IDENTITY(1,1) NOT NULL,
        [LocationCode]       nvarchar(10)  NULL,           -- NULL = company-wide; 'LAX', 'LSV', etc. = branch-specific
        [LocationName]       nvarchar(100) NOT NULL,        -- Human-readable label, e.g. 'Company (All Branches)'
        [NotificationEmail]  nvarchar(255) NULL,            -- Email address to notify; NULL/empty = no notification sent
        [IsEnabled]          bit           NOT NULL DEFAULT 1,
        [LastModifiedDate]   datetime      NOT NULL DEFAULT GETDATE(),
        [LastModifiedByUserId] int         NOT NULL DEFAULT 1,
        CONSTRAINT [PK_EnvelopeNotificationSettings] PRIMARY KEY CLUSTERED ([EnvelopeNotificationSettingsId] ASC)
    );

    -- Unique index on LocationCode (allows one NULL for company-wide + one per branch code)
    CREATE UNIQUE NONCLUSTERED INDEX [IX_EnvelopeNotificationSettings_LocationCode]
    ON [dbo].[EnvelopeNotificationSettings] ([LocationCode] ASC)
    WHERE [LocationCode] IS NOT NULL;
END
GO

-- ===================================================================
-- 2. Seed default rows (idempotent)
-- ===================================================================

-- Company-wide (LocationCode = NULL)
IF NOT EXISTS (SELECT 1 FROM [dbo].[EnvelopeNotificationSettings] WHERE [LocationCode] IS NULL)
BEGIN
    INSERT INTO [dbo].[EnvelopeNotificationSettings]
        ([LocationCode], [LocationName], [NotificationEmail], [IsEnabled], [LastModifiedByUserId])
    VALUES
        (NULL, 'Company (All Branches)', NULL, 1, 1);
END
GO

-- Los Angeles (LAX)
IF NOT EXISTS (SELECT 1 FROM [dbo].[EnvelopeNotificationSettings] WHERE [LocationCode] = 'LAX')
BEGIN
    INSERT INTO [dbo].[EnvelopeNotificationSettings]
        ([LocationCode], [LocationName], [NotificationEmail], [IsEnabled], [LastModifiedByUserId])
    VALUES
        ('LAX', 'Los Angeles', NULL, 1, 1);
END
GO

-- Las Vegas (LSV)
IF NOT EXISTS (SELECT 1 FROM [dbo].[EnvelopeNotificationSettings] WHERE [LocationCode] = 'LSV')
BEGIN
    INSERT INTO [dbo].[EnvelopeNotificationSettings]
        ([LocationCode], [LocationName], [NotificationEmail], [IsEnabled], [LastModifiedByUserId])
    VALUES
        ('LSV', 'Las Vegas', NULL, 1, 1);
END
GO

-- Chino (CHN)
IF NOT EXISTS (SELECT 1 FROM [dbo].[EnvelopeNotificationSettings] WHERE [LocationCode] = 'CHN')
BEGIN
    INSERT INTO [dbo].[EnvelopeNotificationSettings]
        ([LocationCode], [LocationName], [NotificationEmail], [IsEnabled], [LastModifiedByUserId])
    VALUES
        ('CHN', 'Chino', NULL, 1, 1);
END
GO

-- Phoenix (PHX)
IF NOT EXISTS (SELECT 1 FROM [dbo].[EnvelopeNotificationSettings] WHERE [LocationCode] = 'PHX')
BEGIN
    INSERT INTO [dbo].[EnvelopeNotificationSettings]
        ([LocationCode], [LocationName], [NotificationEmail], [IsEnabled], [LastModifiedByUserId])
    VALUES
        ('PHX', 'Phoenix', NULL, 1, 1);
END
GO

-- San Diego (SND)
IF NOT EXISTS (SELECT 1 FROM [dbo].[EnvelopeNotificationSettings] WHERE [LocationCode] = 'SND')
BEGIN
    INSERT INTO [dbo].[EnvelopeNotificationSettings]
        ([LocationCode], [LocationName], [NotificationEmail], [IsEnabled], [LastModifiedByUserId])
    VALUES
        ('SND', 'San Diego', NULL, 1, 1);
END
GO

PRINT 'AddEnvelopeNotificationSettings migration completed successfully!';
