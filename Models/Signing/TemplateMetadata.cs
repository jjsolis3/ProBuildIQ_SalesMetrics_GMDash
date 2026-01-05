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
                Description = "Release form for installations in occupied units",
                UseCase = "Use when: Installing in a unit currently occupied by a tenant. Requires property staff and resident signatures.",
                Category = "Installation Release"
            },
            ["TenantConsent"] = new()
            {
                ViewPath = "/Views/SignTemplates/TenantConsent.cshtml",
                DisplayName = "Tenant Consent Form",
                Icon = "✅",
                Description = "General consent form for tenant agreements",
                UseCase = "Use when: Obtaining tenant consent for general property activities or modifications.",
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
