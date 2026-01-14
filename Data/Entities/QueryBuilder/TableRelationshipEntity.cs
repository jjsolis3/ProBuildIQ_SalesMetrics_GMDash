using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("TableRelationships")]
    public class TableRelationshipEntity
    {
        [Key]
        public int RelationshipId { get; set; }

        [Required]
        public int FromTableId { get; set; }

        [Required]
        public int ToTableId { get; set; }

        // Join Definition
        [Required]
        [MaxLength(100)]
        public string FromColumnName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ToColumnName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string RelationshipType { get; set; } = "ONE_TO_MANY";

        // Display
        [MaxLength(255)]
        public string? DisplayName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        // Auto-suggest
        public bool IsSuggestedJoin { get; set; } = true;

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        [ForeignKey(nameof(FromTableId))]
        public virtual AllowedTableEntity FromTable { get; set; } = null!;

        [ForeignKey(nameof(ToTableId))]
        public virtual AllowedTableEntity ToTable { get; set; } = null!;
    }
}
