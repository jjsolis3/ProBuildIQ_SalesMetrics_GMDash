using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("ReportDefinitions")]
    public class ReportDefinitionEntity
    {
        [Key]
        public int ReportDefinitionId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ReportId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        // Report Type
        public bool IsCustom { get; set; } = false;

        // Query Definition (for custom reports)
        [Column(TypeName = "nvarchar(max)")]
        public string? QueryDefinitionJson { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? GeneratedSql { get; set; }

        // Data Source (Multi-Source Support)
        [Required]
        [MaxLength(50)]
        public string DataSourceType { get; set; } = "SQL"; // "SQL", "API", "Kudu"

        [Column(TypeName = "nvarchar(max)")]
        public string? DataSourceConfig { get; set; }

        // Authorization
        [MaxLength(255)]
        public string? AllowedRoles { get; set; }

        [MaxLength(255)]
        public string? AllowedLocations { get; set; }

        // Status & Versioning
        public bool IsActive { get; set; } = true;
        public int Version { get; set; } = 1;
        public int? ParentReportId { get; set; }

        // Audit
        public int CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? ModifiedByUserId { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Scheduling
        public bool IsScheduled { get; set; } = false;

        [MaxLength(100)]
        public string? ScheduleCron { get; set; }

        public DateTime? LastExecutedDate { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ParentReportId))]
        public virtual ReportDefinitionEntity? ParentReport { get; set; }

        public virtual ICollection<ReportColumnDefinitionEntity> Columns { get; set; } = new List<ReportColumnDefinitionEntity>();
        public virtual ICollection<QueryTableReferenceEntity> Tables { get; set; } = new List<QueryTableReferenceEntity>();
        public virtual ICollection<QueryFilterEntity> Filters { get; set; } = new List<QueryFilterEntity>();
    }
}
