# Feature Permissions System Update

**Date:** 2026-01-13
**Purpose:** Reorganize Feature Permissions to match actual application sidebar structure and improve granularity

---

## Overview

This update reorganizes the Feature Permissions system to align with the actual application sidebar structure, providing more granular control over user access. The changes address the user's request to make the Feature Permissions modal match the real application features shown in the sidebar.

---

## Changes Made

### 1. SQL Migration: `UpdateFeaturePermissionsToMatchSidebar.sql`

#### Features Added:
- **GMDashboard** - Access to General Manager dashboard and analytics
- **GMRecap** - Access to enter and view weekly recap reports
- **SalesPulse** - Access to sales representative metrics and performance tracking
- **Journal** - Access to personal journal and timeline
- **Management** - Access to management company information
- **Properties** - Access to property information and management
- **ErrorLogs** - Access to error logs and diagnostics
- **Envelopes** - Access to send and review DocuSign envelopes
- **EnvelopeTemplates** - Access to create and manage DocuSign templates
- **FormRequests** - Access to submit and view form requests (e.g., New Customer Form)
- **YardiUpload** - Access to upload Yardi data files

#### Features Updated:
- **Dashboard** → "Sales Dashboard" - Clarified naming
- **Orders** → "Work Orders / Job Schedule" - Better description
- **Tasks** → "Tasks & Calendar" - Includes calendar functionality
- **Users** → "Access Controls & User Management" - Clearer naming
- **Settings** → "System Settings" - More specific
- **Reports** → "Reports Catalog" - Clarified purpose
- **Yardi** → "Yardi Properties" - More specific, moved to Prospecting category
- **Signing** → "Document Signing & Envelopes" - Broader description

#### Features Disabled:
- **Accounts** - Deprecated (no longer used in sidebar)
- **Notifications** - Handled differently (not feature-gated)

#### Default Permissions Granted:
| Feature | Granted To |
|---------|-----------|
| GMDashboard, GMRecap | Admin, General Manager |
| SalesPulse | Admin, General Manager, Sales Manager |
| Journal, Management, Properties | All active users |
| ErrorLogs | Admin only |
| Envelopes | Users with Signing access |
| EnvelopeTemplates | Admin only |
| FormRequests | Admin, General Manager |
| YardiUpload | Admin only |

### 2. Sidebar Updates (`Views/Shared/sidebar.cshtml`)

**Before:** Sidebar sections were primarily controlled by role-based checks (isGMOrAdmin, roleId checks)

**After:** All sidebar sections now use granular feature permissions:

```csharp
// Added permission checks for:
- hasGMDashboardAccess
- hasGMRecapAccess
- hasSalesPulseAccess
- hasJournalAccess
- hasManagementAccess
- hasPropertiesAccess
- hasYardiUploadAccess
- hasErrorLogsAccess
```

**Benefits:**
- Finer control over individual features
- Can grant GM Dashboard without granting GM Recap, for example
- Can grant specific users access to Sales Pulse without making them a manager
- Easier to customize access for individual users

### 3. Topbar Updates (`Views/Shared/topbar.cshtml`)

#### Form Requests Moved:
- **Before:** Located in "Quick Links" dropdown with role-based check (`hasUsersAccess`)
- **After:** Moved to User Menu dropdown with dedicated permission check (`hasFormRequestsAccess`)

#### New Granular Signing Permissions:
- **Before:** Single `hasSigningAccess` check for all signing features
- **After:**
  - `hasEnvelopesAccess` - Controls access to view and send envelopes
  - `hasEnvelopeTemplatesAccess` - Controls access to manage templates

**User Experience Improvement:**
- Form Requests now accessible from the user profile menu (more intuitive location)
- Removed from Quick Links to reduce clutter
- Can grant envelope sending without template management
- Can restrict template management to admins only

---

## How to Apply the Migration

