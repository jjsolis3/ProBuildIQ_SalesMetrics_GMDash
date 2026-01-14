using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("DataSourceConfigurations")]
    public class DataSourceConfigurationEntity
    {
        [Key]
        public int DataSourceConfigId { get; set; }

        [Required]
        [MaxLength(50)]
        public string DataSourceType { get; set; } = string.Empty; // "SQL", "API", "Kudu"

        [Required]
        [MaxLength(100)]
        public string ConfigName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        // Connection Info
        [MaxLength(1000)]
        public string? ConnectionString { get; set; }

        [MaxLength(500)]
        public string? BaseUrl { get; set; }

        [MaxLength(50)]
        public string? AuthenticationType { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? AuthenticationConfig { get; set; }

        // Settings
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRowsPerQuery { get; set; } = 10000;

        // Audit
        public int CreatedByUserId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? ModifiedByUserId { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }
}
