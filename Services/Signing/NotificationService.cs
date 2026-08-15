using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SalesMetrics.Services.Signing;

public sealed class NotificationService : INotificationService
{
    private readonly ISmtpSettingsProvider _smtpProvider;
    private readonly AppSettings _app;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ISmtpSettingsProvider smtpProvider,
        IOptions<AppSettings> app,
        ILogger<NotificationService> logger)
    {
        _smtpProvider = smtpProvider;
        _app          = app.Value;
        _logger       = logger;
    }

    public async Task SendEnvelopeEmailAsync(string toEmail, string toName, string subject, string bodyHtml, string? replyToEmail = null)
        => await SendAsync(toEmail, toName, subject, bodyHtml, replyToEmail);

    public async Task SendCompletedReceiptAsync(string toEmail, string toName, string subject, string bodyHtml, string downloadUrl)
    {
        // Ensure absolute link in the body
        if (!string.IsNullOrWhiteSpace(downloadUrl) && downloadUrl.StartsWith("/"))
        {
            bodyHtml += $@"<p><a href=""{_app.BaseUrl.TrimEnd('/')}{downloadUrl}"">Download the completed PDF</a></p>";
        }
        await SendAsync(toEmail, toName, subject, bodyHtml);
    }

    public async Task SendFormPdfAsync(string toEmail, string subject, byte[] pdfBytes, string pdfFileName, string bodyHtml)
    {
        var smtp = await _smtpProvider.GetAsync();

        if (string.IsNullOrWhiteSpace(smtp.FromEmail))
            throw new InvalidOperationException(
                "SMTP 'From' address is not configured. Go to Settings → Credentials.");

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromEmail, smtp.FromName),
            Subject = subject,
            Body = bodyHtml,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail));

        using var pdfStream = new System.IO.MemoryStream(pdfBytes);
        message.Attachments.Add(new Attachment(pdfStream, pdfFileName, "application/pdf"));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtp.User, smtp.Pass)
        };

        try { await client.SendMailAsync(message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send form PDF to {Email}", toEmail);
            throw;
        }
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string bodyHtml, string? replyToEmail = null)
    {
        var smtp = await _smtpProvider.GetAsync();

        if (string.IsNullOrWhiteSpace(smtp.FromEmail))
            throw new InvalidOperationException(
                "SMTP 'From' address is not configured. Go to Settings → Credentials and set Smtp_FromEmail (or at minimum Smtp_User).");

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromEmail, smtp.FromName),
            Subject = subject,
            Body = bodyHtml,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail, toName));

        // Set Reply-To so that customers who reply (e.g. to send back a signed/printed
        // document) reach the correct branch inbox rather than the generic sending account.
        if (!string.IsNullOrWhiteSpace(replyToEmail))
            message.ReplyToList.Add(new MailAddress(replyToEmail));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtp.User, smtp.Pass)
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }
}
