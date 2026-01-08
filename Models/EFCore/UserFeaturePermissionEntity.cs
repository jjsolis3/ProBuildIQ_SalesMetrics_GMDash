using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Models.EFCore
{
    /// <summary>
    /// Represents a user's permission to access a specific feature
    /// </summary>
    [Table("UserFeaturePermissions")]
    public class UserFeaturePermissionEntity
    {
        [Key]
        public int PermissionId { get; set; }

        /// <summary>
        /// User ID (from Users table)
        /// </summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>
        /// Feature ID (from Features table)
        /// </summary>
        [Required]
        public int FeatureId { get; set; }

        /// <summary>
        /// Whether the user has access to this feature
        /// </summary>
        public bool HasAccess { get; set; } = true;

        /// <summary>
        /// When this permission was granted
        /// </summary>
        public DateTime GrantedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Who granted this permission (UserID)
        /// </summary>
        public int? GrantedByUserId { get; set; }

        /// <summary>
        /// Optional expiration date for temporary access
        /// </summary>
        public DateTime? ExpiresDate { get; set; }

        /// <summary>
        /// Navigation property to User
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual UserEntity? User { get; set; }

        /// <summary>
        /// Navigation property to Feature
        /// </summary>
        [ForeignKey(nameof(FeatureId))]
        public virtual FeatureEntity? Feature { get; set; }
    }
}
