using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("ReportTemplates")]
    public class ReportTemplateEntity
    {
        [Key]
        public int TemplateId { get; set; }

        [Required]
        [MaxLength(255)]
        public string TemplateName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Category { get; set; }

        // Template Structure
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string QueryDefinitionJson { get; set; } = string.Empty;

        // Display
        [MaxLength(50)]
        public string? IconClass { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
