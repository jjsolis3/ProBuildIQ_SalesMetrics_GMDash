// Models/Signing/TemplateMetadata.cs
namespace SalesMetrics.Models.Signing;

/// <summary>
/// Metadata for template selection UI - defines description, icon, and use cases for each template type
/// </summary>
public class TemplateMetadata
{
    public string ViewPath { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Icon { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string UseCase { get; set; } = default!;
    public string Category { get; set; } = default!; // e.g., "Installation", "Consent", "Inspection"

    public static Dictionary<string, TemplateMetadata> GetTemplateMetadata()
    {
        return new Dictionary<string, TemplateMetadata>
        {
            ["OccupiedRelease"] = new()
            {
                ViewPath = "/Views/SignTemplates/OccupiedRelease.cshtml",
                DisplayName = "Occupied Release Form",
                Icon = "🏠",
                Description = "Installation-release language for occupied units with property and tenant context",
                UseCase = "Use when: Flooring/work is being performed in an occupied unit and you need an installation-specific release acknowledgment.",
                Category = "Installation Release"
            },
            ["TenantConsent"] = new()
            {
                ViewPath = "/Views/SignTemplates/TenantConsent.cshtml",
                DisplayName = "Tenant Consent Form",
                Icon = "✅",
                Description = "General-purpose tenant consent language for non-installation scenarios",
                UseCase = "Use when: You need broad tenant consent for property activities/modifications that are not installation-release specific.",
                Category = "Consent"
            }
            // ADD MORE TEMPLATES HERE AS NEEDED:
            // ["MoveInInspection"] = new()
            // {
            //     ViewPath = "/Views/SignTemplates/MoveInInspection.cshtml",
            //     DisplayName = "Move-In Inspection",
            //     Icon = "🔍",
            //     Description = "Document unit condition at move-in",
            //     UseCase = "Use when: Tenant is moving in and you need to document initial unit condition.",
            //     Category = "Inspection"
            // }
        };
    }
}
