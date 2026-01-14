-- =============================================
-- Grant Settings Permission to General Managers
-- Description: Ensures General Managers have Settings access for location switching
-- Created: 2026-01-14
-- Issue: Location switcher disappeared for GMs after permission system update
-- =============================================

-- General Managers need Settings permission to access the location switcher
-- and to configure branch-level settings

DECLARE @GeneralManagerRoleId INT = 4;
DECLARE @SettingsFeatureId INT = (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'Settings');

-- Grant Settings permission to all active General Managers who don't have it
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    @SettingsFeatureId,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    1 AS GrantedByUsers_ID  -- Granted by system/admin
FROM
    [dbo].[Users] u
WHERE
    u.IsActive = 1
    AND u.RoleID = @GeneralManagerRoleId
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID
          AND ufp.FeatureID = @SettingsFeatureId
    );

DECLARE @RowsAffected INT = @@ROWCOUNT;
PRINT 'Granted Settings permission to ' + CAST(@RowsAffected AS VARCHAR(10)) + ' General Manager(s)';

GO

-- Verification
SELECT
    u.Users_ID,
    u.UserID,
    u.FirstName + ' ' + u.LastName AS UserName,
    r.RoleName,
    COUNT(ula.LocationID) AS AssignedLocations,
    CASE WHEN ufp.HasAccess = 1 THEN 'Yes' ELSE 'No' END AS HasSettingsAccess
FROM
    [dbo].[Users] u
    INNER JOIN [dbo].[Roles] r ON u.RoleID = r.RoleID
    LEFT JOIN [dbo].[UserLocationAssignments] ula ON u.Users_ID = ula.UserID AND ula.IsActive = 'YES'
    LEFT JOIN [dbo].[UserFeaturePermissions] ufp ON u.Users_ID = ufp.Users_ID
        AND ufp.FeatureID = (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'Settings')
WHERE
    u.IsActive = 1
    AND u.RoleID = 4  -- General Manager
GROUP BY
    u.Users_ID, u.UserID, u.FirstName, u.LastName, r.RoleName, ufp.HasAccess
ORDER BY
    u.FirstName;

PRINT '';
PRINT 'All General Managers should now have Settings permission and be able to see the location switcher.';

GO
