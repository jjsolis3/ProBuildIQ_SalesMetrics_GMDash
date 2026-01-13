-- =============================================
-- Consolidate Report Permissions
-- Description: Simplifies report permissions to two levels: View/Run and Create
-- Created: 2026-01-13
-- =============================================

-- Step 1: Update the main Reports feature
UPDATE [dbo].[Features]
SET FeatureName = 'Reports',
    Description = 'Access to view and run reports catalog',
    Category = 'Reports',
    DisplayOrder = 80
WHERE FeatureCode = 'Reports';

PRINT 'Updated Reports feature';

-- Step 2: Add new ReportsCreate feature if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM [dbo].[Features] WHERE [FeatureCode] = 'ReportsCreate')
BEGIN
    INSERT INTO [dbo].[Features] ([FeatureCode], [FeatureName], [Description], [Category], [DisplayOrder], [IsActive])
    VALUES ('ReportsCreate', 'Create Report Queries', 'Access to create new report definitions and custom queries', 'Reports', 81, 1);
    PRINT 'Added ReportsCreate feature';
END

-- Step 3: Migrate existing users from individual report permissions to consolidated Reports permission
-- Anyone with ANY report permission gets the base Reports permission
INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT DISTINCT
    ufp.Users_ID,
    (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'Reports'),
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[UserFeaturePermissions] ufp
    INNER JOIN [dbo].[Features] f ON ufp.FeatureID = f.FeatureID
WHERE
    f.FeatureCode IN ('Reports', 'ReportMarginCommission', 'ReportSalesMetrics')
    AND ufp.HasAccess = 1
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp2
        WHERE ufp2.Users_ID = ufp.Users_ID
          AND ufp2.FeatureID = (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'Reports')
    );

PRINT 'Migrated existing report permissions to consolidated Reports permission';

-- Step 4: Grant ReportsCreate to Admin and GM users only (they can create custom queries)
DECLARE @AdminRoleId INT = 1;
DECLARE @GeneralManagerRoleId INT = 4;

INSERT INTO [dbo].[UserFeaturePermissions] ([Users_ID], [FeatureID], [HasAccess], [GrantedDate], [GrantedByUsers_ID])
SELECT
    u.Users_ID,
    (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'ReportsCreate'),
    1 AS HasAccess,
    GETDATE() AS GrantedDate,
    NULL AS GrantedByUsers_ID
FROM
    [dbo].[Users] u
WHERE
    u.IsActive = 1
    AND u.RoleID IN (@AdminRoleId, @GeneralManagerRoleId)
    AND NOT EXISTS (
        SELECT 1 FROM [dbo].[UserFeaturePermissions] ufp
        WHERE ufp.Users_ID = u.Users_ID
          AND ufp.FeatureID = (SELECT FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'ReportsCreate')
    );

PRINT 'Granted ReportsCreate permission to Admin and GM users';

-- Step 5: Disable the individual report features (keep for historical data, but not active)
UPDATE [dbo].[Features]
SET IsActive = 0,
    Description = 'DEPRECATED - Consolidated into Reports permission'
WHERE FeatureCode IN ('ReportMarginCommission', 'ReportSalesMetrics');

PRINT 'Disabled individual report permissions (ReportMarginCommission, ReportSalesMetrics)';

GO

-- =============================================
-- Verification
-- =============================================

-- View active report-related features
SELECT
    FeatureID,
    FeatureCode,
    FeatureName,
    Description,
    IsActive
FROM [dbo].[Features]
WHERE Category = 'Reports'
   OR FeatureCode IN ('Reports', 'ReportsCreate', 'ReportMarginCommission', 'ReportSalesMetrics')
ORDER BY IsActive DESC, DisplayOrder;

PRINT '';
PRINT 'Active Report Features:';
PRINT '- Reports: View and run existing reports';
PRINT '- ReportsCreate: Create new report queries and definitions';

-- View users with report permissions
SELECT
    u.Users_ID,
    u.UserID,
    u.FirstName + ' ' + u.LastName AS UserName,
    r.RoleName,
    f.FeatureName,
    ufp.HasAccess
FROM
    [dbo].[Users] u
    LEFT JOIN [dbo].[Roles] r ON u.RoleID = r.RoleID
    INNER JOIN [dbo].[UserFeaturePermissions] ufp ON u.Users_ID = ufp.Users_ID
    INNER JOIN [dbo].[Features] f ON ufp.FeatureID = f.FeatureID
WHERE
    u.IsActive = 1
    AND f.FeatureCode IN ('Reports', 'ReportsCreate')
    AND ufp.HasAccess = 1
ORDER BY
    r.RoleName, u.FirstName, f.FeatureCode;

PRINT '';
PRINT 'Report permissions consolidated successfully!';
PRINT 'Base access (Reports): View and run existing reports';
PRINT 'Advanced access (ReportsCreate): Create custom report queries';

GO
