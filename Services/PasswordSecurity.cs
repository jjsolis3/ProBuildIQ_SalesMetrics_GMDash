using System.Security.Cryptography;
using System.Text;

namespace SalesMetrics.Services
{
    public static class PasswordSecurity
    {
        public static string GenerateSalt()
        {
            var rng = new RNGCryptoServiceProvider();
            var buffer = new byte[16];
            rng.GetBytes(buffer);
            return BitConverter.ToString(buffer).Replace("-", "").ToUpper();
        }

        public static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(combined);
            return BitConverter.ToString(hash).Replace("-", "").ToUpper();
        }
    }

}
