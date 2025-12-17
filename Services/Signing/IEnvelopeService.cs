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
        Task<EnvelopeDetailsVm?> GetDetailsAsync(long envelopeId);
        Task<(IReadOnlyList<EnvelopeListItemVm> Rows, int Total)> SearchAsync(string? status, string? office, int page, int pageSize);

        // Public flow
        //Task<SignReviewVm?> GetReviewAsync(string token, string userAgent, string ip);
        Task<ReviewVm?> GetReviewAsync(string token, string userAgent, string ip);
        Task<(bool ok, string? downloadUrl)> SignAsync(SignSubmitDto dto, string userAgent, string ip);
        Task<(bool isComplete, string? downloadUrl)> TryFinalizeEnvelopeAsync(long envelopeId);

        // NEW (used by your POST Review action)
        Task UpsertTenantRecipientAsync(long envelopeId, string fullName, string email);
        Task CaptureSignatureAsync(long envelopeId, long recipientId, string typedFullName, string sigDataBase64);
        Task ProgressToNextAsync(long envelopeId);

    }
}