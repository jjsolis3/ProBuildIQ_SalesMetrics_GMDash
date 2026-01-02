using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Models.Signing;

public sealed class ReviewVm
{
    public string Token { get; set; } = default!;
    public SignEnvelope Envelope { get; set; } = default!;   // full envelope entity
    public SignRecipient Recipient { get; set; } = default!; // current recipient entity
    public bool HasTenantRecipient { get; set; }
    public PropertyVm? Property { get; set; } // optional property summary
    public string HtmlBody { get; set; } = default!; // rendered Razor HTML
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
}
