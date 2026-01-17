# Expanded User Roles & Hierarchy - Implementation Guide

**Date:** January 17, 2026
**Author:** Jose Solis
**Purpose:** Implement expanded role hierarchy and role-based visibility for GM Recap entries

---

## 📋 Overview

This update expands the SalesMetrics application's role system to better reflect organizational structure and implement proper visibility controls for GM Dashboard/Recap data.

### New Roles Added
- **President/Owner** (RoleId: 8) - Executive oversight with full visibility
- **Regional Manager** (RoleId: 9) - Multi-location oversight with limited visibility

---

## 🎯 Role Hierarchy

The updated role hierarchy (from highest to lowest authority):

1. **Dev Admin** (ADMIN) - Full system access
2. **President/Owner** (PRESIDENT/OWNER) - Executive access, sees all GM recaps
3. **Regional Manager** (REGIONAL MANAGER) - Oversees GMs, limited executive visibility
4. **General Manager** (GENERAL MANAGER) - Location management
5. **Sales Manager** (SALES ADMIN) - Sales team oversight
6. **Sales** (SALES / SALESPERSON) - Sales operations
7. **Office** (OFFICE / OFFICE MANAGER) - Administrative support

---

## 🔐 GM Recap Visibility Rules

### Who Can See What?

| Role | Can View Entries From |
|------|----------------------|
| **Dev Admin** | ALL entries (no restrictions) |
| **President/Owner** | ALL entries (full visibility) |
| **Regional Manager** | GM, Sales Manager, Sales, Office<br>❌ Cannot see President/Owner entries |
| **General Manager** | GM, Sales Manager, Sales, Office<br>❌ Cannot see Regional or President/Owner entries |
| **Sales Manager** | Only own entries |
| **Sales** | Only own entries |
| **Office** | Only own entries |

### Visibility Logic

The visibility is determined by the **role of the person who created the entry**, not the location:

```
Example:
- If a President/Owner creates an entry, only Dev Admin and other President/Owners can see it
- If a GM creates an entry, Regional Managers, President/Owners, Dev Admin, and other GMs can see it
- If a Sales person creates an entry, only they and users with higher authority can see it
```

---

## 📦 Files Changed

### 1. Database Migration
**File:** `Migrations/ExpandUserRolesHierarchy.sql`

**What it does:**
- Adds `PRESIDENT/OWNER` and `REGIONAL MANAGER` roles to the `Roles` table
- Creates/updates `GMWeeklyRecapEntry` and `GMWeeklyRecapField` tables if needed
- Creates `vw_GMRecapWithRoleInfo` view for efficient role-based filtering
- Adds necessary indexes and constraints

**To apply:**
```sql
-- Execute in SQL Server Management Studio or Azure Data Studio
USE SalesMetrics;
GO
-- Run the migration script
```

### 2. RoleHelper Service
**File:** `Services/Helpers/RoleHelper.cs`

**Changes:**
- Added role ID and name constants for all roles
- Added `GetRoleHierarchyLevel()` method to determine authority level
- Added `CanViewRecapFromRole()` method to check visibility permissions
- Updated `CanSwitchLocation()` to include new roles
- Added helper methods `IsManagementRole()` and `IsExecutiveRole()`

**Key Methods:**
```csharp
// Check if user can view a specific recap entry
bool canView = RoleHelper.CanViewRecapFromRole(currentUserRoleId, entryCreatorRoleId);

// Check if user can switch locations
bool canSwitch = RoleHelper.CanSwitchLocation(user);

// Get role hierarchy level (1 = highest, 7+ = lowest)
int level = RoleHelper.GetRoleHierarchyLevel(roleId);
```

### 3. AuthController
**File:** `Controllers/AuthController.cs`

**Changes:**
- Updated `RedirectToAppropriatePage()` to redirect new roles to appropriate dashboards
- President/Owner and Regional Manager redirect to GM Dashboard

