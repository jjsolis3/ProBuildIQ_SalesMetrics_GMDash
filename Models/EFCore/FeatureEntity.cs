using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Models.EFCore
{
    /// <summary>
    /// Represents a feature or section in the application that can be access-controlled
    /// </summary>
    [Table("Features")]
    public class FeatureEntity
    {
        [Key]
        public int FeatureId { get; set; }

        /// <summary>
        /// Unique identifier for the feature (e.g., "Reports", "Dashboard", "Users", "Tasks")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string FeatureCode { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the feature
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this feature provides
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Category for grouping features (e.g., "Core", "Admin", "Reports")
        /// </summary>
        [MaxLength(50)]
        public string? Category { get; set; }

        /// <summary>
        /// Whether this feature is currently active/available
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Display order for UI
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Navigation property for user permissions
        /// </summary>
        public virtual ICollection<UserFeaturePermissionEntity> UserPermissions { get; set; } = new List<UserFeaturePermissionEntity>();
    }
}
