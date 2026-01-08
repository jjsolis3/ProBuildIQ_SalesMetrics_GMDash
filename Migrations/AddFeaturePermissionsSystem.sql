-- =============================================
-- Feature Permissions System Migration
-- Description: Adds Features and UserFeaturePermissions tables for dynamic access control
-- Created: 2026-01-08
-- =============================================

-- Create Features table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Features]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Features] (
        [FeatureID] INT IDENTITY(1,1) NOT NULL,
        [FeatureCode] NVARCHAR(50) NOT NULL,
        [FeatureName] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [Category] NVARCHAR(50) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Features] PRIMARY KEY CLUSTERED ([FeatureID] ASC)
    );

    -- Create unique index on FeatureCode
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Features_FeatureCode] ON [dbo].[Features]
    (
        [FeatureCode] ASC
    );

    PRINT 'Features table created successfully';
END
GO

-- Create UserFeaturePermissions table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserFeaturePermissions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserFeaturePermissions] (
        [PermissionID] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NOT NULL,
        [FeatureID] INT NOT NULL,
        [HasAccess] BIT NOT NULL DEFAULT 1,
        [GrantedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [GrantedByUserID] INT NULL,
        [ExpiresDate] DATETIME NULL,
        CONSTRAINT [PK_UserFeaturePermissions] PRIMARY KEY CLUSTERED ([PermissionID] ASC)
    );

    -- Create unique index on UserID + FeatureID combination
    CREATE UNIQUE NONCLUSTERED INDEX [IX_UserFeaturePermissions_UserID_FeatureID] ON [dbo].[UserFeaturePermissions]
    (
        [UserID] ASC,
        [FeatureID] ASC
    );

    -- Add foreign key to Users table
    ALTER TABLE [dbo].[UserFeaturePermissions]
    ADD CONSTRAINT [FK_UserFeaturePermissions_Users]
        FOREIGN KEY ([UserID])
        REFERENCES [dbo].[Users] ([UserID])
        ON DELETE CASCADE;

    -- Add foreign key to Features table
    ALTER TABLE [dbo].[UserFeaturePermissions]
    ADD CONSTRAINT [FK_UserFeaturePermissions_Features]
        FOREIGN KEY ([FeatureID])
        REFERENCES [dbo].[Features] ([FeatureID])
        ON DELETE CASCADE;

    PRINT 'UserFeaturePermissions table created successfully';
END
GO

-- =============================================
-- Seed default features
-- =============================================

-- Clear existing features (only if needed for fresh install)
-- DELETE FROM [dbo].[Features];

-- Insert default features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'Dashboard')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder])
    VALUES
        ('Dashboard', 'Dashboard', 'Access to main dashboard and analytics', 'Core', 1),
        ('Reports', 'Reports', 'Access to reports catalog and running reports', 'Core', 2),
        ('Orders', 'Orders', 'View and manage orders', 'Core', 3),
        ('Tasks', 'Tasks', 'Access to tasks management', 'Core', 4),
        ('Accounts', 'Accounts', 'Access to accounts and billing', 'Core', 5),
        ('Yardi', 'Yardi Integration', 'Access to Yardi property management features', 'Integration', 6),
        ('Signing', 'Document Signing', 'Access to DocuSign envelope signing features', 'Integration', 7),

        -- Admin features
        ('Users', 'User Management', 'Manage users, roles, and permissions', 'Admin', 10),
        ('Settings', 'Settings', 'Access to system settings and configuration', 'Admin', 11),
        ('Announcements', 'Announcements', 'Create and manage announcements', 'Admin', 12),
        ('Notifications', 'Notifications', 'Access to notification management', 'Admin', 13),

        -- Reports features (can be granular if needed)
        ('ReportMarginCommission', 'Margin Commission Report', 'Access to margin commission discrepancy reports', 'Reports', 20),
        ('ReportSalesMetrics', 'Sales Metrics Report', 'Access to sales performance metrics', 'Reports', 21);

    PRINT 'Default features seeded successfully';
