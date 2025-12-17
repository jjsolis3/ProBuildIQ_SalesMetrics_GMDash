// Domain/Signing/SignEvent.cs
namespace SalesMetrics.Domain.Signing;

public sealed class SignEvent
{
    public long EventId { get; set; }
    public long EnvelopeId { get; set; }
    public long? RecipientId { get; set; }
    public string EventType { get; set; } = default!; // Sent|Opened|Consented|Signed|Downloaded|Reminded|Expired|Declined|Voided
    public DateTime OccurredAtUtc { get; set; }
    public string? MetaJson { get; set; }

    // Nav
    public SignEnvelope Envelope { get; set; } = default!;
    public SignRecipient? Recipient { get; set; }
}
