namespace SalesMetrics.Services.Settings;

public interface IAppCredentialsProvider
{
    /// <summary>Returns the decrypted credential value, or null if the key has no DB row.</summary>
    Task<string?> GetDecryptedAsync(string key);

    /// <summary>
    /// Inserts or updates a credential row with the AES-encrypted value.
    /// </summary>
    Task UpsertAsync(string key, string plainValue, string category,
                     string description, int modifiedByUserId);

    /// <summary>
    /// Returns metadata for all credentials in the given category.
    /// The decrypted value is NEVER included in this list — only whether a value exists.
    /// </summary>
    Task<List<CredentialViewModel>> GetAllByCategoryAsync(string category);
}

public sealed class CredentialViewModel
{
    public string CredentialKey { get; init; } = "";
    public string Category      { get; init; } = "";
    public string Description   { get; init; } = "";
    /// <summary>True when the DB row exists and has a non-empty encrypted value.</summary>
    public bool   HasValue      { get; init; }
}
