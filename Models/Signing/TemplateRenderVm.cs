//Models/Signing/TemplateRenderVm.cs
namespace SalesMetrics.Models.Signing;

public sealed class TemplateRenderVm
{
    public required SalesMetrics.Domain.Signing.SignEnvelope Envelope { get; init; }
    public required SalesMetrics.Domain.Signing.SignRecipient Recipient { get; init; }

    // Optional context data for templates. Use what you need in the cshtml.
    public PropertyVm? Property { get; init; }
    public OrderVm? Order { get; init; }
}

// Keep these minimal for now; add fields as your templates need them.
public sealed record PropertyVm
{
    public int PropertyId { get; init; }
    public string? Name { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
    public string? ManagerName { get; init; }
    public string? Phone { get; init; }
    public string? Unit { get; init; }
}

public sealed record OrderVm
{
    public string? OrderId { get; init; }
    public string? UnitNumber { get; init; }
    public string? DeliveryDate { get; init; }
    public string? TenantName { get; init; }
}
