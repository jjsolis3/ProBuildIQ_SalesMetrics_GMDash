using System.Text.Json;

namespace SalesMetrics.Services
{
    public interface IAdobeSignClient
    {
        Task<string> CreateAgreementFromLibraryAsync(
            string libraryDocumentId,
            string managerEmail,
            string placeholderTenantEmail,
            string message);

        Task ReplaceRecipientAsync(string agreementId, int recipientOrder, string newEmail);

        Task<Stream> DownloadCombinedDocAsync(string agreementId);

        // NEW: polling helpers
        Task<string> GetAgreementStatusAsync(string agreementId);
        Task<JsonElement> GetAgreementEventsAsync(string agreementId);

        // ✅ Return strings, not objects
        Task<Dictionary<string, string>> GetAgreementFormDataAsync(string agreementId);
    }
}
