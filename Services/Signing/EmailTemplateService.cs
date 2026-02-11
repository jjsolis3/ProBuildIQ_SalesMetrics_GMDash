using Microsoft.Extensions.Options;

namespace SalesMetrics.Services.Signing;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private readonly CompanyBrandingSettings _branding;

    public EmailTemplateService(IOptions<CompanyBrandingSettings> branding)
    {
        _branding = branding.Value;
    }

    public string WrapInBrandedTemplate(string innerHtml)
    {
        var companyName = _branding.CompanyName;
        var logoUrl = _branding.LogoUrl;
        var website = _branding.Website;
        var phone = _branding.Phone;
        var year = DateTime.Now.Year;

        // Build footer contact line
        var contactParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(phone))
            contactParts.Add(phone);
        if (!string.IsNullOrWhiteSpace(website))
        {
            var displayUrl = website.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            contactParts.Add($"<a href=\"{website}\" style=\"color:#6b7280;text-decoration:underline;\">{displayUrl}</a>");
        }
        var contactLine = contactParts.Count > 0
            ? $"<p style=\"margin:4px 0 0 0;font-size:12px;color:#6b7280;\">{string.Join(" &nbsp;|&nbsp; ", contactParts)}</p>"
            : "";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>{companyName}</title>
</head>
<body style=""margin:0;padding:0;background-color:#f3f4f6;font-family:Arial,Helvetica,sans-serif;"">
<!-- Wrapper table for full-width background -->
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f3f4f6;"">
<tr><td align=""center"" style=""padding:24px 16px;"">

<!-- Main container -->
<table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.1);"">

  <!-- Header with logo -->
  <tr>
    <td style=""background-color:#DBDBDB;padding:20px 32px;text-align:center;"">
      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
        <tr>
          <td align=""center"">
            <img src=""{logoUrl}"" alt=""{companyName}"" style=""max-height:50px;max-width:260px;width:auto;display:block;margin:0 auto;"" />
          </td>
        </tr>
      </table>
    </td>
  </tr>

  <!-- Accent bar -->
  <tr>
    <td style=""background-color:#E87600;height:4px;font-size:0;line-height:0;"">&nbsp;</td>
  </tr>

  <!-- Body content -->
  <tr>
    <td style=""padding:32px 32px 24px 32px;"">
      {innerHtml}
    </td>
  </tr>

  <!-- Divider -->
  <tr>
    <td style=""padding:0 32px;"">
      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
        <tr><td style=""border-top:1px solid #e5e7eb;height:1px;font-size:0;line-height:0;"">&nbsp;</td></tr>
      </table>
    </td>
  </tr>

  <!-- Footer -->
  <tr>
    <td style=""padding:20px 32px 24px 32px;text-align:center;"">
      <p style=""margin:0;font-size:12px;color:#6b7280;"">&copy; {year} {companyName}. All rights reserved.</p>
      {contactLine}
      <p style=""margin:12px 0 0 0;font-size:11px;color:#9ca3af;"">Powered by ProBuild<span style=""color:#E87600;"">IQ</span> SalesMetrics</p>
    </td>
  </tr>

</table>
<!-- End main container -->

</td></tr>
</table>
<!-- End wrapper -->
</body>
</html>";
    }
}