### Option 1: SQL Server Management Studio (SSMS)
1. Open SSMS and connect to your database
2. Open the file: `Migrations/UpdateFeaturePermissionsToMatchSidebar.sql`
3. Execute the script against your database
4. Review the output messages to confirm all features were added/updated
5. Check the verification queries at the end of the script

### Option 2: Command Line (sqlcmd)
```bash
sqlcmd -S your_server_name -d SalesMetrics -i Migrations/UpdateFeaturePermissionsToMatchSidebar.sql
```

### Option 3: Entity Framework Migration (Future)
This could be converted to an EF Core migration if needed.

---

## Verification

After running the migration, verify the changes:

### 1. Check Features Table:
```sql
SELECT FeatureID, FeatureCode, FeatureName, Category, DisplayOrder, IsActive
FROM Features
WHERE IsActive = 1
ORDER BY Category, DisplayOrder;
```

You should see approximately 20+ active features organized by category.

### 2. Check User Permissions:
```sql
SELECT
    u.FirstName + ' ' + u.LastName AS UserName,
    r.RoleName,
    COUNT(ufp.PermissionID) AS PermissionCount
FROM Users u
LEFT JOIN Roles r ON u.RoleID = r.RoleID
LEFT JOIN UserFeaturePermissions ufp ON u.Users_ID = ufp.Users_ID
WHERE u.IsActive = 1
GROUP BY u.FirstName, u.LastName, r.RoleName
ORDER BY r.RoleName;
```

Expected permission counts:
- **Admin:** 18-20 permissions
- **General Manager:** 15-17 permissions
- **Sales Manager:** 10-12 permissions
- **Sales Rep:** 8-10 permissions

### 3. Test UI Access:
1. Log in as different user types (Admin, GM, Sales Manager, Sales Rep)
2. Verify sidebar shows correct sections based on permissions
3. Verify Form Requests appears in User Menu dropdown (not Quick Links)
4. Verify Documents & Signing section shows correct options

---

## Feature Permission Structure

The new structure aligns with the sidebar hierarchy:

```
GENERAL MGR (Category: GM)
├── GMDashboard - GM Dashboard
└── GMRecap - GM Weekly Recap Entry & View

DASHBOARD (Category: Core)
└── Dashboard - Sales Dashboard

TASKS (Category: Core)
└── Tasks - Tasks & Calendar

SALES REPS (Category: Sales)
└── SalesPulse - Sales Pulse

JOURNAL (Category: Core)
└── Journal - My Journal

JOB SCHEDULE (Category: Core)
└── Orders - Work Orders / Job Schedule

MANAGEMENT (Category: Core)
└── Management - Management Companies

PROPERTIES (Category: Core)
└── Properties - Properties

PROSPECTING (Category: Prospecting)
└── Yardi - Yardi Properties

REPORTS (Category: Reports)
├── Reports - Reports Catalog
├── ReportMarginCommission - Margin Commission Report
└── ReportSalesMetrics - Sales Metrics Report

COMMUNICATION (Category: Communication)
└── Announcements - Announcements

ADMIN / SETTINGS (Category: Admin)
├── Settings - System Settings
├── Users - Access Controls & User Management
└── YardiUpload - Yardi Upload

WEB APP ERRORS (Category: Admin)
└── ErrorLogs - Web App Errors

INTEGRATION (Category: Integration)
├── Signing - Document Signing & Envelopes (legacy)
├── Envelopes - Send & Review Envelopes
└── EnvelopeTemplates - Create & Manage Envelope Templates

USER MENU
└── FormRequests - Form Requests (New Customer Form, Submissions)
```

---

## Managing User Permissions

### Grant Permission to Specific User:
```sql
-- Example: Grant Form Requests access to a specific sales rep
DECLARE @UserId INT = (SELECT Users_ID FROM Users WHERE UserID = 'SALESREP001');
DECLARE @FeatureId INT = (SELECT FeatureID FROM Features WHERE FeatureCode = 'FormRequests');

INSERT INTO UserFeaturePermissions (Users_ID, FeatureID, HasAccess, GrantedDate, GrantedByUsers_ID)
VALUES (@UserId, @FeatureId, 1, GETDATE(), 1); -- Granted by Admin (Users_ID = 1)
```

