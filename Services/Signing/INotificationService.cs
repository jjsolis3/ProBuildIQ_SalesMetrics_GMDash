// Services/Signing/INotificationService.cs
using System.Threading.Tasks;

namespace SalesMetrics.Services.Signing;

public interface INotificationService
{
    /// <param name="replyToEmail">
    /// Optional Reply-To address. When set, email clients will direct replies here instead
    /// of the sending account — used so that customers who print and scan documents back
    /// can reply directly to the branch inbox rather than the generic sending account.
    /// </param>
    Task SendEnvelopeEmailAsync(string toEmail, string toName, string subject, string bodyHtml, string? replyToEmail = null);
    Task SendCompletedReceiptAsync(string toEmail, string toName, string subject, string bodyHtml, string downloadUrl);
}
