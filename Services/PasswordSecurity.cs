using System.Security.Cryptography;
using System.Text;

namespace SalesMetrics.Services
{
    public static class PasswordSecurity
    {
        // PBKDF2 iteration count — NIST SP 800-132 / OWASP 2023 recommendation for SHA-256
        private const int Pbkdf2Iterations = 600_000;
        private const int HashByteLength = 32; // 256 bits

        public static string GenerateSalt()
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16); // 128-bit salt
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// Hash a password using PBKDF2-HMAC-SHA256 (600 000 iterations).
        /// Replaces the previous SHA-256 single-pass hash which is brute-forceable.
        /// </summary>
        public static string HashPassword(string password, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password, saltBytes, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            return Convert.ToBase64String(pbkdf2.GetBytes(HashByteLength));
        }

        /// <summary>
        /// Verify a password against a stored PBKDF2 hash in constant time.
        /// </summary>
        public static bool VerifyPassword(string password, string salt, string storedHash)
        {
            var newHash = HashPassword(password, salt);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(newHash),
                Convert.FromBase64String(storedHash));
        }

        /// <summary>
        /// Legacy SHA-256 hash — kept ONLY for verifying existing user passwords during
        /// migration. After a user logs in successfully, re-hash with HashPassword() and
        /// update the stored hash. Remove this method once all users have migrated.
        /// </summary>
        [Obsolete("Legacy SHA-256 hash. Use HashPassword (PBKDF2) for new passwords.")]
        public static string HashPasswordLegacy(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(password + salt);
            var hash = sha256.ComputeHash(combined);
            return BitConverter.ToString(hash).Replace("-", "").ToUpper();
        }

        /// <summary>
        /// Returns true if the stored hash was produced by the legacy SHA-256 path.
        /// Use during login to detect accounts that still need migration to PBKDF2.
        /// </summary>
        public static bool IsLegacyHash(string storedHash)
        {
            // Legacy hashes are 64-char uppercase hex; PBKDF2 hashes are Base64.
            return storedHash.Length == 64 &&
                   storedHash.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));
        }
    }
}