### Revoke Permission from User:
```sql
-- Example: Revoke Error Logs access from a user
DELETE FROM UserFeaturePermissions
WHERE Users_ID = @UserId
  AND FeatureID = (SELECT FeatureID FROM Features WHERE FeatureCode = 'ErrorLogs');
```

### Bulk Grant to Role:
```sql
-- Example: Grant all Sales Managers access to Management feature
INSERT INTO UserFeaturePermissions (Users_ID, FeatureID, HasAccess, GrantedDate)
SELECT
    u.Users_ID,
    (SELECT FeatureID FROM Features WHERE FeatureCode = 'Management'),
    1,
    GETDATE()
FROM Users u
WHERE u.RoleID = 3 -- Sales Manager
  AND u.IsActive = 1
  AND NOT EXISTS (
      SELECT 1 FROM UserFeaturePermissions ufp
      WHERE ufp.Users_ID = u.Users_ID
        AND ufp.FeatureID = (SELECT FeatureID FROM Features WHERE FeatureCode = 'Management')
  );
```

---

## General Settings Tab - Recommendations

The user asked: "What is the 'General Setting' for? What should be added here?"

### Current State:
The "General Settings" tab in System Settings (`/Settings/Index`) currently shows "General settings coming soon".

### Recommended Content for General Settings:

#### 1. **Application-Wide Settings**
- **Company Information**
  - Company Name
  - Logo Upload
  - Primary Color Theme
  - Contact Information

- **Default Behavior**
  - Default Landing Page (by role)
  - Session Timeout Duration
  - Date/Time Format Preferences
  - Number Format (currency, decimals)

#### 2. **Notification Preferences (Global Defaults)**
- Email notification frequency (immediate, daily digest, weekly)
- Push notification settings
- Announcement broadcast defaults

#### 3. **Feature Toggles**
- Enable/Disable specific features globally (e.g., turn off Yardi integration)
- Maintenance mode toggle
- Debug mode (for admins)

#### 4. **Integration Settings**
- DocuSign API Configuration
- Yardi Connection Settings
- ERP System Selection (CompUFloor, Kudu, SqlServer, HTTP)
- Email Server (SMTP) Configuration

#### 5. **Data Retention Policies**
- Journal entry retention period
- Task archive after X days
- Error log retention
- Report cache duration

#### 6. **Regional Settings**
- Default Time Zone
- Default Branch/Location
- Currency Settings
- Language Preferences (if multi-language support is added)

#### 7. **Performance Settings**
- Cache duration for dashboard data
- Report generation limits
- File upload size limits
- Concurrent user limits

#### 8. **Backup and Maintenance**
- Scheduled backup configuration
- Database maintenance schedules
- System health monitoring settings

### Implementation Priority:

**Phase 1 (Essential):**
1. Company Information (Name, Logo)
2. Session Timeout
3. Default Landing Page by Role

**Phase 2 (Important):**
1. Feature Toggles
2. Date/Time/Number Formats
3. Integration Settings (DocuSign, Yardi)

**Phase 3 (Nice to Have):**
1. Regional Settings
2. Performance Settings
3. Data Retention Policies

---

## Troubleshooting

### Issue: Sidebar sections not showing after migration
**Solution:** Clear browser cache and ensure migration was successful. Check that the user has the required permissions.

### Issue: "Feature not found" errors
**Solution:** Verify all features exist in the Features table with correct FeatureCodes. Run the verification query.

### Issue: Admin users losing access
**Solution:** Re-run the permission granting section of the migration for Admin users.

### Issue: Form Requests not appearing in User Menu
**Solution:**
1. Verify user has FormRequests permission
2. Clear browser cache
3. Check that topbar.cshtml was updated correctly

---

## Rollback Instructions