END
GO

-- =============================================
-- Grant default permissions based on existing roles
-- =============================================

-- This script grants permissions to existing users based on their current roles
-- Admin (RoleID = 1): Gets access to everything
-- Sales (RoleID = 2): Gets access to core features
-- Sales Admin (RoleID = 3): Gets access to core + some admin features
-- General Manager (RoleID = 4): Gets access to core + admin features

DECLARE @AdminRoleId INT = 1;
DECLARE @SalesRoleId INT = 2;
DECLARE @SalesAdminRoleId INT = 3;
DECLARE @GeneralManagerRoleId INT = 4;

-- Core features available to all
DECLARE @CoreFeatures TABLE (FeatureCode NVARCHAR(50));
INSERT INTO @CoreFeatures VALUES ('Dashboard'), ('Orders'), ('Tasks'), ('Accounts');

-- Admin features
DECLARE @AdminFeatures TABLE (FeatureCode NVARCHAR(50));
INSERT INTO @AdminFeatures VALUES ('Users'), ('Settings'), ('Announcements'), ('Notifications');

-- Grant Core features to ALL active users
INSERT INTO [dbo].[UserFeaturePermissions] ([UserID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUserID])
SELECT
    u.UserID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUserID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND f.FeatureCode IN (SELECT FeatureCode FROM @CoreFeatures)
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.UserID = u.UserID AND ufp.FeatureID = f.FeatureID
    );

-- Grant Reports to Admin, Sales Admin, and General Manager
INSERT INTO [dbo].[UserFeaturePermissions] ([UserID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUserID])
SELECT
    u.UserID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUserID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID IN (@AdminRoleId, @SalesAdminRoleId, @GeneralManagerRoleId)
    AND f.FeatureCode = 'Reports'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.UserID = u.UserID AND ufp.FeatureID = f.FeatureID
    );

-- Grant Yardi to Sales role (RoleID = 2)
INSERT INTO [dbo].[UserFeaturePermissions] ([UserID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUserID])
SELECT
    u.UserID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUserID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID = @SalesRoleId
    AND f.FeatureCode = 'Yardi'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.UserID = u.UserID AND ufp.FeatureID = f.FeatureID
    );

-- Grant Admin features to Admin and General Manager
INSERT INTO [dbo].[UserFeaturePermissions] ([UserID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUserID])
SELECT
    u.UserID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUserID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID IN (@AdminRoleId, @GeneralManagerRoleId)
    AND f.FeatureCode IN (SELECT FeatureCode FROM @AdminFeatures)
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.UserID = u.UserID AND ufp.FeatureID = f.FeatureID
    );

-- Grant Signing to all users
INSERT INTO [dbo].[UserFeaturePermissions] ([UserID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUserID])
SELECT
    u.UserID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUserID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND f.FeatureCode = 'Signing'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.UserID = u.UserID AND ufp.FeatureID = f.FeatureID
    );

PRINT 'Default permissions granted successfully';
GO

-- =============================================
-- Verification queries (optional - comment out for production)
-- =============================================

-- View all features
SELECT * FROM [dbo].[Features] ORDER BY [Category], [DisplayOrder];

-- View permission count by user
SELECT
    u.UserID,
    u.FirstName,
    u.LastName,
    r.RoleName,
    COUNT(ufp.PermissionID) AS PermissionCount
FROM
    [dbo].[Users] u
    LEFT JOIN [dbo].[Roles] r ON u.RoleID = r.RoleID
    LEFT JOIN [dbo].[UserFeaturePermissions] ufp ON u.UserID = ufp.UserID
WHERE
    u.IsActive = 1
GROUP BY
    u.UserID, u.FirstName, u.LastName, r.RoleName
ORDER BY
    r.RoleName, u.FirstName;

PRINT 'Feature Permissions System migration completed successfully!';
GO
