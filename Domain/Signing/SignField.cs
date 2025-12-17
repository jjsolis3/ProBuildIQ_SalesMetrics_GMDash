// Domain/Signing/SignField.cs
namespace SalesMetrics.Domain.Signing;

public sealed class SignField
{
    public long FieldId { get; set; }
    public long EnvelopeId { get; set; }
    public long? RecipientId { get; set; }
    public string FieldKey { get; set; } = default!;
    public string FieldType { get; set; } = default!; // text|checkbox|initials|date
    public string? FieldValue { get; set; }
    public DateTime? SignedAtUtc { get; set; }

    // Nav
    public SignEnvelope Envelope { get; set; } = default!;
    public SignRecipient? Recipient { get; set; }
}
