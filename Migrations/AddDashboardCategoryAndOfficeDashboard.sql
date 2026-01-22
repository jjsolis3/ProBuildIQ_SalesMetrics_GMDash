-- =============================================
-- Add Dashboard Category and Office Dashboard Permission
-- Description: Updates Sales Dashboard to have Dashboard category and adds Office Dashboard
-- Created: 2026-01-22
-- =============================================

-- Update existing Dashboard feature to have Dashboard category
UPDATE [dbo].[Features]
SET Category = 'Dashboard',
    FeatureName = 'Sales Dashboard',
    Description = 'Access to sales dashboard and analytics',
    DisplayOrder = 1
WHERE FeatureCode = 'Dashboard';

PRINT 'Updated Sales Dashboard with Dashboard category';

-- Insert Office Dashboard feature if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'OfficeDashboard')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('OfficeDashboard', 'Office Dashboard', 'Access to Office Dashboard with envelope metrics and business data', 'Dashboard', 2, 1);

    PRINT 'Office Dashboard feature added successfully';
END
ELSE
BEGIN
    -- If it exists, make sure it has the right category
    UPDATE [dbo].[Features]
    SET Category = 'Dashboard',
        FeatureName = 'Office Dashboard',
        Description = 'Access to Office Dashboard with envelope metrics and business data',
        DisplayOrder = 2,
        IsActive = 1
    WHERE FeatureCode = 'OfficeDashboard';

    PRINT 'Office Dashboard feature updated';
END
GO

-- Grant Office Dashboard permission to Office Manager (RoleID = 5) and Office Staff (RoleID = 6)
DECLARE @OfficeManagerRoleId INT = 5;
DECLARE @OfficeStaffRoleId INT = 6;
DECLARE @AdminRoleId INT = 1;
DECLARE @GMRoleId INT = 4;
DECLARE @PresidentRoleId INT = 7;
DECLARE @RegionalManagerRoleId INT = 8;

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
    AND u.RoleID IN (@OfficeManagerRoleId, @OfficeStaffRoleId) -- Office Manager and Office Staff
    AND f.FeatureCode = 'OfficeDashboard'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );

PRINT 'Office Dashboard access granted to Office Manager and Office Staff users';

-- Grant to Admins and Management roles as well
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
    AND u.RoleID IN (@AdminRoleId, @GMRoleId, @PresidentRoleId, @RegionalManagerRoleId) -- Admin, GM, President/Owner, Regional Manager
    AND f.FeatureCode = 'OfficeDashboard'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );

PRINT 'Office Dashboard access granted to Admin and Management users';
GO

-- Verification - Show all Dashboard category features
SELECT
    f.FeatureID,
    f.FeatureCode,
    f.FeatureName,
    f.Category,
    f.DisplayOrder,
    f.IsActive,
    COUNT(ufp.PermissionID) as UsersWithAccess
FROM
    [dbo].[Features] f
    LEFT JOIN [dbo].[UserFeaturePermissions] ufp ON f.FeatureID = ufp.FeatureID
WHERE
    f.Category = 'Dashboard'
GROUP BY
    f.FeatureID, f.FeatureCode, f.FeatureName, f.Category, f.DisplayOrder, f.IsActive
ORDER BY
    f.DisplayOrder;

PRINT 'Dashboard Category and Office Dashboard migration completed successfully!';
GO