### 4. GMRecapController
**File:** `Controllers/GMRecapController.cs`

**Changes:**
- Added `using SalesMetrics.Services.Helpers;` for RoleHelper access
- Modified `RecapList()` action to implement role-based filtering
- Uses new `vw_GMRecapWithRoleInfo` view to efficiently query with role information
- Implements per-role caching to prevent cross-role data leaks
- Applies `RoleHelper.CanViewRecapFromRole()` to filter visible entries

**Key Implementation:**
```csharp
// Get current user's role
int currentUserRoleId = GetCurrentUserRoleId();

// Query with role information
SELECT RecapID, WeekStartDate, GMUserID, LocationID, GMName, GMRoleId, GMRoleName, CreatedDate
FROM vw_GMRecapWithRoleInfo
ORDER BY WeekStartDate DESC

// Filter based on visibility rules
if (RoleHelper.CanViewRecapFromRole(currentUserRoleId, entryRoleId))
{
    // Add to visible list
}
```

---

## 🚀 Deployment Steps

### Step 1: Apply Database Migration
1. Connect to your SQL Server database
2. Execute `Migrations/ExpandUserRolesHierarchy.sql`
3. Verify roles were added:
   ```sql
   SELECT RoleId, RoleName FROM Roles ORDER BY RoleId;
   ```

### Step 2: Deploy Application Code
1. Ensure all changed files are deployed:
   - `Services/Helpers/RoleHelper.cs`
   - `Controllers/AuthController.cs`
   - `Controllers/GMRecapController.cs`
2. Build the application
3. Deploy to your environment

### Step 3: Configure User Permissions
1. Log in as Admin
2. Navigate to Settings → User Management
3. For each user that should have new roles:
   - Edit user
   - Change `RoleId` to appropriate value:
     - 8 = President/Owner
     - 9 = Regional Manager
   - Grant `GMDashboard` and `GMRecap` feature permissions

### Step 4: Test Visibility Rules
Test with different role accounts to verify:
- [ ] President/Owner can see ALL entries
- [ ] Regional Manager can see GM entries but NOT President/Owner entries
- [ ] GM can see other GM entries but NOT Regional/President entries
- [ ] Sales/Office can only see their own entries

---

## 🔧 Configuration

### Assigning Roles to Users

**Option 1: Via UI (Recommended)**
1. Login as Admin
2. Go to Settings → Users
3. Edit user
4. Change role dropdown to new role
5. Save

**Option 2: Via SQL**
```sql
-- Assign President/Owner role
UPDATE Users
SET RoleId = 8
WHERE Users_ID = <user_id>;

-- Assign Regional Manager role
UPDATE Users
SET RoleId = 9
WHERE Users_ID = <user_id>;
```

### Granting Feature Access

All roles need feature permissions granted via the Access Control UI:

1. Admin logs in
2. Navigate to User Management
3. Edit user
4. Check boxes for:
   - ✅ GMDashboard
   - ✅ GMRecap
   - ✅ Other features as appropriate
5. Save

---

## 🧪 Testing Checklist

### Role Assignment Tests
- [ ] Can assign President/Owner role to a user
- [ ] Can assign Regional Manager role to a user
- [ ] Users with new roles can log in successfully
- [ ] Users redirect to correct dashboard on login

### Location Switching Tests
- [ ] President/Owner can switch locations
- [ ] Regional Manager can switch locations
- [ ] GM can switch locations (existing functionality)
- [ ] Sales/Office cannot switch locations

### GM Recap Visibility Tests

**Test Setup:** Create recap entries from users with different roles:
- Entry 1: Created by President/Owner
- Entry 2: Created by Regional Manager
- Entry 3: Created by GM
- Entry 4: Created by Sales

**Test Cases:**

