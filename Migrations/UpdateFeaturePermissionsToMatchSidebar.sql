-- =============================================
-- Feature Permissions Update - Match Sidebar Structure
-- Description: Updates Features table to match actual application sidebar structure
-- Created: 2026-01-13
-- =============================================

-- Disable features that don't match current sidebar or are being renamed
UPDATE [dbo].[Features] SET IsActive = 0 WHERE FeatureCode IN ('Accounts', 'Notifications');

-- Update existing features to match sidebar naming
UPDATE [dbo].[Features]
SET FeatureName = 'Sales Dashboard',
    Description = 'Access to sales dashboard and analytics',
    DisplayOrder = 10
WHERE FeatureCode = 'Dashboard';

UPDATE [dbo].[Features]
SET FeatureName = 'Work Orders / Job Schedule',
    Description = 'View and manage work orders and job schedules',
    DisplayOrder = 40
WHERE FeatureCode = 'Orders';

UPDATE [dbo].[Features]
SET FeatureName = 'Tasks & Calendar',
    Description = 'Access to task management and calendar',
    DisplayOrder = 20
WHERE FeatureCode = 'Tasks';

UPDATE [dbo].[Features]
SET FeatureName = 'Access Controls & User Management',
    Description = 'Manage users, roles, and permissions',
    DisplayOrder = 100
WHERE FeatureCode = 'Users';

UPDATE [dbo].[Features]
SET FeatureName = 'System Settings',
    Description = 'Access to system settings and configuration',
    DisplayOrder = 101
WHERE FeatureCode = 'Settings';

UPDATE [dbo].[Features]
SET FeatureName = 'Reports Catalog',
    Description = 'Access to reports catalog and running reports',
    DisplayOrder = 80
WHERE FeatureCode = 'Reports';

UPDATE [dbo].[Features]
SET FeatureName = 'Yardi Properties',
    Description = 'Access to Yardi property management and prospecting',
    Category = 'Prospecting',
    DisplayOrder = 70
WHERE FeatureCode = 'Yardi';

UPDATE [dbo].[Features]
SET FeatureName = 'Document Signing & Envelopes',
    Description = 'Access to DocuSign envelope signing features',
    Category = 'Integration',
    DisplayOrder = 110
WHERE FeatureCode = 'Signing';

-- Insert new features that match the sidebar structure
-- Check and insert only if they don't exist

-- GENERAL MGR section features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'GMDashboard')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('GMDashboard', 'GM Dashboard', 'Access to General Manager dashboard and analytics', 'GM', 1, 1);
    PRINT 'Added GMDashboard feature';
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'GMRecap')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('GMRecap', 'GM Weekly Recap Entry & View', 'Access to enter and view weekly recap reports', 'GM', 2, 1);
    PRINT 'Added GMRecap feature';
END

-- SALES REPS section features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'SalesPulse')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('SalesPulse', 'Sales Pulse', 'Access to sales representative metrics and performance tracking', 'Sales', 30, 1);
    PRINT 'Added SalesPulse feature';
END

-- JOURNAL section features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'Journal')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('Journal', 'My Journal', 'Access to personal journal and timeline', 'Core', 35, 1);
    PRINT 'Added Journal feature';
END

-- MANAGEMENT section features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'Management')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('Management', 'Management Companies', 'Access to management company information and tracking', 'Core', 50, 1);
    PRINT 'Added Management feature';
END

-- PROPERTIES section features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'Properties')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('Properties', 'Properties', 'Access to property information and management', 'Core', 60, 1);
    PRINT 'Added Properties feature';
END

-- COMMUNICATION section features (Announcements already exists, just ensure it's configured)
UPDATE [dbo].[Features]
SET Category = 'Communication',
    DisplayOrder = 90
WHERE FeatureCode = 'Announcements';

-- WEB APP ERRORS section features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'ErrorLogs')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('ErrorLogs', 'Web App Errors', 'Access to error logs and diagnostics', 'Admin', 120, 1);
    PRINT 'Added ErrorLogs feature';
END

-- Granular Envelope/Signing features
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'Envelopes')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('Envelopes', 'Send & Review Envelopes', 'Access to send and review DocuSign envelopes', 'Integration', 111, 1);
    PRINT 'Added Envelopes feature';
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'EnvelopeTemplates')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('EnvelopeTemplates', 'Create & Manage Envelope Templates', 'Access to create and manage DocuSign templates', 'Integration', 112, 1);
    PRINT 'Added EnvelopeTemplates feature';
END

-- Form Requests feature
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'FormRequests')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('FormRequests', 'Form Requests', 'Access to submit and view form requests (e.g., New Customer Form)', 'Core', 36, 1);
    PRINT 'Added FormRequests feature';
