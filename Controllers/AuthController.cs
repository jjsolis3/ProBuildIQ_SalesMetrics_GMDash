using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SalesMetrics.Models;
using Microsoft.AspNetCore.Authorization;
using System;
using UAParser;
using System.Security.Cryptography;
using System.Text;
using SalesMetrics.Services;
using SalesMetrics.Services.Signing;
using SalesMetrics.Utilities.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;


namespace SalesMetrics.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IErrorLoggingService _errorLoggingService;
        private readonly IMemoryCache _cache;
        private readonly ISmtpSettingsProvider _smtpProvider;
        private readonly AppSettings _appSettings;

        // Lock out an IP after this many consecutive failures within the sliding window.
        private const int MaxLoginAttempts = 5;
        // Sliding window / lockout duration.
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
        // Password reset token validity window.
        private static readonly TimeSpan ResetTokenExpiry = TimeSpan.FromHours(1);

        public AuthController(
            IConfiguration configuration,
            IErrorLoggingService errorLoggingService,
            IMemoryCache cache,
            ISmtpSettingsProvider smtpProvider,
            IOptions<AppSettings> appSettings)
        {
            _configuration = configuration;
            _errorLoggingService = errorLoggingService;
            _cache = cache;
            _smtpProvider = smtpProvider;
            _appSettings = appSettings.Value;
        }

        // ─────────────────────────────────────────────
        // Rate-limiting helpers (in-memory, per IP)
        // ─────────────────────────────────────────────

        private bool IsRateLimited(string ip)
        {
            string key = $"login_attempts_{ip}";
            return _cache.TryGetValue(key, out int attempts) && attempts >= MaxLoginAttempts;
        }

        private void RecordFailedAttempt(string ip)
        {
            string key = $"login_attempts_{ip}";
            _cache.TryGetValue(key, out int count);
            _cache.Set(key, count + 1, new MemoryCacheEntryOptions
            {
                SlidingExpiration = LockoutDuration
            });
        }

        private void ResetFailedAttempts(string ip)
        {
            _cache.Remove($"login_attempts_{ip}");
        }

        // ─────────────────────────────────────────────
        // Email login
        // ─────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            // Rate-limit check
            var ip = GetUserIPAddress();
            if (IsRateLimited(ip))
            {
                ViewBag.Error = "Too many failed login attempts. Please try again in 15 minutes.";
                return View("Login");
            }

            // Validate inputs
            if (string.IsNullOrEmpty(email) || !email.Contains("@") || !email.Contains("."))
            {
                ViewBag.Error = "Please enter a valid email address.";
                return View("Login");
            }
            if (string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter a valid email and/or password.";
                return View("Login");
            }

            string fullName = string.Empty;
            string role = "Guest";
            string userName = string.Empty;
            int salesMetricsUserId = 0;
            int users_Id = 0;
            int roleId = 0;
            int salesmanId = 0;
            string? salesmanNumber = string.Empty;
            int locationId = 0;

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT
                    Users_ID,
                    UserID,
                    Username,
                    RoleID,
                    Location,
                    SalesmanID,
                    SalesmanNumber,
                    FirstName,
                    LastName,
                    Password,
                    PasswordHash,
                    Salt,
                    Email
                FROM Users
                WHERE Email = @Email and IsActive = 1
                ", conn);

            cmd.Parameters.AddWithValue("@Email", email.Trim().ToLower());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.Read())
            {
                // Generic message — does not reveal whether the email exists
                const string genericError = "Invalid email or password.";
                ViewBag.Error = "Access Denied: " + genericError;
                RecordFailedAttempt(ip);
                await LogLoginAttemptAsync(0, email, "UNK", false, "User not found or inactive");
                return View("Login");
            }

            salesMetricsUserId = Convert.ToInt32(reader["UserID"]);
            users_Id = Convert.ToInt32(reader["Users_ID"]);
            userName = reader["Username"]?.ToString() ?? string.Empty;
            roleId = Convert.ToInt32(reader["RoleID"]);
            role = GetUserRole(roleId);
            locationId = Convert.ToInt32(reader["Location"]);
            salesmanId = reader.IsDBNull(reader.GetOrdinal("SalesmanID")) ? 0 : Convert.ToInt32(reader["SalesmanID"]);
            salesmanNumber = reader.IsDBNull(reader.GetOrdinal("SalesmanNumber")) ? "" : reader["SalesmanNumber"].ToString();
            fullName = $"{reader["FirstName"]} {reader["LastName"]}";

            string officeLocation = locationId switch
            {
                1 => "LAX",
                2 => "LSV",
                3 => "CHN",
                4 => "PHX",
                5 => "SND",
                _ => "Unk"
            };

            try
            {
                string storedHash = reader["PasswordHash"]?.ToString();
                string storedSalt = reader["Salt"]?.ToString();

                if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
                {
                    const string missingHashMsg = "User is missing a hashed password or salt.";
                    ViewBag.Error = "Account configuration error. Please contact the IT Dept.";
                    RecordFailedAttempt(ip);
                    await LogLoginAttemptAsync(salesMetricsUserId, userName, officeLocation, false, missingHashMsg);
                    return View("Login");
                }

                string inputHash = PasswordSecurity.HashPassword(password, storedSalt);

                if (!string.Equals(inputHash, storedHash, StringComparison.OrdinalIgnoreCase))
                {
                    string legacyHash = PasswordSecurity.HashPasswordLegacy(password, storedSalt);

                    if (!string.Equals(legacyHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        // Generic message — does not reveal whether account exists
                        ViewBag.Error = "Invalid email or password. Please try again.";
                        RecordFailedAttempt(ip);
                        await LogLoginAttemptAsync(salesMetricsUserId, userName, officeLocation, false, "Invalid password");
                        return View("Login");
                    }
                    else
                    {
                        // Legacy hash matched: upgrade to Base64
                        var newSalt = PasswordSecurity.GenerateSalt();
                        var newHash = PasswordSecurity.HashPassword(password, newSalt);

                        using (var upgradeConn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
                        {
                            await upgradeConn.OpenAsync();
                            var upgradeCmd = new SqlCommand("UPDATE Users SET PasswordHash = @NewHash, Salt = @NewSalt, PasswordChangedDate = GetDate() WHERE UserID = @UserID AND Location = @LocationId", upgradeConn);
                            upgradeCmd.Parameters.AddWithValue("@NewHash", newHash);
                            upgradeCmd.Parameters.AddWithValue("@NewSalt", newSalt);
                            upgradeCmd.Parameters.AddWithValue("@UserID", salesMetricsUserId);
                            upgradeCmd.Parameters.AddWithValue("@LocationId", locationId);
                            await upgradeCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Password validation exception: {ex.Message}";
                ViewBag.Error = "An error occurred while signing in. Please try again later.";
                RecordFailedAttempt(ip);
                await LogLoginAttemptAsync(salesMetricsUserId, userName, officeLocation, false, errorMsg);
                return View("Login");
            }

            reader.Close();

            // Successful login — clear any accumulated failure count for this IP
            ResetFailedAttempts(ip);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Role, role),
                new Claim("FullName", fullName),
                new Claim(ClaimTypes.Email, email),
                new Claim("OfficeLocation", officeLocation),
                new Claim("UserId", salesMetricsUserId.ToString()),
                new Claim("Users_ID", users_Id.ToString()),
                new Claim("SalesmanId", salesmanId.ToString()),
                new Claim("RoleId", roleId.ToString()),
                new Claim("LocationId", locationId.ToString())
            };

            if (salesmanId > 0)
                claims.Add(new Claim("SalesmanNumber", salesmanNumber));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            HttpContext.Session.SetString("Users_ID", users_Id.ToString());
            HttpContext.Session.SetString("UserId", salesMetricsUserId.ToString());
            HttpContext.Session.SetString("FullName", fullName);
            HttpContext.Session.SetString("Username", userName);
            HttpContext.Session.SetString("OfficeLocation", officeLocation);
            HttpContext.Session.SetString("RoleId", roleId.ToString());
            HttpContext.Session.SetString("RoleName", role);
            HttpContext.Session.SetString("LocationId", locationId.ToString());

            if (salesmanId > 0)
            {
                HttpContext.Session.SetInt32("SalesmanId", salesmanId);
                HttpContext.Session.SetString("SalesmanNumber", salesmanNumber);
            }

            await LogLoginAttemptAsync(salesMetricsUserId, userName, officeLocation, true);

            using var updateCmd = new SqlCommand("UPDATE Users SET LastLoginDate = GETDATE() WHERE UserID = @UserID and Location = @locationId and Email = @Email", conn);
            updateCmd.Parameters.AddWithValue("@UserID", salesMetricsUserId);
            updateCmd.Parameters.AddWithValue("@locationId", locationId);
            updateCmd.Parameters.AddWithValue("@Email", email);
            await updateCmd.ExecuteNonQueryAsync();

            using (var conn2 = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn2.Open();
                var locationCmd = new SqlCommand(@"
                    SELECT LocationID
                    FROM UserLocationAssignments
                    WHERE UserID = @UserId AND IsActive = 'YES'
                ", conn2);
                locationCmd.Parameters.AddWithValue("@UserId", users_Id);

                var assignedLocations = new List<string>();
                using (var reader2 = locationCmd.ExecuteReader())
                {
                    while (reader2.Read())
                        assignedLocations.Add(reader2["LocationID"].ToString());
                }

                HttpContext.Session.SetString("AssignedLocations", string.Join(",", assignedLocations));
            }

            return RedirectToAppropriatePage(role);
        }

        // ─────────────────────────────────────────────
        // Username / office login
        // ─────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login_Alt()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login_Alt(string username, string password, string officeLocation)
        {
            // Rate-limit check
            var ip = GetUserIPAddress();
            if (IsRateLimited(ip))
            {
                ViewBag.Error = "Too many failed login attempts. Please try again in 15 minutes.";
                return View("Login_Alt");
            }

            if (string.IsNullOrEmpty(officeLocation))
            {
                ViewBag.Error = "Please select an office location!";
                return View("Login_Alt");
            }

            string connectionString = _configuration.GetConnectionString(officeLocation);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string for '{officeLocation}' is not found in appsettings.json.");
            }

            int locationId = officeLocation.ToUpper() switch
            {
                "LAX" => 1,
                "LSV" => 2,
                "CHN" => 3,
                "PHX" => 4,
                "SND" => 5,
                _ => 0
            };

            if (locationId == 0)
            {
                ViewBag.Error = "Invalid office location selected!";
                return View("Login_Alt");
            }

            string fullName = string.Empty;
            string role = "Guest";
            int salesMetricsUserId = 0;
            int users_Id = 0;
            int roleId = 0;
            int salesmanId = 0;
            string? salesmanNumber = string.Empty;

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT
                    Users_ID,
                    UserID,
                    RoleID,
                    Location,
                    SalesmanID,
                    SalesmanNumber,
                    FirstName,
                    LastName,
                    Password,
                    PasswordHash,
                    Salt
                FROM Users
                WHERE Username = @username and Location = @LocationId and IsActive = 1
                ", conn);

            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@LocationId", locationId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.Read())
            {
                // Generic message
                ViewBag.Error = "Invalid username or password.";
                RecordFailedAttempt(ip);
                await LogLoginAttemptAsync(0, username, officeLocation, false, "User not found or inactive");
                return View("Login_Alt");
            }

            salesMetricsUserId = Convert.ToInt32(reader["UserID"]);
            users_Id = Convert.ToInt32(reader["Users_ID"]);
            roleId = Convert.ToInt32(reader["RoleID"]);
            role = GetUserRole(roleId);
            locationId = Convert.ToInt32(reader["Location"]);
            salesmanId = reader.IsDBNull(reader.GetOrdinal("SalesmanID")) ? 0 : Convert.ToInt32(reader["SalesmanID"]);
            salesmanNumber = reader.IsDBNull(reader.GetOrdinal("SalesmanNumber")) ? "" : reader["SalesmanNumber"].ToString();
            fullName = $"{reader["FirstName"]} {reader["LastName"]}";

            try
            {
                string storedHash = reader["PasswordHash"]?.ToString();
                string storedSalt = reader["Salt"]?.ToString();

                if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
                {
                    const string missingHashMsg = "User is missing a hashed password or salt.";
                    ViewBag.Error = "Account configuration error. Please contact the IT Dept.";
                    RecordFailedAttempt(ip);
                    await LogLoginAttemptAsync(salesMetricsUserId, username, officeLocation, false, missingHashMsg);
                    return View("Login_Alt");
                }

                string inputHash = PasswordSecurity.HashPassword(password, storedSalt);

                if (!string.Equals(inputHash, storedHash, StringComparison.OrdinalIgnoreCase))
                {
                    string legacyHash = PasswordSecurity.HashPasswordLegacy(password, storedSalt);

                    if (!string.Equals(legacyHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        // Generic message
                        ViewBag.Error = "Invalid username or password. Please try again.";
                        RecordFailedAttempt(ip);
                        await LogLoginAttemptAsync(salesMetricsUserId, username, officeLocation, false, "Invalid password");
                        return View("Login_Alt");
                    }
                    else
                    {
                        // Legacy hash matched: upgrade to Base64
                        var newSalt = PasswordSecurity.GenerateSalt();
                        var newHash = PasswordSecurity.HashPassword(password, newSalt);

                        using (var upgradeConn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
                        {
                            await upgradeConn.OpenAsync();
                            var upgradeCmd = new SqlCommand("UPDATE Users SET PasswordHash = @NewHash, Salt = @NewSalt, PasswordChangedDate = GetDate() WHERE UserID = @UserID AND Location = @LocationId", upgradeConn);
                            upgradeCmd.Parameters.AddWithValue("@NewHash", newHash);
                            upgradeCmd.Parameters.AddWithValue("@NewSalt", newSalt);
                            upgradeCmd.Parameters.AddWithValue("@UserID", salesMetricsUserId);
                            upgradeCmd.Parameters.AddWithValue("@LocationId", locationId);
                            await upgradeCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Password validation exception: {ex.Message}";
                ViewBag.Error = "An error occurred while signing in. Please try again later.";
                RecordFailedAttempt(ip);
                await LogLoginAttemptAsync(salesMetricsUserId, username, officeLocation, false, errorMsg);
                return View("Login_Alt");
            }

            reader.Close();

            // Successful login — clear failure count
            ResetFailedAttempts(ip);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("FullName", fullName),
                new Claim("OfficeLocation", officeLocation),
                new Claim("UserId", salesMetricsUserId.ToString()),
                new Claim("Users_ID", users_Id.ToString()),
                new Claim("SalesmanId", salesmanId.ToString()),
                new Claim("RoleId", roleId.ToString()),
                new Claim("LocationId", locationId.ToString())
            };

            if (!string.IsNullOrEmpty(salesmanNumber))
                claims.Add(new Claim("SalesmanNumber", salesmanNumber));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            HttpContext.Session.SetString("Users_ID", users_Id.ToString());
            HttpContext.Session.SetString("UserId", salesMetricsUserId.ToString());
            HttpContext.Session.SetString("FullName", fullName);
            HttpContext.Session.SetString("Username", username);
            HttpContext.Session.SetString("OfficeLocation", officeLocation);
            HttpContext.Session.SetString("RoleId", roleId.ToString());
            HttpContext.Session.SetString("RoleName", role);
            HttpContext.Session.SetString("LocationId", locationId.ToString());

            if (salesmanId > 0)
                HttpContext.Session.SetInt32("SalesmanId", salesmanId);
            if (!string.IsNullOrEmpty(salesmanNumber))
                HttpContext.Session.SetString("SalesmanNumber", salesmanNumber);

            await LogLoginAttemptAsync(salesMetricsUserId, username, officeLocation, true);

            using var updateCmd = new SqlCommand("UPDATE Users SET LastLoginDate = GETDATE() WHERE UserID = @UserID and Location = @locationId", conn);
            updateCmd.Parameters.AddWithValue("@UserID", salesMetricsUserId);
            updateCmd.Parameters.AddWithValue("@locationId", locationId);
            await updateCmd.ExecuteNonQueryAsync();

            using (var conn2 = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn2.Open();
                var locationCmd = new SqlCommand(@"
                    SELECT LocationID
                    FROM UserLocationAssignments
                    WHERE UserID = @UserId AND IsActive = 'YES'
                ", conn2);
                locationCmd.Parameters.AddWithValue("@UserId", users_Id);

                var assignedLocations = new List<string>();
                using (var reader2 = locationCmd.ExecuteReader())
                {
                    while (reader2.Read())
                        assignedLocations.Add(reader2["LocationID"].ToString());
                }

                HttpContext.Session.SetString("AssignedLocations", string.Join(",", assignedLocations));
            }

            return RedirectToAppropriatePage(role);
        }

        // ─────────────────────────────────────────────
        // Forgot Password
        // ─────────────────────────────────────────────

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            // Always show the same success message to prevent account enumeration.
            ViewBag.Submitted = true;

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                return View();

            email = email.Trim().ToLower();

            try
            {
                string connStr = _configuration.GetConnectionString("SalesMetrics");

                // Look up user — only proceed if the account exists; otherwise silently drop.
                string? userEmail = null;
                string? fullName = null;

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(
                        "SELECT Email, FirstName + ' ' + LastName AS FullName FROM Users WHERE Email = @Email AND IsActive = 1",
                        conn);
                    cmd.Parameters.AddWithValue("@Email", email);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (reader.Read())
                    {
                        userEmail = reader["Email"]?.ToString();
                        fullName  = reader["FullName"]?.ToString();
                    }
                }

                if (string.IsNullOrEmpty(userEmail))
                    return View(); // User not found — silently return

                // Generate a secure URL-safe token and persist it
                string token = TokenHelper.CreateSecureToken(32);
                DateTime expiresAt = DateTime.UtcNow.Add(ResetTokenExpiry);

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // Invalidate any previous unused tokens for this email
                    var deleteOld = new SqlCommand(
                        "DELETE FROM PasswordResetTokens WHERE Email = @Email AND UsedAt IS NULL",
                        conn);
                    deleteOld.Parameters.AddWithValue("@Email", email);
                    await deleteOld.ExecuteNonQueryAsync();

                    var insertCmd = new SqlCommand(@"
                        INSERT INTO PasswordResetTokens (Token, Email, ExpiresAt)
                        VALUES (@Token, @Email, @ExpiresAt)", conn);
                    insertCmd.Parameters.AddWithValue("@Token", token);
                    insertCmd.Parameters.AddWithValue("@Email", email);
                    insertCmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                // Build the reset link
                string baseUrl = _appSettings?.BaseUrl?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";
                string resetUrl = $"{baseUrl}/Auth/ResetPassword?token={Uri.EscapeDataString(token)}";

                // Send the reset email via SMTP
                var smtp = await _smtpProvider.GetAsync();
                if (!string.IsNullOrWhiteSpace(smtp.Host) && !string.IsNullOrWhiteSpace(smtp.FromEmail))
                {
                    string subject = "SalesMetrics — Password Reset Request";
                    string body = $@"
                        <p>Hi {System.Net.WebUtility.HtmlEncode(fullName ?? email)},</p>
                        <p>We received a request to reset your SalesMetrics password.</p>
                        <p>
                            <a href=""{resetUrl}"" style=""background:#0d6efd;color:#fff;padding:10px 20px;border-radius:5px;text-decoration:none;"">
                                Reset My Password
                            </a>
                        </p>
                        <p>This link expires in <strong>1 hour</strong>.</p>
                        <p>If you did not request a password reset, please ignore this email or contact your IT Department.</p>
                        <hr/>
                        <small>ProBuildIQ SalesMetrics &mdash; password reset link</small>";

                    using var message = new MailMessage
                    {
                        From      = new MailAddress(smtp.FromEmail, smtp.FromName),
                        Subject   = subject,
                        Body      = body,
                        IsBodyHtml = true
                    };
                    message.To.Add(new MailAddress(email, fullName ?? email));

                    using var client = new SmtpClient(smtp.Host, smtp.Port)
                    {
                        EnableSsl             = smtp.EnableSsl,
                        UseDefaultCredentials = false,
                        Credentials           = new NetworkCredential(smtp.User, smtp.Pass)
                    };
                    await client.SendMailAsync(message);
                }
            }
            catch (Exception ex)
            {
                // Log but do not surface error to prevent enumeration
                await _errorLoggingService.LogWarningAsync(
                    $"ForgotPassword flow error for '{email}': {ex.Message}",
                    HttpContext, null, "Auth");
            }

            return View();
        }

        // ─────────────────────────────────────────────
        // Reset Password (token-based)
        // ─────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("ForgotPassword");

            // Validate token exists, is unused, and has not expired
            bool valid = await IsResetTokenValidAsync(token);
            if (!valid)
            {
                ViewBag.InvalidToken = true;
                return View();
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(token))
                return RedirectToAction("ForgotPassword");

            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                ViewBag.Error = "Password must be at least 8 characters.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }

            string connStr = _configuration.GetConnectionString("SalesMetrics");

            try
            {
                string? email = null;
                DateTime? expiresAt = null;

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    var lookupCmd = new SqlCommand(@"
                        SELECT Email, ExpiresAt FROM PasswordResetTokens
                        WHERE Token = @Token AND UsedAt IS NULL", conn);
                    lookupCmd.Parameters.AddWithValue("@Token", token);

                    using var reader = await lookupCmd.ExecuteReaderAsync();
                    if (reader.Read())
                    {
                        email     = reader["Email"]?.ToString();
                        expiresAt = Convert.ToDateTime(reader["ExpiresAt"]);
                    }
                }

                if (string.IsNullOrEmpty(email) || expiresAt == null || expiresAt < DateTime.UtcNow)
                {
                    ViewBag.InvalidToken = true;
                    return View();
                }

                // Hash the new password
                string salt    = PasswordSecurity.GenerateSalt();
                string newHash = PasswordSecurity.HashPassword(newPassword, salt);

                using (var conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();

                    // Update the user's password (all records sharing this email)
                    var updateCmd = new SqlCommand(@"
                        UPDATE Users
                        SET PasswordHash        = @Hash,
                            Salt                = @Salt,
                            PasswordChangedDate = GETDATE()
                        WHERE Email = @Email AND IsActive = 1", conn);
                    updateCmd.Parameters.AddWithValue("@Hash",  newHash);
                    updateCmd.Parameters.AddWithValue("@Salt",  salt);
                    updateCmd.Parameters.AddWithValue("@Email", email);
                    await updateCmd.ExecuteNonQueryAsync();

                    // Mark token as used
                    var markUsed = new SqlCommand(@"
                        UPDATE PasswordResetTokens SET UsedAt = GETDATE()
                        WHERE Token = @Token", conn);
                    markUsed.Parameters.AddWithValue("@Token", token);
                    await markUsed.ExecuteNonQueryAsync();
                }

                ViewBag.Success = true;
                return View();
            }
            catch (Exception ex)
            {
                await _errorLoggingService.LogWarningAsync(
                    $"ResetPassword error for token '{token}': {ex.Message}",
                    HttpContext, null, "Auth");

                ViewBag.Error = "An unexpected error occurred. Please try again or contact IT.";
                return View();
            }
        }

        private async Task<bool> IsResetTokenValidAsync(string token)
        {
            string connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT COUNT(1) FROM PasswordResetTokens
                WHERE Token = @Token AND UsedAt IS NULL AND ExpiresAt > GETUTCDATE()", conn);
            cmd.Parameters.AddWithValue("@Token", token);

            int count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        // ─────────────────────────────────────────────
        // Root redirect
        // ─────────────────────────────────────────────

        [HttpGet("/")]
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(userId))
                    return RedirectToAction("Index", "Dashboard");
            }

            return RedirectToAction("Login", "Auth");
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private string GetUserRole(int roleId)
        {
            string role = "Guest";
            string connectionString = _configuration.GetConnectionString("SalesMetrics");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT RoleName FROM Roles WHERE RoleID = @roleId", conn);
                cmd.Parameters.AddWithValue("@roleId", roleId);

                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    role = result.ToString()!;
            }
            return role;
        }

        /// <summary>
        /// Records a login attempt to LoginHistory and, on failure, also writes a Warning
        /// to the ErrorLog so it surfaces in the Error Dashboard.
        /// </summary>
        private async Task LogLoginAttemptAsync(int userID, string username, string branch, bool success, string errorMsg = null)
        {
            string connSR = _configuration.GetConnectionString("SalesMetrics");

            using (SqlConnection conn = new SqlConnection(connSR))
            {
                conn.Open();

                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                var parser = UAParser.Parser.GetDefault();
                var clientInfo = parser.Parse(userAgent);

                var browser = clientInfo.UA.Family + " " + clientInfo.UA.Major;
                var os = clientInfo.OS.Family + " " + clientInfo.OS.Major;
                var deviceInfo = $"{browser} on {os}";

                SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO [LoginHistory] (UserID, UserName, Success, IPAddress, office, DeviceInfo, UserAgentRaw, ErrorLog)
                    VALUES (@userID, @userName, @success, @ipAddress, @office, @deviceInfo, @userAgentRaw, @errorLog)", conn);

                cmd.Parameters.AddWithValue("@userID", userID);
                cmd.Parameters.AddWithValue("@userName", username);
                cmd.Parameters.AddWithValue("@success", success ? "Success" : "Failed");
                cmd.Parameters.AddWithValue("@ipAddress", GetUserIPAddress());
                cmd.Parameters.AddWithValue("@office", branch);
                cmd.Parameters.AddWithValue("@deviceInfo", deviceInfo ?? "Unknown");
                cmd.Parameters.AddWithValue("@userAgentRaw", Request.Headers["User-Agent"].ToString());
                cmd.Parameters.AddWithValue("@errorLog", success ? (object)DBNull.Value : errorMsg);

                cmd.ExecuteNonQuery();
            }

            // On failure, also write to the ErrorLog so it appears in the Error Dashboard
            if (!success)
            {
                var additionalData = $"{{\"username\":\"{username}\",\"office\":\"{branch}\",\"ip\":\"{GetUserIPAddress()}\"}}";
                await _errorLoggingService.LogWarningAsync(
                    $"Failed login attempt for '{username}' from {GetUserIPAddress()} [{branch}]: {errorMsg}",
                    HttpContext,
                    additionalData,
                    "Auth"
                );
            }
        }

        private string GetUserIPAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private IActionResult RedirectToAppropriatePage(string userRole)
        {
            switch (userRole)
            {
                case "ADMIN":
                    return RedirectToAction("Index", "Dashboard");
                case "PRESIDENT/OWNER":
                    return RedirectToAction("Index", "GMDash");
                case "REGIONAL MANAGER":
                    return RedirectToAction("Index", "GMDash");
                case "GENERAL MANAGER":
                    return RedirectToAction("Index", "GMDash");
                case "SALES ADMIN":
                    return RedirectToAction("Index", "Dashboard");
                case "SALES":
                    return RedirectToAction("Index", "Dashboard");
                case "OFFICE MANAGER":
                    return RedirectToAction("Index", "Office");
                case "OFFICE":
                    return RedirectToAction("Index", "Office");
                default:
                    return RedirectToAction("Index", "Home");
            }
        }

        // ─────────────────────────────────────────────
        // Logout
        // ─────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }

        [HttpGet]
        public async Task<IActionResult> LogoutSilent()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }

        // ─────────────────────────────────────────────
        // Access Denied
        // ─────────────────────────────────────────────

        [HttpGet("Auth/AccessDenied")]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            string? featureName = null;
            if (!string.IsNullOrEmpty(returnUrl))
            {
                var decoded = Uri.UnescapeDataString(returnUrl).Trim('/');
                var parts = decoded.Split('/');
                if (parts.Length >= 2)
                {
                    var controller = System.Text.RegularExpressions.Regex.Replace(parts[0], "([a-z])([A-Z])", "$1 $2");
                    var action = System.Text.RegularExpressions.Regex.Replace(parts[1], "([a-z])([A-Z])", "$1 $2");
                    featureName = $"{controller} - {action}";
                }
                else if (parts.Length == 1 && parts[0].Length > 0)
                {
                    featureName = System.Text.RegularExpressions.Regex.Replace(parts[0], "([a-z])([A-Z])", "$1 $2");
                }
            }

            ViewBag.FeatureName = featureName;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ─────────────────────────────────────────────
        // Google OAuth
        // ─────────────────────────────────────────────

        [Authorize]
        [HttpGet("auth/connect-google")]
        public IActionResult ConnectGoogle()
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback", "Auth"),
                Items =
                {
                    { "prompt", "consent" },
                    { "access_type", "offline" }
                }
            }, "Google");
        }

        [Authorize]
        [HttpGet("auth/google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("Google");
            if (!authenticateResult.Succeeded) return RedirectToAction("Profile");

            var googleEmail = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var accessToken = authenticateResult.Properties.GetTokenValue("access_token");
            var refreshToken = authenticateResult.Properties.GetTokenValue("refresh_token");

            var userId = HttpContext.Session.GetString("UserId");

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();
            var cmd = new SqlCommand(@"
                UPDATE Users
                SET GoogleEmail = @Email,
                    GoogleAccessToken = @AccessToken,
                    GoogleRefreshToken = @RefreshToken
                WHERE Users_ID = @Users_ID
            ", conn);

            cmd.Parameters.AddWithValue("@Email", googleEmail ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AccessToken", accessToken ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RefreshToken", refreshToken ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Users_ID", userId);

            await cmd.ExecuteNonQueryAsync();

            TempData["Success"] = "Google Calendar connected successfully!";
            return RedirectToAction("Profile", "Accounts");
        }

        [Authorize]
        [HttpPost("auth/disconnect-google")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisconnectGoogle()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Profile", "Accounts");

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
                await conn.OpenAsync();

                var readCmd = new SqlCommand(
                    "SELECT GoogleAccessToken FROM Users WHERE Users_ID = @Users_ID", conn);
                readCmd.Parameters.AddWithValue("@Users_ID", userId);
                var accessToken = (await readCmd.ExecuteScalarAsync())?.ToString();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    using var http = new System.Net.Http.HttpClient();
                    _ = http.PostAsync(
                        $"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(accessToken)}",
                        null);
                }

                var clearCmd = new SqlCommand(@"
                    UPDATE Users
                    SET GoogleEmail       = NULL,
                        GoogleAccessToken = NULL,
                        GoogleRefreshToken = NULL
                    WHERE Users_ID = @Users_ID
                ", conn);
                clearCmd.Parameters.AddWithValue("@Users_ID", userId);
                await clearCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Swallow — UI will still reflect disconnected state
            }

            TempData["Success"] = "Google account disconnected successfully.";
            return RedirectToAction("Profile", "Accounts");
        }

        // ─────────────────────────────────────────────
        // Register
        // ─────────────────────────────────────────────

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Please complete all required fields.";
                return View(model);
            }

            string salt = PasswordSecurity.GenerateSalt();
            string passwordHash = PasswordSecurity.HashPassword(model.Password, salt);

            string connStr = _configuration.GetConnectionString("SalesMetrics");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    INSERT INTO Users
                        (Username, Password, FirstName, LastName, Email, UserID, RoleID, Location, SalesmanID, SalesmanNumber, CreatedDate, IsActive, PasswordHash, Salt)
                    VALUES
                        (@Username, @Password, @FirstName, @LastName, @Email, @UserId, @RoleId, @LocationId, @SalesmanId, @SalesmanNumber, GETDATE(), 1, @PasswordHash, @Salt)
                ", conn);

                cmd.Parameters.AddWithValue("@Username", model.Username);
                cmd.Parameters.AddWithValue("@Password", model.Password);
                cmd.Parameters.AddWithValue("@FirstName", model.FirstName);
                cmd.Parameters.AddWithValue("@LastName", model.LastName);
                cmd.Parameters.AddWithValue("@Email", model.Email);
                cmd.Parameters.AddWithValue("@UserId", model.UserId);
                cmd.Parameters.AddWithValue("@RoleId", model.RoleId);
                cmd.Parameters.AddWithValue("@LocationId", model.LocationId);
                cmd.Parameters.AddWithValue("@SalesmanId", (object?)model.SalesmanId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SalesmanNumber", (object?)model.SalesmanNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@Salt", salt);

                cmd.ExecuteNonQuery();
            }

            TempData["LoginSuccess"] = "Account registered successfully. You may now log in.";
            return RedirectToAction("Login");
        }
    }
}
