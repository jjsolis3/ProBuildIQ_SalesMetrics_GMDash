using System.Security.Claims;

namespace SalesMetrics.Services.Helpers
{
    public static class RoleHelper
    {
        // Role ID Constants (matching database)
        public const int ROLE_ADMIN = 1;              // Dev Admin
        public const int ROLE_SALES = 2;              // Sales / Salesperson
        public const int ROLE_SALESPERSON = 2;        // Alias for SALES
        public const int ROLE_SALES_ADMIN = 3;        // Sales Manager
        public const int ROLE_GENERAL_MANAGER = 4;    // General Manager
        public const int ROLE_OFFICE_MANAGER = 5;     // Office Manager
        public const int ROLE_OFFICE = 6;             // Office
        public const int ROLE_PRESIDENT_OWNER = 7;    // President/Owner (new)
        public const int ROLE_REGIONAL_MANAGER = 8;   // Regional Manager (new)

        // Role Name Constants
        public const string ROLE_NAME_ADMIN = "ADMIN";
        public const string ROLE_NAME_PRESIDENT_OWNER = "PRESIDENT/OWNER";
        public const string ROLE_NAME_REGIONAL_MANAGER = "REGIONAL MANAGER";
        public const string ROLE_NAME_GENERAL_MANAGER = "GENERAL MANAGER";
        public const string ROLE_NAME_SALES_ADMIN = "SALES ADMIN";
        public const string ROLE_NAME_SALES = "SALES";
        public const string ROLE_NAME_OFFICE_MANAGER = "OFFICE MANAGER";
        public const string ROLE_NAME_OFFICE = "OFFICE";

        /// <summary>
        /// Determines if the user can switch locations
        /// </summary>
        public static bool CanSwitchLocation(ClaimsPrincipal user)
        {
            var roleId = user.FindFirst("RoleId")?.Value;

            // Admin, President/Owner, Regional Manager, and General Manager can switch locations
            return roleId == "1" ||  // Admin
                   roleId == "7" ||  // President/Owner
                   roleId == "8" ||  // Regional Manager
                   roleId == "4" ||  // General Manager
                   user.IsInRole("Admin") ||
                   user.IsInRole("President/Owner") ||
                   user.IsInRole("Regional Manager") ||
                   user.IsInRole("General Manager");
        }

        /// <summary>
        /// Gets the role hierarchy level (lower number = higher authority)
        /// </summary>
        public static int GetRoleHierarchyLevel(int roleId)
        {
            return roleId switch
            {
                ROLE_ADMIN => 1,              // Dev Admin - highest
                ROLE_PRESIDENT_OWNER => 2,    // President/Owner
                ROLE_REGIONAL_MANAGER => 3,   // Regional Manager
                ROLE_GENERAL_MANAGER => 4,    // General Manager
                ROLE_SALES_ADMIN => 5,        // Sales Manager
                ROLE_SALES => 6,              // Sales / Salesperson (both use RoleId=2)
                ROLE_OFFICE_MANAGER => 6,     // Office Manager
                ROLE_OFFICE => 7,             // Office
                _ => 99                       // Unknown role - lowest priority
            };
        }

        /// <summary>
        /// Checks if the current user can view GM Recap entries from a specific role
        /// Based on visibility hierarchy:
        /// - Dev Admin & President/Owner: See ALL entries
        /// - Regional Manager: See GM, Sales Manager, Sales, Office entries (NOT President/Owner)
        /// - General Manager: See GM, Sales Manager, Sales, Office entries (NOT Regional/President)
        /// - Others: See only own entries
        /// </summary>
        public static bool CanViewRecapFromRole(int currentUserRoleId, int targetEntryRoleId)
        {
            int currentLevel = GetRoleHierarchyLevel(currentUserRoleId);
            int targetLevel = GetRoleHierarchyLevel(targetEntryRoleId);

            // Dev Admin and President/Owner can see everything
            if (currentUserRoleId == ROLE_ADMIN || currentUserRoleId == ROLE_PRESIDENT_OWNER)
            {
                return true;
            }

            // Regional Manager can see GM and below, but NOT President/Owner
            if (currentUserRoleId == ROLE_REGIONAL_MANAGER)
            {
                return targetEntryRoleId != ROLE_PRESIDENT_OWNER && targetLevel >= currentLevel;
            }

            // General Manager can see GM level and below, but NOT Regional or President/Owner
            if (currentUserRoleId == ROLE_GENERAL_MANAGER)
            {
                return targetEntryRoleId != ROLE_PRESIDENT_OWNER &&
                       targetEntryRoleId != ROLE_REGIONAL_MANAGER &&
                       targetLevel >= currentLevel;
            }

            // Others can only see their own role level
            return currentLevel == targetLevel;
        }

        /// <summary>
        /// Checks if a role is a management role (has oversight capabilities)
        /// </summary>
        public static bool IsManagementRole(int roleId)
        {
            return roleId == ROLE_ADMIN ||
                   roleId == ROLE_PRESIDENT_OWNER ||
                   roleId == ROLE_REGIONAL_MANAGER ||
                   roleId == ROLE_GENERAL_MANAGER ||
                   roleId == ROLE_SALES_ADMIN ||
                   roleId == ROLE_OFFICE_MANAGER;
        }

        /// <summary>
        /// Checks if the user is in an executive role (President/Owner or Admin)
        /// </summary>
        public static bool IsExecutiveRole(int roleId)
        {
            return roleId == ROLE_ADMIN || roleId == ROLE_PRESIDENT_OWNER;
        }
    }

}
