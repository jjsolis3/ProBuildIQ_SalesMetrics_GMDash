namespace SalesMetrics.Services.Signing;

public interface IEmailTemplateService
{
    /// <summary>
    /// Wraps the given inner HTML content in a branded email layout with header, logo, and footer.
    /// </summary>
    string WrapInBrandedTemplate(string innerHtml);
}
