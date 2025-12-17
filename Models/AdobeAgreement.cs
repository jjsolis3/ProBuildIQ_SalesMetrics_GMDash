using System;

namespace SalesMetrics.Models
{
    public class AdobeAgreement
    {
        public int Id { get; set; }
        public string AgreementId { get; set; } = "";
        public string? TemplateId { get; set; }
        public string ManagerEmail { get; set; } = "";
        public string? TenantEmail { get; set; }
        public string PlaceholderTenantEmail { get; set; } = "";
        public int? PropertyId { get; set; }
        public int CreatedBy { get; set; }
        public string Status { get; set; } = "OUT_FOR_SIGNATURE";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
