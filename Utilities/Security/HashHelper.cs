// Utilities/Security/HashHelper.cs
using System.Security.Cryptography;

namespace SalesMetrics.Utilities.Security;

public static class HashHelper
{
    public static byte[] Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }
}
