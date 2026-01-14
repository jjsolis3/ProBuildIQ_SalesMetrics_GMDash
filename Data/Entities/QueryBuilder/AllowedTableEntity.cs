using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("AllowedTables")]
    public class AllowedTableEntity
    {
        [Key]
        public int AllowedTableId { get; set; }

        [Required]
        [MaxLength(100)]
        public string TableName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string SchemaName { get; set; } = "dbo";

        [Required]
        [MaxLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        // Data Source Support (Multi-Source)
        [Required]
        [MaxLength(50)]
        public string DataSourceType { get; set; } = "SQL"; // "SQL", "API", "Kudu"

        [MaxLength(500)]
        public string? ApiEndpoint { get; set; }

        [MaxLength(10)]
        public string? ApiMethod { get; set; }

        // Security
        public int? RequiresRoleId { get; set; }

        [MaxLength(255)]
        public string? RequiresLocation { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public virtual ICollection<AllowedColumnEntity> Columns { get; set; } = new List<AllowedColumnEntity>();
    }
}
