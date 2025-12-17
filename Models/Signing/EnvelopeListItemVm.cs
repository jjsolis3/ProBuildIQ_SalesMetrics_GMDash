// Models/Signing/EnvelopeListItemVm.cs
using System;

namespace SalesMetrics.Models.Signing;

public sealed class EnvelopeListItemVm
{
    public long EnvelopeId { get; set; }
    public string Subject { get; set; } = default!;
    public string TemplateKey { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? LocationCode { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public int RecipientCount { get; set; }
    public int SignedCount { get; set; }
    public string? PropertyName { get; set; } // (optional—fill via ERP merge svc)
}

