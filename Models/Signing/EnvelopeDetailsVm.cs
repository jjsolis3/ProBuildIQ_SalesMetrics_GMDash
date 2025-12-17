// Models/Signing/EnvelopeDetailsVm.cs
using System;
using System.Collections.Generic;

namespace SalesMetrics.Models.Signing;

public sealed class EnvelopeDetailsVm
{
    public long EnvelopeId { get; set; }
    public string TemplateKey { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string? MessageBody { get; set; }
    public string Status { get; set; } = default!;
    public string? LocationCode { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? PdfPath { get; set; }
    public string? PdfSha256Hex { get; set; }

    public List<RecipientVm> Recipients { get; set; } = new();
    public List<EventVm> Events { get; set; } = new();

    public sealed class RecipientVm
    {
        public long RecipientId { get; set; }
        public string Role { get; set; } = default!;
        public int SignerOrder { get; set; }
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public DateTime? ViewedAtUtc { get; set; }
        public DateTime? SignedAtUtc { get; set; }
        public DateTime? DeclinedAtUtc { get; set; }
    }

    public sealed class EventVm
    {
        public string EventType { get; set; } = default!;
        public DateTime OccurredAtUtc { get; set; }
        public string? Recipient { get; set; }
        public string? Meta { get; set; }
    }
}
