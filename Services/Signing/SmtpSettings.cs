namespace SalesMetrics.Services.Signing;

public sealed class SmtpSettings
{
    public string Host { get; set; } = default!;
    public int Port { get; set; } = 587;
    public string User { get; set; } = default!;
    public string? Pass { get; set; }          // read from secrets/env
    public string FromEmail { get; set; } = default!;
    public string FromName { get; set; } = "SalesMetrics";
    public bool EnableSsl { get; set; } = true;
}

public sealed class AppSettings
{
    public string BaseUrl { get; set; } = default!;
}

public sealed class CompanyBrandingSettings
{
    public string CompanyName { get; set; } = "SalesMetrics";
    public string LogoUrl { get; set; } = "/assets/images/logo-light.png";
    /// <summary>
    /// Optional absolute URL for the Envelope feature logo.
    /// When set, it appears inside envelope emails between the orange accent bar
    /// and the body content — completely separate from the company logo in the header.
    /// </summary>
    public string? EnvelopeLogoUrl { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
}
