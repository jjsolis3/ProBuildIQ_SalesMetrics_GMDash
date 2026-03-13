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

        /// <summary>
        /// Edit subject, message body, and/or unsigned recipient details.
        /// If a recipient's email is changed, their access token is regenerated and a
        /// fresh invitation is sent automatically.  Signed recipients are never modified.
        /// </summary>
        Task EditEnvelopeAsync(EditEnvelopeVm vm, int modifiedByUserId);

        Task<EnvelopeDetailsVm?> GetDetailsAsync(long envelopeId);
        Task<(IReadOnlyList<EnvelopeListItemVm> Rows, int Total)> SearchAsync(string? status, string? office, int page, int pageSize, int? createdByUserId = null, string? scope = null, int? userRoleId = null, int? userLocationId = null);

        // Public flow
        Task<ReviewVm?> GetReviewAsync(string token, string userAgent, string ip);
        Task<bool> DeclineEnvelopeAsync(string token, string reason, string userAgent, string ip); // Recipient declines to sign
        Task<(bool isComplete, string? downloadUrl)> TryFinalizeEnvelopeAsync(long envelopeId);

        // NEW (used by your POST Review action)
        Task UpsertTenantRecipientAsync(long envelopeId, string fullName, string email, string? phone = null);
        Task MarkTenantSkippedAsync(long envelopeId, string skippedByName, string source = "ManagerReview", int? skippedByUserId = null);
        Task CaptureSignatureAsync(long envelopeId, long recipientId, string typedFullName, string sigDataBase64);
        Task ProgressToNextAsync(long envelopeId);

        // Template access
        Task<SignTemplate?> GetTemplateByKeyAsync(string templateKey);

        /// <summary>
        /// Marks an envelope as complete using a staff-uploaded scanned PDF (offline signing).
        /// Any recipients who have not yet signed digitally are marked as signed with offline metadata.
        /// The uploaded PDF is stored in place of the auto-generated one and the envelope is set to Completed.
        /// </summary>
        Task MarkOfflineCompleteAsync(long envelopeId, Microsoft.AspNetCore.Http.IFormFile signedPdf, string? staffNote, int staffUserId, string staffName);

    }
}
