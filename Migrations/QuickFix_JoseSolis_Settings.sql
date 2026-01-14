-- =============================================
-- Quick Fix: Enable Settings Permission for Jose Solis (GM)
-- =============================================

USE SalesMetrics;
GO

-- Find Jose Solis's Users_ID (should be 16 based on the screenshot)
DECLARE @UserId INT = 16;  -- Jose Solis (GM)
DECLARE @SettingsFeatureId INT = (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'Settings');

PRINT 'Fixing Settings permission for Jose Solis (Users_ID: ' + CAST(@UserId AS VARCHAR(10)) + ')';

-- Check if record exists
IF EXISTS (
    SELECT 1 FROM [dbo].[UserFeaturePermissions]
    WHERE Users_ID = @UserId AND FeatureID = @SettingsFeatureId
)
BEGIN
    -- UPDATE existing record
    UPDATE [dbo].[UserFeaturePermissions]
    SET HasAccess = 1,
        GrantedDate = GETDATE(),
        GrantedByUsers_ID = 1
    WHERE Users_ID = @UserId
      AND FeatureID = @SettingsFeatureId;

    PRINT 'Updated existing Settings permission to HasAccess = 1';
END
ELSE
BEGIN
    -- INSERT new record
    INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
    VALUES (@UserId, @SettingsFeatureId, 1, GETDATE(), 1);

    PRINT 'Inserted new Settings permission';
END

-- Verify
SELECT
    u.Users_ID,
    u.UserID,
    u.FirstName + ' ' + u.LastName AS UserName,
    f.FeatureName,
    ufp.HasAccess,
    ufp.GrantedDate,
    COUNT(ula.LocationID) AS AssignedLocations
FROM
    [dbo].[Users] u
    INNER JOIN [dbo].[UserFeaturePermissions] ufp ON u.Users_ID = ufp.Users_ID
    INNER JOIN [dbo].[Features] f ON ufp.FeatureID = f.FeatureID
    LEFT JOIN [dbo].[UserLocationAssignments] ula ON u.Users_ID = ula.UserID AND ula.IsActive = 'YES'
WHERE
    u.Users_ID = @UserId
    AND f.FeatureCode = 'Settings'
GROUP BY
    u.Users_ID, u.UserID, u.FirstName, u.LastName, f.FeatureName, ufp.HasAccess, ufp.GrantedDate;

PRINT '';
PRINT 'Jose Solis should now have Settings access and be able to see the location switcher!';
PRINT 'Have him log out and log back in to see the changes.';

GO
