using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

namespace SalesMetrics.Services.Settings;

/// <summary>
/// Reads and writes encrypted credentials from the [AppCredentials] table.
/// The AES key lives in appsettings.json under "Encryption:Key" — it is the only
/// secret that remains in that file.  All other secrets (Google OAuth, SMTP) are
/// stored encrypted in the database.
/// </summary>
public sealed class AppCredentialsProvider : IAppCredentialsProvider
{
    private readonly string _connStr;
    private readonly string _encKey;

    public AppCredentialsProvider(IConfiguration configuration)
    {
        _connStr = configuration.GetConnectionString("SalesMetrics")
                   ?? throw new InvalidOperationException("SalesMetrics connection string is not configured.");
        _encKey = configuration["Encryption:Key"]
                  ?? throw new InvalidOperationException(
                      "Encryption:Key is missing from configuration. " +
                      "Generate one with AesEncryption.GenerateKey() and add it to appsettings.json.");
    }

    public async Task<string?> GetDecryptedAsync(string key)
    {
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            "SELECT EncryptedValue FROM AppCredentials WHERE CredentialKey = @k", conn);
        cmd.Parameters.AddWithValue("@k", key);

        var raw = (await cmd.ExecuteScalarAsync())?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return AesEncryption.Decrypt(raw, _encKey);
        }
        catch (FormatException)
        {
            // Encryption key is not valid Base-64 (e.g. ENCRYPTION_KEY env var not set),
            // or the stored value was encrypted with a different key.
            // Return null so callers fall back to appsettings.json defaults.
            return null;
        }
        catch (CryptographicException)
        {
            // Wrong key (key mismatch after key rotation). Fall back to defaults.
            return null;
        }
    }

    public async Task UpsertAsync(string key, string plainValue, string category,
                                  string description, int modifiedByUserId)
    {
        var encrypted = AesEncryption.Encrypt(plainValue, _encKey);

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        // MERGE so we can call this for both insert and update
        const string sql = @"
            MERGE AppCredentials AS target
            USING (SELECT @k AS CredentialKey) AS source ON target.CredentialKey = source.CredentialKey
            WHEN MATCHED THEN
                UPDATE SET EncryptedValue       = @v,
                           Category             = @cat,
                           Description          = @desc,
                           LastModifiedDate     = GETDATE(),
                           LastModifiedByUserId = @uid
            WHEN NOT MATCHED THEN
                INSERT (CredentialKey, EncryptedValue, Category, Description, LastModifiedDate, LastModifiedByUserId)
                VALUES (@k, @v, @cat, @desc, GETDATE(), @uid);";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@k",    key);
        cmd.Parameters.AddWithValue("@v",    encrypted);
        cmd.Parameters.AddWithValue("@cat",  category);
        cmd.Parameters.AddWithValue("@desc", string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description);
        cmd.Parameters.AddWithValue("@uid",  modifiedByUserId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<CredentialViewModel>> GetAllByCategoryAsync(string category)
    {
        var list = new List<CredentialViewModel>();

        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(
            @"SELECT CredentialKey, Category, Description,
                     CASE WHEN LEN(EncryptedValue) > 0 THEN 1 ELSE 0 END AS HasValue
              FROM AppCredentials
              WHERE Category = @cat
              ORDER BY CredentialKey", conn);
        cmd.Parameters.AddWithValue("@cat", category);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CredentialViewModel
            {
                CredentialKey = reader.GetString(0),
                Category      = reader.GetString(1),
                Description   = reader.IsDBNull(2) ? "" : reader.GetString(2),
                HasValue      = reader.GetInt32(3) == 1,
            });
        }

        return list;
    }
}
