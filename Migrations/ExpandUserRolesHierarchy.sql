-- =============================================
-- Expand User Roles Hierarchy Migration
-- Description: Adds President/Owner and Regional Manager roles for enhanced organizational structure
-- Created: 2026-01-17
-- Author: Jose Solis
-- =============================================

PRINT 'Starting Expand User Roles Hierarchy Migration...';
GO

-- =============================================
-- Step 1: Add New Roles to Roles Table
-- =============================================

-- Check if Roles table exists (it should, but safety check)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
BEGIN
    PRINT 'ERROR: Roles table does not exist. Please ensure base schema is created first.';
    RETURN;
END
GO

-- Insert new roles if they don't exist
-- Expected Role Hierarchy (by RoleID):
-- 1 = ADMIN (Dev Admin - existing)
-- 2 = SALESPERSON (existing)
-- 3 = SALES ADMIN (existing)
-- 4 = GENERAL MANAGER (existing)
-- 5 = SALES (existing)
-- 6 = OFFICE MANAGER (existing)
-- 7 = OFFICE (existing)
-- 8 = PRESIDENT/OWNER (new)
-- 9 = REGIONAL MANAGER (new)

-- Add PRESIDENT/OWNER role
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'PRESIDENT/OWNER')
BEGIN
    INSERT INTO [dbo].[Roles] ([RoleName])
    VALUES ('PRESIDENT/OWNER');
    PRINT 'Added PRESIDENT/OWNER role';
END
ELSE
BEGIN
    PRINT 'PRESIDENT/OWNER role already exists';
END
GO

-- Add REGIONAL MANAGER role
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleName] = 'REGIONAL MANAGER')
BEGIN
    INSERT INTO [dbo].[Roles] ([RoleName])
    VALUES ('REGIONAL MANAGER');
    PRINT 'Added REGIONAL MANAGER role';
END
ELSE
BEGIN
    PRINT 'REGIONAL MANAGER role already exists';
END
GO

-- Display current roles for verification
PRINT 'Current Roles in System:';
SELECT RoleId, RoleName FROM [dbo].[Roles] ORDER BY RoleId;
GO

-- =============================================
-- Step 2: Verify GM Recap Tables Structure
-- =============================================

-- Check if GMWeeklyRecapEntry table exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GMWeeklyRecapEntry]') AND type in (N'U'))
BEGIN
    PRINT 'WARNING: GMWeeklyRecapEntry table does not exist. Creating it now...';

    CREATE TABLE [dbo].[GMWeeklyRecapEntry] (
        [RecapID] INT IDENTITY(1,1) NOT NULL,
        [GMUserID] INT NOT NULL,
        [LocationID] INT NOT NULL,
        [WeekStartDate] DATE NOT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] NVARCHAR(50) NULL,
        [DeletedDate] DATETIME NULL,
        [IsDraft] BIT NOT NULL DEFAULT 0,
        [SubmittedDate] DATETIME NULL,
        CONSTRAINT [PK_GMWeeklyRecapEntry] PRIMARY KEY CLUSTERED ([RecapID] ASC),
        CONSTRAINT [FK_GMWeeklyRecapEntry_Users] FOREIGN KEY ([GMUserID])
            REFERENCES [dbo].[Users] ([Users_ID])
    );

    CREATE NONCLUSTERED INDEX [IX_GMWeeklyRecapEntry_GMUserID] ON [dbo].[GMWeeklyRecapEntry]
    (
        [GMUserID] ASC
    );

    CREATE NONCLUSTERED INDEX [IX_GMWeeklyRecapEntry_WeekStartDate] ON [dbo].[GMWeeklyRecapEntry]
    (
        [WeekStartDate] DESC
    );

    PRINT 'GMWeeklyRecapEntry table created successfully';
END
ELSE
BEGIN
    PRINT 'GMWeeklyRecapEntry table already exists';

    -- Check if SubmittedDate column exists, add if not
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GMWeeklyRecapEntry]') AND name = 'SubmittedDate')
    BEGIN
        ALTER TABLE [dbo].[GMWeeklyRecapEntry] ADD [SubmittedDate] DATETIME NULL;
        PRINT 'Added SubmittedDate column to GMWeeklyRecapEntry';
    END

    -- Check if IsDraft column exists, add if not
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GMWeeklyRecapEntry]') AND name = 'IsDraft')
    BEGIN
        ALTER TABLE [dbo].[GMWeeklyRecapEntry] ADD [IsDraft] BIT NOT NULL DEFAULT 0;
        PRINT 'Added IsDraft column to GMWeeklyRecapEntry';
    END

    -- Check if ModifiedBy column exists, add if not
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GMWeeklyRecapEntry]') AND name = 'ModifiedBy')
    BEGIN
        ALTER TABLE [dbo].[GMWeeklyRecapEntry] ADD [ModifiedBy] NVARCHAR(50) NULL;
        PRINT 'Added ModifiedBy column to GMWeeklyRecapEntry';
    END
END
GO

-- Check if GMWeeklyRecapField table exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GMWeeklyRecapField]') AND type in (N'U'))
BEGIN
    PRINT 'WARNING: GMWeeklyRecapField table does not exist. Creating it now...';

    CREATE TABLE [dbo].[GMWeeklyRecapField] (
        [FieldID] INT IDENTITY(1,1) NOT NULL,
        [RecapID] INT NOT NULL,
        [FieldName] NVARCHAR(100) NOT NULL,
        [FieldValue] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [ModifiedDate] DATETIME NULL,
        CONSTRAINT [PK_GMWeeklyRecapField] PRIMARY KEY CLUSTERED ([FieldID] ASC),
        CONSTRAINT [FK_GMWeeklyRecapField_RecapEntry] FOREIGN KEY ([RecapID])
            REFERENCES [dbo].[GMWeeklyRecapEntry] ([RecapID])
            ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_GMWeeklyRecapField_RecapID] ON [dbo].[GMWeeklyRecapField]
    (
        [RecapID] ASC
    );

    PRINT 'GMWeeklyRecapField table created successfully';
