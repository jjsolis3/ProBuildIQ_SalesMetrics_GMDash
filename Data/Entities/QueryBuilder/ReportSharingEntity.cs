using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("ReportSharing")]
    public class ReportSharingEntity
    {
        [Key]
        public int SharingId { get; set; }

        [Required]
        public int ReportDefinitionId { get; set; }

        [Required]
        public int SharedByUserId { get; set; }

        public int? SharedWithUserId { get; set; }
        public int? SharedWithRoleId { get; set; }

        public DateTime SharedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey(nameof(ReportDefinitionId))]
        public virtual ReportDefinitionEntity ReportDefinition { get; set; } = null!;
    }
}
