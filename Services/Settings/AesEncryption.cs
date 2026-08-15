using System.Security.Cryptography;
using System.Text;

namespace SalesMetrics.Services.Settings;

/// <summary>
/// AES-256-CBC encryption/decryption helper.
/// The key is a 32-byte value stored as Base64 in appsettings.json under "Encryption:Key".
/// A random 16-byte IV is prepended to each ciphertext so every Encrypt call produces
/// a different output even for identical plaintext.
/// </summary>
public static class AesEncryption
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with the supplied Base64-encoded 32-byte key.
    /// Returns a Base64 string of the format [16-byte IV][ciphertext].
    /// </summary>
    public static string Encrypt(string plaintext, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV so Decrypt can extract it without separate storage
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a Base64 ciphertext produced by <see cref="Encrypt"/>.
    /// Returns the original plaintext string.
    /// </summary>
    public static string Decrypt(string base64Cipher, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        var data = Convert.FromBase64String(base64Cipher);

        using var aes = Aes.Create();
        aes.Key = key;

        // First 16 bytes are the IV
        var iv = new byte[16];
        var cipherBytes = new byte[data.Length - 16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        Buffer.BlockCopy(data, 16, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Generates a new random 32-byte AES key and returns it as a Base64 string.
    /// Use this once to produce the value for "Encryption:Key" in appsettings.json.
    /// </summary>
    public static string GenerateKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}
