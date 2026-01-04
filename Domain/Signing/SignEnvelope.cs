// Domain/Signing/SignEnvelope.cs
namespace SalesMetrics.Domain.Signing;

public sealed class SignEnvelope
{
    public long EnvelopeId { get; set; }
    public string TemplateKey { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string? MessageBody { get; set; }

    // Business context
    public int? PropertyID { get; set; }
    public int? OrderId { get; set; }
    public string? OrderNumber { get; set; } // e.g., "90805.4" - display friendly order number
    public string? CustomerNumber { get; set; }
    public string? LocationCode { get; set; }

    // Tenant skip tracking
    public bool TenantSkipped { get; set; } // true if Property Staff skipped tenant signature
    public string? TenantSkippedByName { get; set; } // Name of staff member who skipped tenant
    public DateTime? TenantSkippedAtUtc { get; set; } // When tenant was skipped

    public string Status { get; set; } = "Draft"; // Draft|Sent|Viewed|Completed|Expired|Declined|Voided
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? DeclinedAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }

    public string? PdfStoragePath { get; set; }
    public byte[]? PdfSha256 { get; set; }

    public int CreatedByUsers_ID { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public int? ModifiedByUsers_ID { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }

    // Nav
    public SignTemplate Template { get; set; } = default!;
    public ICollection<SignRecipient> Recipients { get; set; } = new List<SignRecipient>();
    public ICollection<SignEvent> Events { get; set; } = new List<SignEvent>();
    public ICollection<SignAttachment>? Attachments { get; set; }
    public ICollection<SignField>? Fields { get; set; }
}
