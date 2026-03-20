using System.Collections.Generic;
using System.Threading.Tasks;
using SalesMetrics.Models.EFCore;

namespace SalesMetrics.Services.Permissions
{
    /// <summary>
    /// Service for managing and checking user feature permissions
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// Check if a user has access to a specific feature
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="featureCode">Feature code (e.g., "Reports", "Dashboard")</param>
        /// <returns>True if user has access, false otherwise</returns>
        Task<bool> HasFeatureAccessAsync(int userId, string featureCode);

        /// <summary>
        /// Get all features available in the system
        /// </summary>
        /// <returns>List of all features</returns>
        Task<List<FeatureEntity>> GetAllFeaturesAsync();

        /// <summary>
        /// Get all features a user has access to
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of features the user can access</returns>
        Task<List<FeatureEntity>> GetUserFeaturesAsync(int userId);

        /// <summary>
        /// Grant a user access to a feature
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="featureId">Feature ID</param>
        /// <param name="grantedByUserId">User ID who is granting the permission</param>
        /// <returns>True if successful</returns>
        Task<bool> GrantFeatureAccessAsync(int userId, int featureId, int grantedByUserId);

        /// <summary>
        /// Revoke a user's access to a feature
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="featureId">Feature ID</param>
        /// <returns>True if successful</returns>
        Task<bool> RevokeFeatureAccessAsync(int userId, int featureId);

        /// <summary>
        /// Update user's feature permissions in batch
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="featureIds">List of feature IDs to grant access to</param>
        /// <param name="grantedByUserId">User ID who is updating the permissions</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateUserPermissionsAsync(int userId, List<int> featureIds, int grantedByUserId);

        /// <summary>
        /// Get permission IDs for a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of feature IDs the user has access to</returns>
        Task<List<int>> GetUserPermissionIdsAsync(int userId);

        /// <summary>
        /// Get all user IDs that currently have access to a specific feature
        /// </summary>
        Task<List<int>> GetUsersWithFeatureAccessAsync(int featureId);

        /// <summary>
        /// Bulk set which users have access to a feature.
        /// Users in userIdsWithAccess are granted; all others have access revoked.
        /// </summary>
        Task<bool> BulkUpdateFeatureAccessAsync(int featureId, List<int> userIdsWithAccess, int grantedByUserId);

        /// <summary>
        /// Bulk set feature access limited to a specific scope of users.
        /// Only users in scopedUserIds are affected; users outside the scope are left unchanged.
        /// </summary>
        Task<bool> BulkUpdateFeatureAccessForScopedUsersAsync(int featureId, List<int> scopedUserIds, List<int> userIdsWithAccess, int grantedByUserId);

        /// <summary>
        /// Get all user IDs that currently have a specific location assigned.
        /// </summary>
        Task<List<int>> GetUsersWithLocationAssignmentAsync(int locationId);

        /// <summary>
        /// Bulk update location assignments for a scoped set of users.
        /// Only users in <paramref name="scopedUserIds"/> are affected.
        /// Users in <paramref name="userIdsToAssign"/> receive the location;
        /// scoped users NOT in that list have the location removed.
        /// </summary>
        Task<bool> BulkUpdateLocationAssignmentsAsync(int locationId, List<int> scopedUserIds, List<int> userIdsToAssign, int adminUserId);
    }
}
