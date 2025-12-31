-- Migration: Add Announcements Hub and Settings Tables
-- Date: 2025-12-31
-- Description: Adds new columns to BroadcastMessages table and creates NotificationSettings and SecuritySettings tables

-- ===================================================================
-- 1. Add new columns to BroadcastMessages table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'ScheduledDate')
BEGIN
    ALTER TABLE [dbo].[BroadcastMessages]
    ADD [ScheduledDate] datetime NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'IsSent')
BEGIN
    ALTER TABLE [dbo].[BroadcastMessages]
    ADD [IsSent] bit NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'SentDate')
BEGIN
    ALTER TABLE [dbo].[BroadcastMessages]
    ADD [SentDate] datetime NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'SentCount')
BEGIN
    ALTER TABLE [dbo].[BroadcastMessages]
    ADD [SentCount] int NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'ReadCount')
BEGIN
    ALTER TABLE [dbo].[BroadcastMessages]
    ADD [ReadCount] int NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'IsTemplate')
BEGIN
    ALTER TABLE [dbo].[BroadcastMessages]
    ADD [IsTemplate] bit NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[BroadcastMessages]') AND name = 'TemplateCategory')
BEGIN
    ALTER TABLE [dbo].[BroadcastMessages]
    ADD [TemplateCategory] nvarchar(50) NULL;
END
GO

-- ===================================================================
-- 2. Create NotificationSettings table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NotificationSettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[NotificationSettings] (
        [NotificationSettingsID] int IDENTITY(1,1) NOT NULL,
        [CategoryName] nvarchar(50) NOT NULL,
        [IsEnabled] bit NOT NULL DEFAULT 1,
        [NotifyAssignee] bit NOT NULL DEFAULT 1,
        [NotifyManager] bit NOT NULL DEFAULT 0,
        [NotifyTaskOwner] bit NOT NULL DEFAULT 0,
        [EnableInAppNotification] bit NOT NULL DEFAULT 1,
        [EnableEmailNotification] bit NOT NULL DEFAULT 0,
        [ReminderHoursBefore] int NULL DEFAULT 24,
        [SpecificStatuses] nvarchar(255) NULL,
        [LastModifiedDate] datetime NOT NULL DEFAULT GETDATE(),
        [LastModifiedByUserID] int NOT NULL,
        CONSTRAINT [PK_NotificationSettings] PRIMARY KEY CLUSTERED ([NotificationSettingsID] ASC)
    );

    -- Create unique index on CategoryName
    CREATE UNIQUE NONCLUSTERED INDEX [IX_NotificationSettings_CategoryName]
    ON [dbo].[NotificationSettings] ([CategoryName] ASC);
END
GO

-- ===================================================================
-- 3. Create SecuritySettings table
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SecuritySettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SecuritySettings] (
        [SecuritySettingsID] int IDENTITY(1,1) NOT NULL,
        [SettingKey] nvarchar(100) NOT NULL,
        [SettingValue] nvarchar(1000) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Category] nvarchar(50) NOT NULL,
        [LastModifiedDate] datetime NOT NULL DEFAULT GETDATE(),
        [LastModifiedByUserID] int NOT NULL,
        CONSTRAINT [PK_SecuritySettings] PRIMARY KEY CLUSTERED ([SecuritySettingsID] ASC)
    );

    -- Create unique index on SettingKey
    CREATE UNIQUE NONCLUSTERED INDEX [IX_SecuritySettings_SettingKey]
    ON [dbo].[SecuritySettings] ([SettingKey] ASC);
END
GO

-- ===================================================================
-- 4. Seed default notification settings
-- ===================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[NotificationSettings] WHERE [CategoryName] = 'TaskAssigned')
BEGIN
    INSERT INTO [dbo].[NotificationSettings]
    ([CategoryName], [IsEnabled], [NotifyAssignee], [NotifyManager], [NotifyTaskOwner], [EnableInAppNotification], [EnableEmailNotification], [LastModifiedByUserID])
    VALUES
    ('TaskAssigned', 1, 1, 0, 0, 1, 0, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[NotificationSettings] WHERE [CategoryName] = 'StatusChanged')
