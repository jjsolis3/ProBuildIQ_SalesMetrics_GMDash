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
using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using System.Numerics;
using Azure.Identity;


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
        public async Task<IActionResult> Login(string email, string password)
        {
            // Check if email is entered and entered correctly.  not missing an @ sign or .com, etc...
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
            string userName = string.Empty; // SalesMetrics Username
            int salesMetricsUserId = 0;
            int users_Id = 0; // SalesMetrics Users_ID
            int roleId = 0;
            int salesmanId = 0;
            string? salesmanNumber = string.Empty;
            int locationId = 0; // SalesMetrics LocationID

            // Assign Connection to SalesMetric DB
            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();

            // Authenticate user in SalesMetrics
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
                string errorMsg = "User not found or inactive!";
                ViewBag.Error = "Access Denied: User not found or Inactive!";
                LogLoginAttempt(salesMetricsUserId, email, "UNK", false, errorMsg);
                return View("Login");
            }

            // ✅ Move this up to make sure salesMetricsUserId is populated
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

                // Validate Hash
                if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
                {
                    string errorMsg = "User is missing a hashed password or salt.";
                    ViewBag.Error = errorMsg + " Please contact the IT Dept.";
                    LogLoginAttempt(salesMetricsUserId, userName, officeLocation, false, errorMsg);
                    return View("Login");
                }

                string inputHash = PasswordSecurity.HashPassword(password, storedSalt);

                if (!string.Equals(inputHash, storedHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Fallback: try legacy hex-based hash comparison
                    string legacyHash = PasswordSecurity.HashPasswordLegacy(password, storedSalt);
                    

                    if (!string.Equals(legacyHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        string errorMsg = "Invalid Password!";
                        ViewBag.Error = errorMsg + " Please try again.";
                        LogLoginAttempt(salesMetricsUserId, userName, officeLocation, false, errorMsg);
                        return View("Login");
                    }
                    else
                    {
                        // ✅ Legacy hash matched: upgrade to Base64
                        var newSalt = PasswordSecurity.GenerateSalt();
                        var newHash = PasswordSecurity.HashPassword(password, newSalt); // ✅ rehash using the new salt


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
                // Log the exception (optional)
                string errorMsg = "An error occurred while validating the password. ("+ex+")";
                ViewBag.Error = errorMsg + " Please try again later.";
                LogLoginAttempt(salesMetricsUserId, userName, officeLocation, false, errorMsg);
                return View("Login");
            }               

            // ✅ Close the reader before reusing the connection
            reader.Close();

            // Set Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Role, role),
                new Claim("FullName", fullName),
                new Claim(ClaimTypes.Email, email),
                new Claim("OfficeLocation", officeLocation),
                new Claim("UserId", salesMetricsUserId.ToString()),
                new Claim("Users_ID", users_Id.ToString()), // SalesMetrics Users_ID
                new Claim("SalesmanId", salesmanId.ToString()), 
                new Claim("RoleId", roleId.ToString()),
                new Claim("LocationId", locationId.ToString())
            };

            if (salesmanId > 0)
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

            LogLoginAttempt(salesMetricsUserId, userName, officeLocation, true);

            // UPDATE LastLoginDate in Users table
            using var updateCmd = new SqlCommand("UPDATE Users SET LastLoginDate = GETDATE() WHERE UserID = @UserID and Location = @locationId and Email = @Email", conn);
            updateCmd.Parameters.AddWithValue("@UserID", salesMetricsUserId);
            updateCmd.Parameters.AddWithValue("@locationId", locationId);
            updateCmd.Parameters.AddWithValue("@Email", email);
            await updateCmd.ExecuteNonQueryAsync();

            // After setting all session variables
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
                    {
                        assignedLocations.Add(reader2["LocationID"].ToString());
                    }
                }

                HttpContext.Session.SetString("AssignedLocations", string.Join(",", assignedLocations));
            }

            return RedirectToAppropriatePage(role);
        }

        [HttpGet]
        public IActionResult Login_Alt()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Login_Alt(string username, string password, string officeLocation)
        {
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
            int users_Id = 0; // SalesMetrics Users_ID
            int roleId = 0;
            int salesmanId = 0;
            string? salesmanNumber = string.Empty;

            // Assign Connection to SalesMetric DB
            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();

            // Authenticate user in SalesMetrics
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
                string errorMsg = "User not found or inactive!";
                ViewBag.Error = "Access Denied: User not found or Inactive!";
                LogLoginAttempt(salesMetricsUserId, username, officeLocation, false, errorMsg);
                return View("Login_Alt");
            }

            // ✅ Move this up to make sure salesMetricsUserId is populated
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

                // Validate Hash
                if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
                {
                    string errorMsg = "User is missing a hashed password or salt.";
                    ViewBag.Error = errorMsg + " Please contact the IT Dept.";
                    LogLoginAttempt(salesMetricsUserId, username, officeLocation, false, errorMsg);
                    return View("Login");
                }

                string inputHash = PasswordSecurity.HashPassword(password, storedSalt);

                if (!string.Equals(inputHash, storedHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Fallback: try legacy hex-based hash comparison
                    string legacyHash = PasswordSecurity.HashPasswordLegacy(password, storedSalt);


                    if (!string.Equals(legacyHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        string errorMsg = "Invalid Password!";
                        ViewBag.Error = errorMsg + " Please try again.";
                        LogLoginAttempt(salesMetricsUserId, username, officeLocation, false, errorMsg);
                    }
                    else
                    {
                        // ✅ Legacy hash matched: upgrade to Base64
                        var newSalt = PasswordSecurity.GenerateSalt();
                        var newHash = PasswordSecurity.HashPassword(password, newSalt); // ✅ rehash using the new salt


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
                // Log the exception (optional)
                string errorMsg = "An error occurred while validating the password.";
                ViewBag.Error = errorMsg + " Please try again later.";
                LogLoginAttempt(salesMetricsUserId, username, officeLocation, false, errorMsg);
                return View("Login_Alt");
            }

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
                new Claim("Users_ID", users_Id.ToString()), // SalesMetrics Users_ID
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
            HttpContext.Session.SetString("Users_ID", users_Id.ToString());
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
            using var updateCmd = new SqlCommand("UPDATE Users SET LastLoginDate = GETDATE() WHERE UserID = @UserID and Location = @locationId", conn);
            updateCmd.Parameters.AddWithValue("@UserID", salesMetricsUserId);
            updateCmd.Parameters.AddWithValue("@locationId", locationId);
            await updateCmd.ExecuteNonQueryAsync();

            // After setting all session variables
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
                    {
                        assignedLocations.Add(reader2["LocationID"].ToString());
                    }
                }

                HttpContext.Session.SetString("AssignedLocations", string.Join(",", assignedLocations));
            }

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

        private void LogLoginAttempt(int userID, string username, string branch, bool success, string errorMsg = null)
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
                    INSERT INTO [LoginHistory] (UserID, UserName, Success, IPAddress, office, DeviceInfo, UserAgentRaw, ErrorLog)
                    VALUES (@userID, @userName, @success, @ipAddress, @office, @deviceInfo, @userAgentRaw, @errorLog)", conn);

                cmd.Parameters.AddWithValue("@userID", userID);
                cmd.Parameters.AddWithValue("@userName", username);
                cmd.Parameters.AddWithValue("@success", success ? "Success" : "Failed");
                cmd.Parameters.AddWithValue("@ipAddress", GetUserIPAddress());
                cmd.Parameters.AddWithValue("@office", branch);
                cmd.Parameters.AddWithValue("@deviceInfo", deviceInfo ?? "Unknown");
                cmd.Parameters.AddWithValue("@userAgentRaw", Request.Headers["User-Agent"].ToString());
                cmd.Parameters.AddWithValue("@errorLog", success == true ? DBNull.Value : errorMsg);

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

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Clear session
            HttpContext.Session.Clear();
            // Sign out from cookie authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // Redirect to login
            return RedirectToAction("Login", "Auth");
        }


        [HttpGet]
        public async Task<IActionResult> LogoutSilent()
        {
            // Clear session
            HttpContext.Session.Clear();
            // Sign out from cookie authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // Redirect to login
            return RedirectToAction("Login", "Auth");
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

        // POST: /auth/disconnect-google
        // Revokes the stored Google tokens and clears them from the database.
        [Authorize]
        [HttpPost("auth/disconnect-google")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisconnectGoogle()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Profile", "Accounts");

            // Try to revoke the access token at Google's endpoint (best-effort — don't block on failure)
            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
                await conn.OpenAsync();

                // Read current access token so we can revoke it
                var readCmd = new SqlCommand(
                    "SELECT GoogleAccessToken FROM Users WHERE UserID = @UserID", conn);
                readCmd.Parameters.AddWithValue("@UserID", userId);
                var accessToken = (await readCmd.ExecuteScalarAsync())?.ToString();

                if (!string.IsNullOrEmpty(accessToken))
                {
                    using var http = new System.Net.Http.HttpClient();
                    // Google revocation endpoint — fire-and-forget is fine; we clear the DB regardless
                    _ = http.PostAsync(
                        $"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(accessToken)}",
                        null);
                }

                // Clear the tokens from the database
                var clearCmd = new SqlCommand(@"
                    UPDATE Users
                    SET GoogleEmail       = NULL,
                        GoogleAccessToken = NULL,
                        GoogleRefreshToken = NULL
                    WHERE UserID = @UserID
                ", conn);
                clearCmd.Parameters.AddWithValue("@UserID", userId);
                await clearCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Swallow — the UI will still reflect disconnected state
            }

            TempData["Success"] = "Google account disconnected successfully.";
            return RedirectToAction("Profile", "Accounts");
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
