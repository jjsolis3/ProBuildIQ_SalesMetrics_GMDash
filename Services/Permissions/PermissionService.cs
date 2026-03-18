using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Models.EFCore;

namespace SalesMetrics.Services.Permissions
{
    /// <summary>
    /// Implementation of permission service for managing user feature access
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly SalesMetricsDbContext _context;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(SalesMetricsDbContext context, ILogger<PermissionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> HasFeatureAccessAsync(int userId, string featureCode)
        {
            try
            {
                var feature = await _context.Features
                    .FirstOrDefaultAsync(f => f.FeatureCode == featureCode && f.IsActive);

                if (feature == null)
                {
                    _logger.LogWarning("Feature {FeatureCode} not found or inactive", featureCode);
                    return false;
                }

                var permission = await _context.UserFeaturePermissions
                    .FirstOrDefaultAsync(p => p.Users_ID == userId && p.FeatureId == feature.FeatureId);

                if (permission == null)
                {
                    return false;
                }

                // Check if permission has expired
                if (permission.ExpiresDate.HasValue && permission.ExpiresDate.Value < DateTime.Now)
                {
                    return false;
                }

                return permission.HasAccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking feature access for user {UserId} and feature {FeatureCode}", userId, featureCode);
                return false;
            }
        }

        public async Task<List<FeatureEntity>> GetAllFeaturesAsync()
        {
            try
            {
                return await _context.Features
                    .Where(f => f.IsActive)
                    .OrderBy(f => f.DisplayOrder)
                    .ThenBy(f => f.FeatureName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all features");
                return new List<FeatureEntity>();
            }
        }

        public async Task<List<FeatureEntity>> GetUserFeaturesAsync(int userId)
        {
            try
            {
                var now = DateTime.Now;

                return await _context.UserFeaturePermissions
                    .Include(p => p.Feature)
                    .Where(p => p.Users_ID == userId
                        && p.HasAccess
                        && p.Feature != null
                        && p.Feature.IsActive
                        && (!p.ExpiresDate.HasValue || p.ExpiresDate.Value > now))
                    .Select(p => p.Feature!)
                    .OrderBy(f => f.DisplayOrder)
                    .ThenBy(f => f.FeatureName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving features for user {UserId}", userId);
                return new List<FeatureEntity>();
            }
        }

        public async Task<bool> GrantFeatureAccessAsync(int userId, int featureId, int grantedByUserId)
        {
            try
            {
                var existingPermission = await _context.UserFeaturePermissions
                    .FirstOrDefaultAsync(p => p.Users_ID == userId && p.FeatureId == featureId);

                if (existingPermission != null)
                {
                    // Update existing permission
                    existingPermission.HasAccess = true;
                    existingPermission.GrantedDate = DateTime.Now;
                    existingPermission.GrantedByUsers_ID = grantedByUserId;
                    existingPermission.ExpiresDate = null;
                }
                else
                {
                    // Create new permission
                    var newPermission = new UserFeaturePermissionEntity
                    {
                        Users_ID = userId,
                        FeatureId = featureId,
                        HasAccess = true,
                        GrantedDate = DateTime.Now,
                        GrantedByUsers_ID = grantedByUserId
                    };
                    _context.UserFeaturePermissions.Add(newPermission);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error granting feature access for user {UserId} and feature {FeatureId}", userId, featureId);
                return false;
            }
        }

        public async Task<bool> RevokeFeatureAccessAsync(int userId, int featureId)
        {
            try
            {
                var permission = await _context.UserFeaturePermissions
                    .FirstOrDefaultAsync(p => p.Users_ID == userId && p.FeatureId == featureId);

                if (permission != null)
                {
                    _context.UserFeaturePermissions.Remove(permission);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking feature access for user {UserId} and feature {FeatureId}", userId, featureId);
                return false;
            }
        }

        public async Task<bool> UpdateUserPermissionsAsync(int userId, List<int> featureIds, int grantedByUserId)
        {
            try
            {
                // Get all existing permissions for this user
                var existingPermissions = await _context.UserFeaturePermissions
                    .Where(p => p.Users_ID == userId)
                    .ToListAsync();

                // Remove permissions that are no longer selected
                var permissionsToRemove = existingPermissions
                    .Where(p => !featureIds.Contains(p.FeatureId))
                    .ToList();
                _context.UserFeaturePermissions.RemoveRange(permissionsToRemove);

                // Add or update permissions for selected features
                foreach (var featureId in featureIds)
                {
                    var existing = existingPermissions.FirstOrDefault(p => p.FeatureId == featureId);
                    if (existing != null)
                    {
                        // Update existing
                        existing.HasAccess = true;
                        existing.GrantedDate = DateTime.Now;
                        existing.GrantedByUsers_ID = grantedByUserId;
                        existing.ExpiresDate = null;
                    }
                    else
                    {
                        // Add new
                        var newPermission = new UserFeaturePermissionEntity
                        {
                            Users_ID = userId,
                            FeatureId = featureId,
                            HasAccess = true,
                            GrantedDate = DateTime.Now,
                            GrantedByUsers_ID = grantedByUserId
                        };
                        _context.UserFeaturePermissions.Add(newPermission);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating permissions for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<int>> GetUserPermissionIdsAsync(int userId)
        {
            try
            {
                var now = DateTime.Now;

                return await _context.UserFeaturePermissions
                    .Where(p => p.Users_ID == userId
                        && p.HasAccess
                        && (!p.ExpiresDate.HasValue || p.ExpiresDate.Value > now))
                    .Select(p => p.FeatureId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving permission IDs for user {UserId}", userId);
                return new List<int>();
            }
        }

        public async Task<List<int>> GetUsersWithFeatureAccessAsync(int featureId)
        {
            try
            {
                var now = DateTime.Now;
                return await _context.UserFeaturePermissions
                    .Where(p => p.FeatureId == featureId
                        && p.HasAccess
                        && (!p.ExpiresDate.HasValue || p.ExpiresDate.Value > now))
                    .Select(p => p.Users_ID)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users with access to feature {FeatureId}", featureId);
                return new List<int>();
            }
        }

        public async Task<bool> BulkUpdateFeatureAccessAsync(int featureId, List<int> userIdsWithAccess, int grantedByUserId)
        {
            try
            {
                var existing = await _context.UserFeaturePermissions
                    .Where(p => p.FeatureId == featureId)
                    .ToListAsync();

                // Revoke access for users not in the new list
                var toRevoke = existing.Where(p => !userIdsWithAccess.Contains(p.Users_ID)).ToList();
                _context.UserFeaturePermissions.RemoveRange(toRevoke);

                // Grant or update access for users in the list
                foreach (var userId in userIdsWithAccess)
                {
                    var perm = existing.FirstOrDefault(p => p.Users_ID == userId);
                    if (perm != null)
                    {
                        perm.HasAccess = true;
                        perm.GrantedDate = DateTime.Now;
                        perm.GrantedByUsers_ID = grantedByUserId;
                        perm.ExpiresDate = null;
                    }
                    else
                    {
                        _context.UserFeaturePermissions.Add(new UserFeaturePermissionEntity
                        {
                            FeatureId = featureId,
                            Users_ID = userId,
                            HasAccess = true,
                            GrantedDate = DateTime.Now,
                            GrantedByUsers_ID = grantedByUserId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk-updating feature access for feature {FeatureId}", featureId);
                return false;
            }
        }
    }
}
