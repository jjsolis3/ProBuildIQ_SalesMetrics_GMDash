namespace SalesMetrics.Services.Signing;

public interface IEmailTemplateService
{
    /// <summary>
    /// Wraps the given inner HTML content in a branded email layout with header, logo, and footer.
    /// Branding values are resolved from DB (overrides) with appsettings as fallback.
    /// </summary>
    Task<string> WrapInBrandedTemplateAsync(string innerHtml);
}
