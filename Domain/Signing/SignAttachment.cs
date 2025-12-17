// Domain/Signing/SignAttachment.cs
namespace SalesMetrics.Domain.Signing;

public sealed class SignAttachment
{
    public long AttachmentId { get; set; }
    public long EnvelopeId { get; set; }
    public string FileName { get; set; } = default!;
    public string BlobPath { get; set; } = default!;
    public string? MimeType { get; set; }

    public int UploadedByUsers_ID { get; set; }
    public DateTime UploadedDateUtc { get; set; }

    // Nav
    public SignEnvelope Envelope { get; set; } = default!;
}
