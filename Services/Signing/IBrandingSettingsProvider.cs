namespace SalesMetrics.Services.Signing;

/// <summary>
/// Resolves Company Branding settings at runtime.
/// DB values (SecuritySettings, Category="Branding") take precedence; falls back to appsettings CompanyBranding section.
/// </summary>
public interface IBrandingSettingsProvider
{
    /// <summary>Returns the effective branding settings, merging DB overrides with appsettings defaults.</summary>
    Task<CompanyBrandingSettings> GetAsync();
}
