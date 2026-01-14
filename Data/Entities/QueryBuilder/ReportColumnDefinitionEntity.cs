using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("ReportColumnDefinitions")]
    public class ReportColumnDefinitionEntity
    {
        [Key]
        public int ColumnDefinitionId { get; set; }

        [Required]
        public int ReportDefinitionId { get; set; }

        // Column Identity
        [Required]
        [MaxLength(100)]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        // Display Configuration
        [Required]
        [MaxLength(50)]
        public string DataType { get; set; } = "text";

        [MaxLength(50)]
        public string? FormatString { get; set; }

        public bool IsVisible { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;
        public int? Width { get; set; }

        // Aggregation
        [MaxLength(20)]
        public string? AggregateFunction { get; set; }

        // Conditional Formatting
        [Column(TypeName = "nvarchar(max)")]
        public string? ConditionalFormattingJson { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ReportDefinitionId))]
        public virtual ReportDefinitionEntity ReportDefinition { get; set; } = null!;
    }
}
