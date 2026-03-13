// Models/Signing/CreateEnvelopeVm.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SalesMetrics.Models.Signing;

public sealed class CreateEnvelopeVm
{
    [Required] public string TemplateKey { get; set; } = default!;
    [Required, StringLength(200)] public string Subject { get; set; } = default!;
    [StringLength(int.MaxValue)] public string? MessageBody { get; set; }

    // Context (optional)
    public int? PropertyID { get; set; }
    public string? CustomPropertyName { get; set; } // Free-text name used when no ERP property is selected
    public int? OrderId { get; set; }
    public string? CustomOrderNumber { get; set; }  // Free-text order number when no ERP match
    public string? CustomUnitNumber { get; set; }   // Free-text unit number when no ERP match
    public string? CustomerNumber { get; set; }
    public string? LocationCode { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    // If true, pre-check "Skip tenant signature" when property staff opens signing page.
    public bool PreselectSkipTenant { get; set; }

    // Recipients
    public List<RecipientInput> Recipients { get; set; } = new();

    public sealed class RecipientInput
    {
        [Required] public string Role { get; set; } = "Manager";
        [Range(1, 10)] public int SignerOrder { get; set; } = 1;
        [Required] public string FullName { get; set; } = default!;
        [Required, EmailAddress] public string Email { get; set; } = default!;
        public string? Phone { get; set; }
    }

    // For dropdowns
    public List<(string Key, string Display)> Templates { get; set; } = new();
}
