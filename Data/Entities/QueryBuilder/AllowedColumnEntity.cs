using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("AllowedColumns")]
    public class AllowedColumnEntity
    {
        [Key]
        public int AllowedColumnId { get; set; }

        [Required]
        public int AllowedTableId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DataType { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Security
        public bool IsSensitive { get; set; } = false;

        // Query Building Hints
        public bool IsFilterable { get; set; } = true;
        public bool IsSortable { get; set; } = true;
        public bool IsAggregatable { get; set; } = false;

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        [ForeignKey(nameof(AllowedTableId))]
        public virtual AllowedTableEntity AllowedTable { get; set; } = null!;
    }
}