If you need to rollback these changes:

```sql
-- 1. Delete new features (this will cascade delete permissions)
DELETE FROM Features WHERE FeatureCode IN (
    'GMDashboard', 'GMRecap', 'SalesPulse', 'Journal',
    'Management', 'Properties', 'ErrorLogs', 'Envelopes',
    'EnvelopeTemplates', 'FormRequests', 'YardiUpload'
);

-- 2. Restore old feature names
UPDATE Features SET FeatureName = 'Dashboard' WHERE FeatureCode = 'Dashboard';
UPDATE Features SET FeatureName = 'Orders' WHERE FeatureCode = 'Orders';
-- ... (continue for other renamed features)

-- 3. Re-enable disabled features
UPDATE Features SET IsActive = 1 WHERE FeatureCode IN ('Accounts', 'Notifications');
```

**Note:** Rollback also requires reverting the view file changes manually.

---

## Future Enhancements

### 1. UI-Based Permission Management
Create an admin interface to manage feature permissions without SQL:
- Drag-and-drop permission assignment
- Role templates
- Bulk permission updates

### 2. Permission Groups
Create permission groups (e.g., "Basic Sales", "Advanced Sales", "Management") that can be assigned to users.

### 3. Time-Based Permissions
Implement permission expiration dates for temporary access grants.

### 4. Audit Trail
Log all permission changes for compliance and troubleshooting.

### 5. Permission Inheritance
Allow users to inherit permissions from their manager or team.

---

## Support

For questions or issues with this migration:
1. Check the verification queries in the migration script
2. Review error logs in `/ErrorLog/Index`
3. Contact system administrator

---

## Report Permissions Consolidation

**Additional Migration:** `ConsolidateReportPermissions.sql`

The initial migration included individual report permissions (Margin Commission Report, Sales Metrics Report), but this was simplified to a two-tier system:

### Two-Tier Report Access:

1. **Reports** (Base Permission)
   - Access to Reports section in sidebar
   - Can view and run existing reports
   - Can execute pre-built report queries
   - **Granted to:** Any user who needs to run reports

2. **ReportsCreate** (Advanced Permission)
   - Includes all base Reports permissions PLUS
   - Can create new report definitions
   - Can write custom SQL queries for reports
   - Can modify report templates
   - **Granted to:** Admin and General Manager only (by default)

### Benefits of This Approach:

✅ **Simple for most users** - Just grant "Reports" permission to run reports
✅ **No per-report permissions needed** - Don't need a permission for every report you create
✅ **Controlled report creation** - Only trusted users can create custom queries
✅ **Scalable** - Add 100 new reports without touching permissions
✅ **Maintainable** - Clear separation between viewing and creating

### Implementation in Code:

```csharp
// In Reports Controller/View
var hasReportsAccess = await _permissionService.HasFeatureAccessAsync(userId, "Reports");
var canCreateReports = await _permissionService.HasFeatureAccessAsync(userId, "ReportsCreate");

// Show Reports section if user has base access
if (!hasReportsAccess)
    return Forbid();

// Show "Create New Report" button only if user can create
if (canCreateReports)
{
    // Show create report UI
}
```

### Migration Instructions:

Run migrations in this order:
1. **First:** `UpdateFeaturePermissionsToMatchSidebar.sql` - Main feature reorganization
2. **Second:** `ConsolidateReportPermissions.sql` - Report permissions consolidation

The second migration will:
- Consolidate individual report permissions into single "Reports" permission
- Create new "ReportsCreate" permission
- Migrate existing users automatically
- Disable deprecated report permissions

## Summary

This update provides:
✅ Feature permissions matching actual sidebar structure
✅ Granular control over individual features
✅ Two-tier report access (View/Run vs Create)
✅ Improved user experience with Form Requests in User Menu
✅ Better organization by feature categories
✅ Easier permission management for administrators
✅ Foundation for future permission enhancements

The system is now more flexible, maintainable, and aligns with the actual application structure.
