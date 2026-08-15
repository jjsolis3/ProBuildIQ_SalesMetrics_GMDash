using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalesMetrics.Data;

namespace SalesMetrics.Services.Signing;

/// <summary>
/// Resolves branding settings: DB rows (SecuritySettings, Category="Branding") override
/// appsettings values. Empty DB strings fall through to the appsettings default.
/// </summary>
public sealed class BrandingSettingsProvider : IBrandingSettingsProvider
{
    private readonly SalesMetricsDbContext _db;
    private readonly CompanyBrandingSettings _defaults;

    public BrandingSettingsProvider(SalesMetricsDbContext db, IOptions<CompanyBrandingSettings> defaults)
    {
        _db       = db;
        _defaults = defaults.Value;
    }

    public async Task<CompanyBrandingSettings> GetAsync()
    {
        var dbRows = await _db.SecuritySettings
            .Where(s => s.Category == "Branding")
            .ToListAsync();

        string? Resolve(string key, string? fallback)
        {
            var row = dbRows.FirstOrDefault(r => r.SettingKey == key);
            return !string.IsNullOrWhiteSpace(row?.SettingValue) ? row!.SettingValue : fallback;
        }

        return new CompanyBrandingSettings
        {
            CompanyName     = Resolve("CompanyName",     _defaults.CompanyName)     ?? "SalesMetrics",
            LogoUrl         = Resolve("LogoUrl",         _defaults.LogoUrl)         ?? "/assets/images/logo-light.png",
            EnvelopeLogoUrl = Resolve("EnvelopeLogoUrl", _defaults.EnvelopeLogoUrl),
            Website         = Resolve("Website",         _defaults.Website),
            Phone           = Resolve("Phone",           _defaults.Phone),
        };
    }
}
