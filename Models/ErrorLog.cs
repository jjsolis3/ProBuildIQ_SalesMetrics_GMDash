using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models
{
    public class ErrorLog
    {
        public long ErrorID { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Level { get; set; } = "Error";

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? Exception { get; set; }
        public string? InnerException { get; set; }

        [StringLength(200)]
        public string? RequestId { get; set; }

        [StringLength(200)]
        public string? TraceId { get; set; }

        [StringLength(200)]
        public string? SessionID { get; set; }

        public int? UserID { get; set; }

        [StringLength(100)]
        public string? Username { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }

        [StringLength(500)]
        public string? RequestPath { get; set; }

        [StringLength(10)]
        public string? RequestMethod { get; set; }

        [StringLength(1000)]
        public string? QueryString { get; set; }

        [StringLength(45)]
        public string? IPAddress { get; set; }

        [StringLength(100)]
        public string? Controller { get; set; }

        [StringLength(100)]
        public string? Action { get; set; }

        [StringLength(100)]
        public string? Area { get; set; }

        public string? AdditionalData { get; set; }

        [StringLength(200)]
        public string? Source { get; set; }

        public bool IsResolved { get; set; } = false;

        [StringLength(100)]
        public string? ResolvedBy { get; set; }

        public DateTime? ResolvedDate { get; set; }

        public string? ResolutionNotes { get; set; }
    }
}
