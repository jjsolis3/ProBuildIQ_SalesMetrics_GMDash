using System.Security.Cryptography;
using System.Text;

namespace SalesMetrics.Services
{
    public static class PasswordSecurity
    {
        
        public static string GenerateSalt()
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16); // 128-bit salt
            return Convert.ToBase64String(saltBytes); // ⬅️ cleaner & more compatible for storage
        }

        public static string HashPassword(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(combined);
            return Convert.ToBase64String(hash);
        }

        public static string HashPasswordLegacy(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(combined);
            return BitConverter.ToString(hash).Replace("-", "").ToUpper(); // original legacy format
        }
    }

}
