// Utilities/Security/TokenHelper.cs
using System.Security.Cryptography;

namespace SalesMetrics.Utilities.Security;

public static class TokenHelper
{
    public static string CreateSecureToken(int bytes = 32)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buffer)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
