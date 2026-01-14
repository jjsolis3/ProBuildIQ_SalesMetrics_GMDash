using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesMetrics.Data.Entities.QueryBuilder
{
    [Table("UserFavoriteReports")]
    public class UserFavoriteReportEntity
    {
        [Key]
        public int FavoriteId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int ReportDefinitionId { get; set; }

        public DateTime AddedDate { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey(nameof(ReportDefinitionId))]
        public virtual ReportDefinitionEntity ReportDefinition { get; set; } = null!;
    }
}
