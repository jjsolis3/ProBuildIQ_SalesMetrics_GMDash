-- =============================================
-- Grant Settings Permission to General Managers (UPSERT Version)
-- Description: Ensures ALL General Managers have Settings access for location switching
-- This version handles both INSERT (new records) and UPDATE (existing disabled records)
-- Created: 2026-01-14
-- =============================================

USE SalesMetrics;
GO

DECLARE @GeneralManagerRoleId INT = 4;
DECLARE @SettingsFeatureId INT = (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'Settings');

PRINT '======================================';
PRINT 'Granting Settings Permission to ALL General Managers';
PRINT '======================================';
PRINT '';

-- Step 1: UPDATE existing Settings permissions that are disabled (HasAccess = 0)
UPDATE ufp
SET ufp.HasAccess = 1,
    ufp.GrantedDate = GETDATE(),
    ufp.GrantedByUsers_ID = 1  -- System/Admin
FROM [dbo].[UserFeaturePermissions] ufp
INNER JOIN [dbo].[Users] u ON ufp.Users_ID = u.Users_ID
WHERE
    u.IsActive = 1
    AND u.RoleID = @GeneralManagerRoleId
    AND ufp.FeatureID = @SettingsFeatureId
    AND ufp.HasAccess = 0;  -- Only update disabled permissions

DECLARE @UpdatedCount INT = @@ROWCOUNT;
PRINT 'Updated ' + CAST(@UpdatedCount AS VARCHAR(10)) + ' existing Settings permission(s) from disabled to enabled';

-- Step 2: INSERT new Settings permissions for GMs who don't have any record yet
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    @SettingsFeatureId,
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    1 AS GrantedByUsers_ID  -- System/Admin
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

DECLARE @InsertedCount INT = @@ROWCOUNT;
PRINT 'Inserted ' + CAST(@InsertedCount AS VARCHAR(10)) + ' new Settings permission(s)';
PRINT '';
PRINT 'Total General Managers affected: ' + CAST((@UpdatedCount + @InsertedCount) AS VARCHAR(10));
PRINT '';

GO

-- =============================================
-- Verification Query
-- =============================================
PRINT '======================================';
PRINT 'Verification: All General Managers and Their Settings Access';
PRINT '======================================';

SELECT
    u.Users_ID,
    u.UserID,
    u.UserName,
    u.FirstName + ' ' + u.LastName AS FullName,
    r.RoleName,
    COUNT(DISTINCT ula.LocationID) AS AssignedLocations,
    CASE
        WHEN ufp.HasAccess = 1 THEN 'Yes'
        WHEN ufp.HasAccess = 0 THEN 'No (Disabled)'
        ELSE 'No (Missing)'
    END AS HasSettingsAccess,
    ufp.GrantedDate,
    CASE
        WHEN ufp.HasAccess = 1 AND COUNT(DISTINCT ula.LocationID) > 1 THEN 'OK - Can switch locations'
        WHEN ufp.HasAccess = 1 AND COUNT(DISTINCT ula.LocationID) = 1 THEN 'OK - Single location'
        WHEN ufp.HasAccess = 1 AND COUNT(DISTINCT ula.LocationID) = 0 THEN 'WARNING - No locations assigned'
        ELSE 'ERROR - Cannot switch locations'
    END AS Status
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
    u.Users_ID, u.UserID, u.UserName, u.FirstName, u.LastName, r.RoleName, ufp.HasAccess, ufp.GrantedDate
ORDER BY
    CASE WHEN ufp.HasAccess = 1 THEN 0 ELSE 1 END,  -- Show enabled first
    u.FirstName;

PRINT '';
PRINT '======================================';
PRINT 'All General Managers should now have Settings permission enabled';
PRINT 'and be able to see the location switcher if they have multiple locations.';
PRINT '======================================';

-- Show any remaining issues
IF EXISTS (
    SELECT 1 FROM [dbo].[Users] u
    LEFT JOIN [dbo].[UserFeaturePermissions] ufp ON u.Users_ID = ufp.Users_ID
        AND ufp.FeatureID = (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'Settings')
    WHERE u.IsActive = 1
      AND u.RoleID = 4
      AND (ufp.HasAccess IS NULL OR ufp.HasAccess = 0)
)
BEGIN
    PRINT '';
    PRINT '!!! WARNING: Some General Managers still do not have Settings access !!!';
    PRINT 'Please review the verification results above.';
END
ELSE
BEGIN
    PRINT '';
    PRINT '✓ SUCCESS: All General Managers now have Settings access!';
END

GO