END

-- Yardi Upload (Admin feature)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'YardiUpload')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('YardiUpload', 'Yardi Upload', 'Access to upload Yardi data files', 'Admin', 102, 1);
    PRINT 'Added YardiUpload feature';
END

GO

-- =============================================
-- Grant default permissions based on roles
-- =============================================

DECLARE @AdminRoleId INT = 1;
DECLARE @SalesRoleId INT = 2;
DECLARE @SalesManagerRoleId INT = 3;
DECLARE @GeneralManagerRoleId INT = 4;

PRINT 'Granting new feature permissions to users based on roles...';

-- Grant GM features to GM and Admin only
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID IN (@AdminRoleId, @GeneralManagerRoleId)
    AND f.FeatureCode IN ('GMDashboard', 'GMRecap')
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'GM features granted to Admin and General Manager';

-- Grant SalesPulse to GM, Admin, and Sales Managers
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID IN (@AdminRoleId, @GeneralManagerRoleId, @SalesManagerRoleId)
    AND f.FeatureCode = 'SalesPulse'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'SalesPulse granted to Admin, GM, and Sales Managers';

-- Grant Journal, Management, and Properties to ALL active users
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND f.FeatureCode IN ('Journal', 'Management', 'Properties')
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'Journal, Management, and Properties granted to all active users';

-- Grant ErrorLogs to Admin only
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID = @AdminRoleId
    AND f.FeatureCode = 'ErrorLogs'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'ErrorLogs granted to Admin';

-- Grant Envelopes to users who have Signing access
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND f.FeatureCode = 'Envelopes'
    AND EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        INNER JOIN [dbo].[Features] sf ON ufp.FeatureID = sf.FeatureID
        WHERE ufp.Users_ID = u.Users_ID AND sf.FeatureCode = 'Signing' AND ufp.HasAccess = 1
    )
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp2
        WHERE ufp2.Users_ID = u.Users_ID AND ufp2.FeatureID = f.FeatureID
    );
PRINT 'Envelopes granted to users with Signing access';

-- Grant EnvelopeTemplates to Admin only
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID = @AdminRoleId
    AND f.FeatureCode = 'EnvelopeTemplates'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'EnvelopeTemplates granted to Admin';

-- Grant FormRequests to Admin only (can be adjusted later for specific users)
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID IN (@AdminRoleId, @GeneralManagerRoleId)
    AND f.FeatureCode = 'FormRequests'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'FormRequests granted to Admin and GM';

-- Grant YardiUpload to Admin only
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID = @AdminRoleId
    AND f.FeatureCode = 'YardiUpload'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'YardiUpload granted to Admin';

-- Grant Settings to Admin and General Manager (required for location switcher)
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    f.FeatureID,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
    CROSS JOIN [dbo].[Features] f
WHERE
    u.IsActive = 1
    AND u.RoleID IN (@AdminRoleId, @GeneralManagerRoleId)
    AND f.FeatureCode = 'Settings'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );
PRINT 'Settings granted to Admin and GM (required for location switcher)';

GO

-- =============================================
-- Verification queries
-- =============================================

-- View all active features ordered by category and display order
SELECT
    FeatureID,
    FeatureCode,
    FeatureName,
    Description,
    Category,
    DisplayOrder,
    IsActive
FROM [dbo].[Features]
WHERE IsActive = 1
ORDER BY
    CASE Category
        WHEN 'GM' THEN 1
        WHEN 'Core' THEN 2
        WHEN 'Sales' THEN 3
        WHEN 'Prospecting' THEN 4
        WHEN 'Reports' THEN 5
        WHEN 'Communication' THEN 6
        WHEN 'Admin' THEN 7
        WHEN 'Integration' THEN 8
        ELSE 9
    END,
    DisplayOrder;

-- View permission count by user
SELECT
    u.Users_ID,
    u.UserID,
    u.FirstName + ' ' + u.LastName AS UserName,
    r.RoleName,
    COUNT(ufp.PermissionID) AS PermissionCount
FROM
    [dbo].[Users] u
    LEFT JOIN [dbo].[Roles] r ON u.RoleID = r.RoleID
    LEFT JOIN [dbo].[UserFeaturePermissions] ufp ON u.Users_ID = ufp.Users_ID
WHERE
    u.IsActive = 1
GROUP BY
    u.Users_ID, u.UserID, u.FirstName, u.LastName, r.RoleName
ORDER BY
    r.RoleName, u.FirstName;

PRINT 'Feature Permissions updated successfully to match sidebar structure!';
GO
