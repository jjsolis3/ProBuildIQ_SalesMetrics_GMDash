using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("QueryFilters")]
    public class QueryFilterEntity
    {
        [Key]
        public int FilterId { get; set; }

        [Required]
        public int ReportDefinitionId { get; set; }

        // Filter Definition
        [Required]
        [MaxLength(100)]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Operator { get; set; } = "=";

        [MaxLength(500)]
        public string? Value { get; set; }

        // Logical Grouping
        public int GroupLevel { get; set; } = 0;

        [MaxLength(10)]
        public string? LogicalOperator { get; set; }

        // Dynamic Parameters
        public bool IsParameter { get; set; } = false;

        [MaxLength(50)]
        public string? ParameterName { get; set; }

        [MaxLength(20)]
        public string? ParameterType { get; set; }

        public bool IsRequired { get; set; } = false;

        [MaxLength(255)]
        public string? DefaultValue { get; set; }

        public int DisplayOrder { get; set; } = 0;

        // Navigation Properties
        [ForeignKey(nameof(ReportDefinitionId))]
        public virtual ReportDefinitionEntity ReportDefinition { get; set; } = null!;
    }
}
