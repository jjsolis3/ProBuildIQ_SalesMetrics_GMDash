using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SalesMetrics.Services.Signing;

public sealed class NotificationService : INotificationService
{
    private readonly SmtpSettings _smtp;
    private readonly AppSettings _app;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IOptions<SmtpSettings> smtp,
        IOptions<AppSettings> app,
        ILogger<NotificationService> logger)
    {
        _smtp = smtp.Value;
        _app = app.Value;
        _logger = logger;
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

    private async Task SendAsync(string toEmail, string toName, string subject, string bodyHtml, string? replyToEmail = null)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_smtp.FromEmail, _smtp.FromName),
            Subject = subject,
            Body = bodyHtml,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail, toName));

        // Set Reply-To so that customers who reply (e.g. to send back a signed/printed
        // document) reach the correct branch inbox rather than the generic sending account.
        if (!string.IsNullOrWhiteSpace(replyToEmail))
            message.ReplyToList.Add(new MailAddress(replyToEmail));

        using var client = new SmtpClient(_smtp.Host, _smtp.Port)
        {
            EnableSsl = _smtp.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_smtp.User, _smtp.Pass)
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
