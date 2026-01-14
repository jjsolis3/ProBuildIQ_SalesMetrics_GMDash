using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("QueryTableReferences")]
    public class QueryTableReferenceEntity
    {
        [Key]
        public int TableReferenceId { get; set; }

        [Required]
        public int ReportDefinitionId { get; set; }

        // Table Info
        [Required]
        [MaxLength(100)]
        public string TableName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TableAlias { get; set; } = string.Empty;

        public bool IsBaseTable { get; set; } = false;

        // Join Configuration
        [MaxLength(20)]
        public string? JoinType { get; set; }

        [MaxLength(500)]
        public string? JoinCondition { get; set; }

        public int? JoinToTableId { get; set; }

        public int DisplayOrder { get; set; } = 0;

        // Navigation Properties
        [ForeignKey(nameof(ReportDefinitionId))]
        public virtual ReportDefinitionEntity ReportDefinition { get; set; } = null!;

        [ForeignKey(nameof(JoinToTableId))]
        public virtual QueryTableReferenceEntity? JoinToTable { get; set; }
    }
}
