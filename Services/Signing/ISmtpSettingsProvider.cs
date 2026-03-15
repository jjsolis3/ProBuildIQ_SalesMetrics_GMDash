namespace SalesMetrics.Services.Signing;

/// <summary>
/// Resolves SMTP settings at runtime.
/// Values stored encrypted in AppCredentials (Category="Smtp") take precedence;
/// falls back to the appsettings.json "Smtp" section when a DB row is absent or empty.
/// </summary>
public interface ISmtpSettingsProvider
{
    Task<SmtpSettings> GetAsync();
}
