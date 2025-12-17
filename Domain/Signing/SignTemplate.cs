// Domain/Signing/SignTemplate.cs
namespace SalesMetrics.Domain.Signing;

public sealed class SignTemplate
{
    public string TemplateKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string RazorViewPath { get; set; } = default!;
    public string? MergeSpecJson { get; set; }
    public string? DefaultSubject { get; set; }
    public string? DefaultMessage { get; set; }
    public bool IsActive { get; set; } = true;

    public int CreatedByUsers_ID { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public int? ModifiedByUsers_ID { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }

    // Nav
    public ICollection<SignEnvelope> Envelopes { get; set; } = new List<SignEnvelope>();
}