| Logged In As | Should See Entries |
|-------------|-------------------|
| Dev Admin | 1, 2, 3, 4 (ALL) |
| President/Owner | 1, 2, 3, 4 (ALL) |
| Regional Manager | 2, 3, 4 (NOT 1) |
| General Manager | 3, 4 (NOT 1, 2) |
| Sales | 4 (only own) |

### Feature Access Tests
- [ ] President/Owner with GMRecap permission can access recap entry form
- [ ] Regional Manager with GMRecap permission can access recap list
- [ ] Users without GMRecap permission cannot access GM Recap features
- [ ] Feature permissions work correctly for all new roles

---

## 📊 Database Schema Reference

### Roles Table
```sql
CREATE TABLE [dbo].[Roles] (
    [RoleId] INT IDENTITY(1,1) NOT NULL,
    [RoleName] NVARCHAR(100) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
);
```

**Current Roles:**
| RoleId | RoleName |
|--------|----------|
| 1 | ADMIN |
| 2 | SALESPERSON |
| 3 | SALES ADMIN |
| 4 | GENERAL MANAGER |
| 5 | SALES |
| 6 | OFFICE MANAGER |
| 7 | OFFICE |
| 8 | PRESIDENT/OWNER ⭐ NEW |
| 9 | REGIONAL MANAGER ⭐ NEW |

### vw_GMRecapWithRoleInfo View
```sql
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
WHERE E.DeletedDate IS NULL;
```

---

## 🐛 Troubleshooting

### Issue: New roles don't appear in dropdown
**Solution:** Ensure migration was applied successfully and roles exist in database:
```sql
SELECT * FROM Roles WHERE RoleName IN ('PRESIDENT/OWNER', 'REGIONAL MANAGER');
```

### Issue: User can't see expected recap entries
**Possible Causes:**
1. User doesn't have `GMRecap` feature permission
   - **Fix:** Grant feature via Access Control UI
2. Recap entries don't have proper role information
   - **Fix:** Verify `vw_GMRecapWithRoleInfo` view returns role data
3. Cache showing old data
   - **Fix:** Wait 5 minutes or restart application to clear cache

### Issue: User can't switch locations
**Solution:** Verify user has appropriate role (Admin, President/Owner, Regional Manager, or GM)

### Issue: View `vw_GMRecapWithRoleInfo` doesn't exist
**Solution:** Run the migration script which creates the view

---

## 🔄 Rollback Plan

If issues occur, you can rollback:

### Database Rollback
```sql
-- Remove new roles (only if no users are assigned)
DELETE FROM Roles WHERE RoleName IN ('PRESIDENT/OWNER', 'REGIONAL MANAGER');

-- Drop the view
DROP VIEW IF EXISTS [dbo].[vw_GMRecapWithRoleInfo];
```

### Code Rollback
1. Revert changed files from git
2. Redeploy previous version

**⚠️ Warning:** Only rollback database if no users have been assigned to new roles!

---

## 📞 Support

For questions or issues:
- Contact: Jose Solis
- Date Implemented: January 17, 2026
- Related Tasks: Expand User Roles & Permissions for GM Dashboard

---

## 📝 Future Enhancements

Potential improvements for consideration:

1. **Location-Based Filtering:** Add ability for Regional Managers to only see entries from specific locations
2. **Delegation:** Allow managers to temporarily delegate visibility permissions
3. **Audit Trail:** Track who viewed which recap entries
4. **Email Notifications:** Notify managers when subordinates submit recaps
5. **Reporting:** Generate role-based reports on recap completion rates

---

## ✅ Summary

This implementation successfully:
- ✅ Adds President/Owner and Regional Manager roles
- ✅ Implements hierarchical visibility for GM Recap entries
- ✅ Updates location switching permissions for new roles
- ✅ Maintains existing feature-based access control
- ✅ Preserves backwards compatibility with existing roles
- ✅ Implements efficient database views for performance
- ✅ Adds comprehensive helper methods for role management

The system now properly reflects the organizational hierarchy while maintaining security and performance.
