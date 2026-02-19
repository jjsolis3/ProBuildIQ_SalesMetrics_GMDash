using SalesMetrics.Services.Helpers;

namespace SalesMetrics.Models.Reports
{
    /// <summary>
    /// Input + output view model for the Envelope Activity Report.
    /// Input fields are bound from the filter form; output fields are populated
    /// by the controller after querying the SalesMetrics database.
    /// </summary>
    public class EnvelopeReportViewModel
    {
        // ── Filter inputs (bound from POST form) ────────────────────────────
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? StatusFilter { get; set; }
        /// <summary>Branch code (LSV, PHX, …) — only used when the current user can view all branches.</summary>
        public string? BranchFilter { get; set; }

        // ── KPI summary ──────────────────────────────────────────────────────
        public int TotalEnvelopes { get; set; }
        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public int ExpiredVoidedCount { get; set; }
        public double CompletionRate { get; set; }
        public double AvgDaysToComplete { get; set; }

        // ── Detail rows ──────────────────────────────────────────────────────
        public List<EnvelopeReportRow> Rows { get; set; } = new();

        // ── UI state ─────────────────────────────────────────────────────────
        public bool ShowResults { get; set; }
        public string? ErrorMessage { get; set; }
        /// <summary>True for Admin, GM, President, Regional Manager — lets them pick any branch.</summary>
        public bool CanViewAllBranches { get; set; }
        public Dictionary<int, (string Code, string Name)> Locations { get; set; } = new();
        public string CurrentLocationCode { get; set; } = "";
    }

    public class EnvelopeReportRow
    {
        public long EnvelopeId { get; set; }
        public string Subject { get; set; } = "";
        public string? OrderNumber { get; set; }
        public string? LocationCode { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedDateUtc { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int RecipientCount { get; set; }
        public int SignedCount { get; set; }
        /// <summary>Business days from sent to completed; null if not yet completed.</summary>
        public double? DaysToComplete { get; set; }
    }
}
