// Domain/Signing/SignRecipient.cs
using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Domain.Signing;

public sealed class SignRecipient
{
    public long RecipientId { get; set; }
    public long EnvelopeId { get; set; }
    public string Role { get; set; } = default!; // Manager|Tenant|Other
    public int SignerOrder { get; set; } = 1;

    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }

    public string AccessToken { get; set; } = default!;
    public DateTime? AccessTokenExpiresAt { get; set; }
    public bool RequiresOtp { get; set; }
    public DateTime? LastOtpSentAtUtc { get; set; }

    public DateTime? ViewedAtUtc { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public DateTime? DeclinedAtUtc { get; set; }
    public string? IPAddressViewed { get; set; }
    public string? IPAddressSigned { get; set; }
    public string? UserAgentViewed { get; set; }
    public string? UserAgentSigned { get; set; }

    [MaxLength(200)]
    public string? TypedFullName { get; set; } // signer typed attestation

    public string? SignatureImagePath { get; set; }
    public string? SignatureTyped { get; set; }
    public string? SignatureMetaJson { get; set; }

    // Was Missing,  is it correct. I added it, to avoid error triggered
    public DateTime? CreatedDateUtc { get; set; }

    // Nav
    public SignEnvelope Envelope { get; set; } = default!;
    public ICollection<SignEvent> Events { get; set; } = new List<SignEvent>();
}
