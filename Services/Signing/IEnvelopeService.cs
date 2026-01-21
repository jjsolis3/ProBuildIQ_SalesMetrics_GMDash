// Services/Signing/IEnvelopeService.cs
using System.Threading.Tasks;
using SalesMetrics.Domain.Signing;
using SalesMetrics.Models.Signing;

namespace SalesMetrics.Services.Signing
{
    public interface IEnvelopeService
    {
        Task<long> CreateAsync(int createdByUsersId, CreateEnvelopeVm vm);
        Task SendAsync(long envelopeId); // send emails & mark Sent
        Task VoidEnvelopeAsync(long envelopeId, int voidedByUserId, string? reason = null); // void/cancel envelope
        Task<EnvelopeDetailsVm?> GetDetailsAsync(long envelopeId);
        Task<(IReadOnlyList<EnvelopeListItemVm> Rows, int Total)> SearchAsync(string? status, string? office, int page, int pageSize, int? createdByUserId = null, string? scope = null, int? userRoleId = null, int? userLocationId = null);

        // Public flow
        //Task<SignReviewVm?> GetReviewAsync(string token, string userAgent, string ip);
        Task<ReviewVm?> GetReviewAsync(string token, string userAgent, string ip);
        Task<(bool ok, string? downloadUrl)> SignAsync(SignSubmitDto dto, string userAgent, string ip);
        Task<bool> DeclineEnvelopeAsync(string token, string reason, string userAgent, string ip); // Recipient declines to sign
        Task<(bool isComplete, string? downloadUrl)> TryFinalizeEnvelopeAsync(long envelopeId);

        // NEW (used by your POST Review action)
        Task UpsertTenantRecipientAsync(long envelopeId, string fullName, string email, string? phone = null);
        Task MarkTenantSkippedAsync(long envelopeId, string skippedByName);
        Task CaptureSignatureAsync(long envelopeId, long recipientId, string typedFullName, string sigDataBase64);
        Task ProgressToNextAsync(long envelopeId);

        // Template access
        Task<SignTemplate?> GetTemplateByKeyAsync(string templateKey);

    }
}