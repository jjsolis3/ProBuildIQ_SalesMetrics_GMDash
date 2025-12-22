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
    public string? Website { get; set; }
    public string? Phone { get; set; }
}
