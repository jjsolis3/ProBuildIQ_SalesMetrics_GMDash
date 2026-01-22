-- =============================================
-- Add Office Dashboard Permission
-- Description: Adds "Office Dashboard" feature to the Features table
-- Created: 2026-01-22
-- =============================================

-- Insert Office Dashboard feature if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'OfficeDashboard')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('OfficeDashboard', 'Office Dashboard', 'Access to Office Dashboard with envelope metrics and business data', 'Dashboard', 2, 1);

    PRINT 'Office Dashboard feature added successfully';
END
ELSE
BEGIN
    PRINT 'Office Dashboard feature already exists, skipping';
END
GO

-- Grant Office Dashboard permission to Office Manager (RoleID = 5) and Office Staff (RoleID = 6)
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
    AND u.RoleID IN (5, 6) -- Office Manager and Office Staff
    AND f.FeatureCode = 'OfficeDashboard'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );

PRINT 'Office Dashboard access granted to Office Manager and Office Staff users';

-- Optionally, grant to Admins and GMs as well
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
    AND u.RoleID IN (1, 4, 7, 8) -- Admin, GM, President/Owner, Regional Manager
    AND f.FeatureCode = 'OfficeDashboard'
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID AND ufp.FeatureID = f.FeatureID
    );

PRINT 'Office Dashboard access granted to Admin and GM users';
GO

-- Verification
SELECT
    u.Users_ID,
    u.FirstName,
    u.LastName,
    r.RoleName,
    f.FeatureName
FROM
    [dbo].[Users] u
    INNER JOIN [dbo].[UserFeaturePermissions] ufp ON u.Users_ID = ufp.Users_ID
    INNER JOIN [dbo].[Features] f ON ufp.FeatureID = f.FeatureID
    LEFT JOIN [dbo].[Roles] r ON u.RoleID = r.RoleID
WHERE
    f.FeatureCode = 'OfficeDashboard'
    AND u.IsActive = 1
ORDER BY
    r.RoleName, u.FirstName;

PRINT 'Office Dashboard Permission migration completed successfully!';
GO
