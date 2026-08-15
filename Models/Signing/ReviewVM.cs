using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Models.Signing;

public sealed class ReviewVm
{
    public string Token { get; set; } = default!;
    public SignEnvelope Envelope { get; set; } = default!;   // full envelope entity
    public SignRecipient Recipient { get; set; } = default!; // current recipient entity
    public bool HasTenantRecipient { get; set; }
    public PropertyVm? Property { get; set; } // optional property summary

    // Phase 1: template-driven tenant section control
    public bool RequiresTenantSection { get; set; } = true;

    // Phase 2: signer-fillable custom fields from the template MergeSpec
    // Only contains fields with empty/null values that the signer must fill in
    public IReadOnlyList<SignField> RecipientFields { get; set; } = Array.Empty<SignField>();

    // Phase 3: envelope type (Consent | Communication)
    public string EnvelopeType { get; set; } = "Consent";
}

public sealed class ReviewPostVm
{
    public bool Accept { get; set; }
    public string? TypedFullName { get; set; }
    public string? SigData { get; set; }
    public string? TenantFullName { get; set; } // manager-only
    public string? TenantEmail { get; set; }    // manager-only
    public string? TenantPhone { get; set; }    // manager-only: tenant phone number
    public bool SkipTenant { get; set; }        // manager-only: skip tenant signature

    // Phase 2: values for custom signer-filled fields (key = FieldKey, value = entered text)
    public Dictionary<string, string> CustomFields { get; set; } = new();
}
