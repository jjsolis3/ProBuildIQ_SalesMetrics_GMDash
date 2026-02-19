// Models/Signing/EditEnvelopeVm.cs
namespace SalesMetrics.Models.Signing;

/// <summary>
/// View-model for editing an existing envelope.
/// Only fields that are safe to change after sending are included.
/// Recipients who have already signed are sent read-only; unsigned recipients
/// can have their name/email/phone corrected and optionally have a fresh
/// invitation email queued.
/// </summary>
public sealed class EditEnvelopeVm
{
    public long EnvelopeId { get; set; }

    public string Subject { get; set; } = default!;
    public string? MessageBody { get; set; }

    // Read-only context (shown on the form, not editable)
    public string Status { get; set; } = default!;
    public string? PropertyName { get; set; }
    public string? OrderNumber { get; set; }
    public string? LocationCode { get; set; }

    public List<EditRecipientVm> Recipients { get; set; } = new();
}

public sealed class EditRecipientVm
{
    public long RecipientId { get; set; }
    public string Role { get; set; } = default!;
    public int SignerOrder { get; set; }

    // Editable only when HasSigned == false
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }

    /// <summary>True when this recipient has already signed — their fields are read-only.</summary>
    public bool HasSigned { get; set; }

    /// <summary>
    /// When checked, a fresh invitation email is sent to this recipient after saving.
    /// Automatically set to true when the email address is changed.
    /// </summary>
    public bool ResendInvite { get; set; }
}
