// Models/Signing/SignPublicVms.cs
using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models.Signing;

public sealed class SignReviewVm
{
    public long EnvelopeId { get; set; }
    public long RecipientId { get; set; }
    public string Subject { get; set; } = default!;
    public string TemplateKey { get; set; } = default!;
    public string HtmlAgreement { get; set; } = default!; // rendered HTML preview
    public string RecipientName { get; set; } = default!;
    public string RecipientEmail { get; set; } = default!;
}

public sealed class SignSubmitDto
{
    [Required] public string Token { get; set; } = default!; // access token
    public bool Consented { get; set; }
    public string? SignatureTyped { get; set; } // if typed
    public string? SignatureImageBase64 { get; set; } // if drawn
}