END
ELSE
BEGIN
    PRINT 'GMWeeklyRecapField table already exists';

    -- Check if ModifiedDate column exists, add if not
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GMWeeklyRecapField]') AND name = 'ModifiedDate')
    BEGIN
        ALTER TABLE [dbo].[GMWeeklyRecapField] ADD [ModifiedDate] DATETIME NULL;
        PRINT 'Added ModifiedDate column to GMWeeklyRecapField';
    END
END
GO

-- =============================================
-- Step 3: Grant Feature Permissions for New Roles
-- =============================================

-- Ensure new roles have access to GM Dashboard and GM Recap features
DECLARE @PresidentRoleId INT, @RegionalRoleId INT;
DECLARE @GMDashboardFeatureId INT, @GMRecapFeatureId INT;

-- Get the RoleIds for new roles
SELECT @PresidentRoleId = RoleId FROM [dbo].[Roles] WHERE RoleName = 'PRESIDENT/OWNER';
SELECT @RegionalRoleId = RoleId FROM [dbo].[Roles] WHERE RoleName = 'REGIONAL MANAGER';

-- Get Feature IDs
SELECT @GMDashboardFeatureId = FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'GMDashboard';
SELECT @GMRecapFeatureId = FeatureID FROM [dbo].[Features] WHERE FeatureCode = 'GMRecap';

IF @PresidentRoleId IS NOT NULL AND @GMDashboardFeatureId IS NOT NULL
BEGIN
    PRINT 'President/Owner and Regional Manager roles will have feature access managed via the Access Control UI';
    PRINT 'Admin should grant GMDashboard and GMRecap features to these roles as needed';
END
ELSE
BEGIN
    PRINT 'Note: Feature permissions should be configured via the UI for President/Owner and Regional Manager roles';
END
GO

-- =============================================
-- Step 4: Create Helper View for Role-Based Recap Access
-- =============================================

-- Create or alter a view that helps with role-based filtering
IF OBJECT_ID('[dbo].[vw_GMRecapWithRoleInfo]', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_GMRecapWithRoleInfo];
GO

CREATE VIEW [dbo].[vw_GMRecapWithRoleInfo]
AS
SELECT
    E.RecapID,
    E.WeekStartDate,
    E.GMUserID,
    E.LocationID,
    E.CreatedDate,
    E.ModifiedDate,
    E.IsDraft,
    E.SubmittedDate,
    U.FirstName + ' ' + U.LastName AS GMName,
    U.RoleId AS GMRoleId,
    R.RoleName AS GMRoleName
FROM [dbo].[GMWeeklyRecapEntry] E
INNER JOIN [dbo].[Users] U ON E.GMUserID = U.Users_ID
INNER JOIN [dbo].[Roles] R ON U.RoleId = R.RoleId
WHERE E.DeletedDate IS NULL;  -- Only show non-deleted entries
GO

PRINT 'Created vw_GMRecapWithRoleInfo view for role-based filtering';
GO

-- =============================================
-- Step 5: Display Role Hierarchy Reference
-- =============================================

PRINT '';
PRINT '========================================';
PRINT 'UPDATED ROLE HIERARCHY';
PRINT '========================================';
PRINT '1. Dev Admin (ADMIN)';
PRINT '2. President/Owner (PRESIDENT/OWNER) - NEW';
PRINT '3. Regional Manager (REGIONAL MANAGER) - NEW';
PRINT '4. General Manager (GENERAL MANAGER)';
PRINT '5. Sales Manager (SALES ADMIN)';
PRINT '6. Sales (SALES / SALESPERSON)';
PRINT '7. Office (OFFICE / OFFICE MANAGER)';
PRINT '';
PRINT 'VISIBILITY RULES FOR GM RECAP:';
PRINT '- Dev Admin: See ALL entries';
PRINT '- President/Owner: See ALL entries';
PRINT '- Regional Manager: See GM, Sales Manager, Sales, Office entries (NOT President/Owner)';
PRINT '- General Manager: See GM, Sales Manager, Sales, Office entries (NOT Regional/President)';
PRINT '- Sales Manager: See only own entries';
PRINT '- Sales: See only own entries';
PRINT '- Office: See only own entries';
PRINT '========================================';
PRINT '';

-- =============================================
-- Step 6: Verification Queries
-- =============================================

PRINT 'Verification: All Roles in System';
SELECT RoleId, RoleName FROM [dbo].[Roles] ORDER BY RoleId;

PRINT '';
PRINT 'Verification: Sample GM Recap Entries with Role Info (if any exist)';
IF EXISTS (SELECT 1 FROM [dbo].[GMWeeklyRecapEntry])
BEGIN
    SELECT TOP 5
        RecapID,
        GMName,
        GMRoleName,
        WeekStartDate,
        CreatedDate
    FROM [dbo].[vw_GMRecapWithRoleInfo]
    ORDER BY CreatedDate DESC;
END
ELSE
BEGIN
    PRINT 'No GM Recap entries exist yet';
END

PRINT '';
PRINT 'Expand User Roles Hierarchy Migration completed successfully!';
GO
