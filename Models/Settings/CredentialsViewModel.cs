using SalesMetrics.Services.Settings;

namespace SalesMetrics.Models.Settings;

public sealed class CredentialsViewModel
{
    public List<CredentialViewModel> GoogleCredentials { get; init; } = new();
    public List<CredentialViewModel> SmtpCredentials   { get; init; } = new();
}
