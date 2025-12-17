// Services/Signing/INotificationService.cs
using System.Threading.Tasks;

namespace SalesMetrics.Services.Signing;

public interface INotificationService
{
    Task SendEnvelopeEmailAsync(string toEmail, string toName, string subject, string bodyHtml);
    Task SendCompletedReceiptAsync(string toEmail, string toName, string subject, string bodyHtml, string downloadUrl);
}
