// Domain/Signing/SignTemplate.cs
namespace SalesMetrics.Domain.Signing;

public sealed class SignTemplate
{
    public string TemplateKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;

    /// <summary>
    /// Legacy: path to a compiled Razor view that renders the signing review page.
    /// Used as fallback when HtmlBodyContent is null.
    /// </summary>
    public string? RazorViewPath { get; set; }

    /// <summary>
    /// DB-stored HTML body for the signing review page.
    /// Supports {{TokenName}} replacement at runtime (see HtmlBodyTokens for available tokens).
    /// When set, takes precedence over RazorViewPath.
    /// </summary>
    public string? HtmlBodyContent { get; set; }

    public string? MergeSpecJson { get; set; }
    public string? PdfFilePath { get; set; }  // Path to uploaded base PDF for stamping
    public string? DefaultSubject { get; set; }
    public string? DefaultMessage { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When true (default), the signing page shows the Tenant Information capture block
    /// so the Manager can add or skip a tenant. Set to false for templates that never
    /// involve a tenant (e.g. communication-only or staff-only consent forms).
    /// </summary>
    public bool RequiresTenantSection { get; set; } = true;

    /// <summary>
    /// Classifies the template's intended use: "Consent" (requires a signature) or
    /// "Communication" (tracked delivery, no signature). Controls which templates
    /// appear in the dropdown when creating an envelope of a given type.
    /// </summary>
    public string TemplateType { get; set; } = "Consent";

    /// <summary>
    /// Comma-separated list of branch codes that may use this template (e.g. "LAX,CHN,SND").
    /// NULL/empty means the template is visible to all branches (global/shared).
    /// </summary>
    public string? LocationCodes { get; set; }

    public int CreatedByUsers_ID { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public int? ModifiedByUsers_ID { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }

    // Nav
    public ICollection<SignEnvelope> Envelopes { get; set; } = new List<SignEnvelope>();
}
