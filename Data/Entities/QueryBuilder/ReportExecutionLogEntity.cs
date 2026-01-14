using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("ReportExecutionLog")]
    public class ReportExecutionLogEntity
    {
        [Key]
        public int ExecutionLogId { get; set; }

        [Required]
        public int ReportDefinitionId { get; set; }

        // Execution Details
        [Required]
        public int ExecutedByUserId { get; set; }

        public DateTime ExecutedDate { get; set; } = DateTime.Now;

        public int? ExecutionTimeMs { get; set; }

        // Parameters used
        [Column(TypeName = "nvarchar(max)")]
        public string? ParametersJson { get; set; }

        // Results
        public int? RowCount { get; set; }

        public bool Success { get; set; } = true;

        [Column(TypeName = "nvarchar(max)")]
        public string? ErrorMessage { get; set; }

        // Export
        [MaxLength(20)]
        public string? ExportFormat { get; set; }

        // Navigation Properties
        [ForeignKey(nameof(ReportDefinitionId))]
        public virtual ReportDefinitionEntity ReportDefinition { get; set; } = null!;
    }
}
