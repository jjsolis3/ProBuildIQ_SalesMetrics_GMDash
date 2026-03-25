using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SalesMetrics.Models.EFCore;

namespace SalesMetrics.Data.Entities;

/// <summary>
/// Per-report user access control.
/// A report can be "restricted" (IsRestricted = true on the configuration side),
/// in which case a user must have an explicit row here to run it.
///
/// ReportKey format:
///   • Reports Center (catalog) : "catalog:{reportId}"   e.g. "catalog:margin-commission-discrepancy-open-invoiced"
///   • Query Builder             : "builder:{id}"        e.g. "builder:42"
/// </summary>
[Table("ReportAccess")]
public class ReportAccessEntity
{
    [Key]
    public int AccessId { get; set; }

    /// <summary>Composite key identifying the report ("catalog:xxx" or "builder:nnn").</summary>
    [Required, MaxLength(200)]
    public string ReportKey { get; set; } = default!;

    [Required]
    public int Users_ID { get; set; }

    public DateTime GrantedDate { get; set; } = DateTime.Now;

    public int? GrantedByUsers_ID { get; set; }

    [ForeignKey(nameof(Users_ID))]
    public virtual UserEntity? User { get; set; }
}
