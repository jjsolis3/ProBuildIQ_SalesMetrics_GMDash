using Microsoft.Extensions.Options;
using SalesMetrics.Services.Settings;

namespace SalesMetrics.Services.Signing;

/// <summary>
/// DB-first SMTP settings: reads encrypted credentials from AppCredentials
/// (Category="Smtp") and falls back to the appsettings.json "Smtp" section
/// for any value not yet stored in the database.
/// </summary>
public sealed class SmtpSettingsProvider : ISmtpSettingsProvider
{
    private readonly IAppCredentialsProvider _creds;
    private readonly SmtpSettings _defaults;

    public SmtpSettingsProvider(IAppCredentialsProvider creds, IOptions<SmtpSettings> defaults)
    {
        _creds    = creds;
        _defaults = defaults.Value;
    }

    public async Task<SmtpSettings> GetAsync()
    {
        string? Coalesce(string? dbVal, string? fallback)
            => !string.IsNullOrWhiteSpace(dbVal) ? dbVal : fallback;

        var host      = await _creds.GetDecryptedAsync("Smtp_Host");
        var portStr   = await _creds.GetDecryptedAsync("Smtp_Port");
        var user      = await _creds.GetDecryptedAsync("Smtp_User");
        var pass      = await _creds.GetDecryptedAsync("Smtp_Pass");
        var fromEmail = await _creds.GetDecryptedAsync("Smtp_FromEmail");
        var fromName  = await _creds.GetDecryptedAsync("Smtp_FromName");

        var resolvedUser = Coalesce(user, _defaults.User) ?? "";

        return new SmtpSettings
        {
            Host      = Coalesce(host,      _defaults.Host)      ?? "smtp.gmail.com",
            Port      = int.TryParse(portStr, out var p) ? p : _defaults.Port,
            User      = resolvedUser,
            Pass      = Coalesce(pass,      _defaults.Pass),
            // If Smtp_FromEmail isn't set, fall back to the SMTP username — they are
            // always the same for Gmail/standard providers and this avoids the empty-address crash.
            FromEmail = Coalesce(fromEmail, _defaults.FromEmail) ?? resolvedUser,
            FromName  = Coalesce(fromName,  _defaults.FromName)  ?? "SalesMetrics",
            EnableSsl = _defaults.EnableSsl,
        };
    }
}