BEGIN
    INSERT INTO [dbo].[NotificationSettings]
    ([CategoryName], [IsEnabled], [NotifyAssignee], [NotifyManager], [NotifyTaskOwner], [EnableInAppNotification], [EnableEmailNotification], [LastModifiedByUserID])
    VALUES
    ('StatusChanged', 1, 1, 0, 1, 1, 0, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[NotificationSettings] WHERE [CategoryName] = 'NoteAdded')
BEGIN
    INSERT INTO [dbo].[NotificationSettings]
    ([CategoryName], [IsEnabled], [NotifyAssignee], [NotifyManager], [NotifyTaskOwner], [EnableInAppNotification], [EnableEmailNotification], [LastModifiedByUserID])
    VALUES
    ('NoteAdded', 1, 1, 0, 0, 1, 0, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[NotificationSettings] WHERE [CategoryName] = 'DueDateReminder')
BEGIN
    INSERT INTO [dbo].[NotificationSettings]
    ([CategoryName], [IsEnabled], [NotifyAssignee], [NotifyManager], [NotifyTaskOwner], [EnableInAppNotification], [EnableEmailNotification], [ReminderHoursBefore], [LastModifiedByUserID])
    VALUES
    ('DueDateReminder', 1, 1, 0, 0, 1, 0, 24, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[NotificationSettings] WHERE [CategoryName] = 'DocumentSigning')
BEGIN
    INSERT INTO [dbo].[NotificationSettings]
    ([CategoryName], [IsEnabled], [NotifyAssignee], [NotifyManager], [NotifyTaskOwner], [EnableInAppNotification], [EnableEmailNotification], [LastModifiedByUserID])
    VALUES
    ('DocumentSigning', 1, 1, 0, 0, 1, 0, 1);
END
GO

-- ===================================================================
-- 5. Seed default security settings
-- ===================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[SecuritySettings] WHERE [SettingKey] = 'SessionTimeoutMinutes')
BEGIN
    INSERT INTO [dbo].[SecuritySettings]
    ([SettingKey], [SettingValue], [Description], [Category], [LastModifiedByUserID])
    VALUES
    ('SessionTimeoutMinutes', '30', 'Session timeout in minutes', 'Session', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SecuritySettings] WHERE [SettingKey] = 'PasswordMinLength')
BEGIN
    INSERT INTO [dbo].[SecuritySettings]
    ([SettingKey], [SettingValue], [Description], [Category], [LastModifiedByUserID])
    VALUES
    ('PasswordMinLength', '8', 'Minimum password length', 'Password', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SecuritySettings] WHERE [SettingKey] = 'RequireSpecialChar')
BEGIN
    INSERT INTO [dbo].[SecuritySettings]
    ([SettingKey], [SettingValue], [Description], [Category], [LastModifiedByUserID])
    VALUES
    ('RequireSpecialChar', 'true', 'Require special characters in password', 'Password', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SecuritySettings] WHERE [SettingKey] = 'RequireUppercase')
BEGIN
    INSERT INTO [dbo].[SecuritySettings]
    ([SettingKey], [SettingValue], [Description], [Category], [LastModifiedByUserID])
    VALUES
    ('RequireUppercase', 'true', 'Require uppercase letters in password', 'Password', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SecuritySettings] WHERE [SettingKey] = 'RequireNumber')
BEGIN
    INSERT INTO [dbo].[SecuritySettings]
    ([SettingKey], [SettingValue], [Description], [Category], [LastModifiedByUserID])
    VALUES
    ('RequireNumber', 'true', 'Require numbers in password', 'Password', 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[SecuritySettings] WHERE [SettingKey] = 'PasswordExpiryDays')
BEGIN
    INSERT INTO [dbo].[SecuritySettings]
    ([SettingKey], [SettingValue], [Description], [Category], [LastModifiedByUserID])
    VALUES
    ('PasswordExpiryDays', '90', 'Password expiry period in days (0 = never)', 'Password', 1);
END
GO

PRINT 'Migration completed successfully!';
