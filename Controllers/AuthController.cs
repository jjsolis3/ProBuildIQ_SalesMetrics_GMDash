using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SalesMetrics.Models;
using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Authorization;
using System;
using UAParser;
using System.Security.Cryptography;
using System.Text;
using SalesMetrics.Services;


namespace SalesMetrics.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string officeLocation)
        {
            if (string.IsNullOrEmpty(officeLocation))
            {
                ViewBag.Error = "Please select an office location!";
                return View("Login");
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
                return View("Login");
            }

            string fullName = string.Empty;
            string role = "Guest";
            int salesMetricsUserId = 0;
            int roleId = 0;
            int salesmanId = 0;
            string? salesmanNumber = string.Empty;

            // Assign Connection to SalesMetric DB
            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();

            // Authenticate user in SalesMetrics
            var cmd = new SqlCommand(@"
                SELECT 
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
                WHERE Username = @username and IsActive = 1
                ", conn);                

            cmd.Parameters.AddWithValue("@username", username);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!reader.Read())
            {
                ViewBag.Error = "Access Denied: User not found or Inactive!";
                return View("Login");
            }

            //string storedPassword = reader["Password"]?.ToString() ?? "";
            //if (storedPassword != password)

            string storedHash = reader["PasswordHash"]?.ToString();
            string storedSalt = reader["Salt"]?.ToString();

            // Validate Hash
            if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            {
                ViewBag.Error = "This user is missing a hashed password.  Please contact the IT Dept.";
                return View("Login");
            }

            string inputHash = PasswordSecurity.HashPassword(password, storedSalt);

            if (!string.Equals(inputHash, storedHash, StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Invalid Password!";
                return View("Login");
            }

            // Extract User Info
            salesMetricsUserId = Convert.ToInt32(reader["UserID"]);
            roleId = Convert.ToInt32(reader["roleId"]);
            role = GetUserRole(roleId);
            locationId = Convert.ToInt32(reader["Location"]);
            salesmanId = reader.IsDBNull(reader.GetOrdinal("SalesmanID")) ? 0 : Convert.ToInt32(reader["SalesmanID"]);
            salesmanNumber = reader.IsDBNull(reader.GetOrdinal("SalesmanNumber")) ? "" : reader["SalesmanNumber"].ToString();
            fullName = $"{reader["FirstName"]} {reader["LastName"]}";

            // ✅ Close the reader before reusing the connection
            reader.Close();

            // Set Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("FullName", fullName),
                new Claim("OfficeLocation", officeLocation),
                new Claim("UserId", salesMetricsUserId.ToString()),
                new Claim("SalesmanId", salesmanId.ToString()), 
                new Claim("RoleId", roleId.ToString()),
                new Claim("LocationId", locationId.ToString())
            };

            if (!string.IsNullOrEmpty(salesmanNumber))
            {
                claims.Add(new Claim("SalesmanNumber", salesmanNumber));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // Save to Session
            HttpContext.Session.SetString("UserId", salesMetricsUserId.ToString());
            HttpContext.Session.SetString("FullName", fullName);
            HttpContext.Session.SetString("Username", username);
            HttpContext.Session.SetString("OfficeLocation", officeLocation);
            HttpContext.Session.SetString("RoleId", roleId.ToString());
            HttpContext.Session.SetString("RoleName", role);
            HttpContext.Session.SetString("LocationId", locationId.ToString());

            if (salesmanId > 0)
            {
                HttpContext.Session.SetInt32("SalesmanId", salesmanId);
            }
            if (!string.IsNullOrEmpty(salesmanNumber))
            {
                HttpContext.Session.SetString("SalesmanNumber", salesmanNumber);
            }

            LogLoginAttempt(salesMetricsUserId, username, officeLocation, true);

            // UPDATE LastLoginDate in Users table
            using var updateCmd = new SqlCommand("UPDATE Users SET LastLoginDate = GETDATE() WHERE UserID = @UserID", conn);
            updateCmd.Parameters.AddWithValue("@UserID", salesMetricsUserId);
            await updateCmd.ExecuteNonQueryAsync();

            return RedirectToAppropriatePage(role);
        }

        [HttpGet("/")]
        public IActionResult Index()
        {
            // Check if the user is authenticated
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = HttpContext.Session.GetString("UserId");
                int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");

                // If session is active
                if (!string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Index", "Dashboard"); // Redirect to dashboard   
                }
            }

            return RedirectToAction("Login", "Auth"); // Show login page
        }

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
                {
                    role = result.ToString()!;
                }
            }
            return role;
        }

        private void LogLoginAttempt(int userID, string username, string branch, bool success)
        {
            string connSR = _configuration.GetConnectionString("SalesMetrics");

            using (SqlConnection conn = new SqlConnection(connSR))
            {
                conn.Open();

                // Get User-Agent
                //var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();

                // Parse User-Agent using UAParser
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                var parser = UAParser.Parser.GetDefault();
                var clientInfo = parser.Parse(userAgent);

                // Extract readable components
                var browser = clientInfo.UA.Family + " " + clientInfo.UA.Major;
                var os = clientInfo.OS.Family + " " + clientInfo.OS.Major;
                var deviceInfo = $"{browser} on {os}";

                SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO [LoginHistory] (UserID, UserName, Success, IPAddress, office, DeviceInfo, UserAgentRaw)
                    VALUES (@userID, @userName, @success, @ipAddress, @office, @deviceInfo, @userAgentRaw)", conn);

                cmd.Parameters.AddWithValue("@userID", userID);
                cmd.Parameters.AddWithValue("@userName", username);
                cmd.Parameters.AddWithValue("@success", success ? "Success" : "Failed");
                cmd.Parameters.AddWithValue("@ipAddress", GetUserIPAddress());
                cmd.Parameters.AddWithValue("@office", branch);
                cmd.Parameters.AddWithValue("@deviceInfo", deviceInfo ?? "Unknown");
                cmd.Parameters.AddWithValue("@userAgentRaw", Request.Headers["User-Agent"].ToString());

                cmd.ExecuteNonQuery();
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
                case "SALES ADMIN":
                    return RedirectToAction("Index", "Dashboard");
                case "SALES":
                    return RedirectToAction("Index", "Dashboard");
                case "GENERAL MANAGER":
                    return RedirectToAction("Index", "GM");
                default:
                    return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // Clear session
            HttpContext.Session.Clear();

            // Sign out from cookie authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Redirect to login
            return RedirectToAction("Login");
        }
               

        // GOOGLE AUTHENTICATION
        [Authorize]
        [HttpGet("auth/connect-google")]
        public IActionResult ConnectGoogle()
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback", "Auth"),
                Items =
        {
            { "prompt", "consent" },           // ← Forces user to re-consent
            { "access_type", "offline" }       // ← Ensures refresh token is returned
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

            // Save to DB
            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();
            var cmd = new SqlCommand(@"
                UPDATE Users
                SET GoogleEmail = @Email,
                    GoogleAccessToken = @AccessToken,
                    GoogleRefreshToken = @RefreshToken
                WHERE UserID = @UserID
            ", conn);

            cmd.Parameters.AddWithValue("@Email", googleEmail ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AccessToken", accessToken ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RefreshToken", refreshToken ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@UserID", userId);

            await cmd.ExecuteNonQueryAsync();

            TempData["Success"] = "Google Calendar connected successfully!";
            return RedirectToAction("User", "Accounts");
        }

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

            // ✅ Generate salt and hash password
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
