using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using SalesMetrics.Services;
using SalesMetrics.Services.Helpers;
using SalesMetrics.Services.Permissions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SalesMetrics.Controllers
{
    public class AccountsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IPermissionService _permissionService;
        private readonly SalesRepMetricsService _metricsService;

        public AccountsController(IConfiguration configuration, IPermissionService permissionService,
            SalesRepMetricsService metricsService)
        {
            _configuration = configuration;
            _permissionService = permissionService;
            _metricsService = metricsService;
        }

        // ACCOUNTS PAGE
        [HttpGet]
        public IActionResult Index()
        {
            var usersWithAssignments = new List<UserWithAssignmentsViewModel>();
            string connStr = _configuration.GetConnectionString("SalesMetrics");

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Fetch users with role and location names
                var cmd = new SqlCommand(@"
                    SELECT
                        u.Users_ID, u.FirstName, u.LastName, u.Username, u.Email, u.UserID,
                        u.RoleID, r.RoleName,
                        u.SalesmanID, u.SalesmanNumber,
                        u.Location,
                        CASE u.Location
                            WHEN 1 THEN 'LAX'
                            WHEN 2 THEN 'LSV'
                            WHEN 3 THEN 'CHN'
                            WHEN 4 THEN 'PHX'
                            WHEN 5 THEN 'SND'
                            ELSE 'Unknown'
                        END as LocationName,
                        u.CreatedDate, u.IsActive, u.LastLoginDate
                    FROM Users u
                    LEFT JOIN Roles r ON u.RoleID = r.RoleID
                    ORDER BY u.Users_ID
                ", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var user = new UserWithAssignmentsViewModel
                    {
                        Users_ID = Convert.ToInt32(reader["Users_ID"]),
                        FirstName = reader["FirstName"]?.ToString() ?? "",
                        LastName = reader["LastName"]?.ToString() ?? "",
                        Username = reader["Username"]?.ToString() ?? "",
                        Email = reader["Email"]?.ToString() ?? "",
                        UserId = Convert.ToInt32(reader["UserID"]),
                        RoleId = Convert.ToInt32(reader["RoleID"]),
                        RoleName = reader["RoleName"]?.ToString() ?? "",
                        SalesmanId = reader["SalesmanID"] != DBNull.Value ? Convert.ToInt32(reader["SalesmanID"]) : 0,
                        SalesmanNumber = reader["SalesmanNumber"]?.ToString() ?? "",
                        Location = Convert.ToInt32(reader["Location"]),
                        LocationName = reader["LocationName"]?.ToString() ?? "",
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                        IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                        LastLoginDate = reader["LastLoginDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastLoginDate"]) : null
                    };
                    usersWithAssignments.Add(user);
                }
                reader.Close();

                // Fetch assigned locations for each user
                foreach (var user in usersWithAssignments)
                {
                    var assignedLocations = new List<string>();
                    var locCmd = new SqlCommand(@"
                        SELECT LocationID
                        FROM UserLocationAssignments
                        WHERE UserID = @UserId AND IsActive = 'YES'
                        ORDER BY LocationID
                    ", conn);
                    locCmd.Parameters.AddWithValue("@UserId", user.Users_ID);

                    using var locReader = locCmd.ExecuteReader();
                    while (locReader.Read())
                    {
                        int locId = Convert.ToInt32(locReader["LocationID"]);
                        string locName = locId switch
                        {
                            1 => "LAX",
                            2 => "LSV",
                            3 => "CHN",
                            4 => "PHX",
                            5 => "SND",
                            _ => "?"
                        };
                        assignedLocations.Add(locName);
                    }

                    user.AssignedLocations = assignedLocations;
                }
            }

            ViewBag.FlaggedUsers = GetFlaggedUsers();
            return View(usersWithAssignments);
        }

        // USER PROFILE PAGE
        public IActionResult Profile(DateTime? startDate, DateTime? endDate)
        {
            var userId = HttpContext.Session.GetString("UserId");
            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            var parsedStartDate = startDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
            var parsedEndDate = endDate ?? DateTime.UtcNow;

            var userProfile = new UserProfileViewModel();

            // -- Fetch User Profile Info --
            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT FirstName, LastName, Email, RoleID, GoogleEmail, GoogleAccessToken, GoogleRefreshToken,
                       SalesmanID, SalesmanNumber, Location
                FROM Users
                WHERE UserID = @UserID AND Location = @LocationId
            ", conn);
            cmd.Parameters.AddWithValue("@UserID", userId);
            cmd.Parameters.AddWithValue("@LocationId", locationId);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    userProfile.FirstName = reader["FirstName"]?.ToString();
                    userProfile.LastName = reader["LastName"]?.ToString();
                    userProfile.FullName = reader["FirstName"] + " " + reader["LastName"];
                    userProfile.Email = reader["Email"]?.ToString();
                    userProfile.RoleId = Convert.ToInt32(reader["RoleID"]);
                    userProfile.GoogleEmail = reader["GoogleEmail"]?.ToString();
                    userProfile.GoogleAccessToken = reader["GoogleAccessToken"]?.ToString();
                    userProfile.GoogleRefreshToken = reader["GoogleRefreshToken"]?.ToString();
                    userProfile.SalesmanID = reader["SalesmanID"] != DBNull.Value ? Convert.ToInt32(reader["SalesmanID"]) : 0;
                    userProfile.SalesmanNumber = reader["SalesmanNumber"]?.ToString();
                    userProfile.LocationId = reader["Location"] != DBNull.Value ? Convert.ToInt32(reader["Location"]) : 0;
                }
            }

            // -- Fetch Sales Rep Metrics (only for Sales Rep role 2, and only when SalesmanID is available) --
            SalesRepMetricsViewModel metrics;
            if (userProfile.RoleId == 2 && userProfile.SalesmanID > 0)
            {
                metrics = _metricsService.GetSalesMetrics(
                    userProfile.SalesmanID, locationId, parsedStartDate, parsedEndDate);
            }
            else
            {
                metrics = new SalesRepMetricsViewModel
                {
                    PropertyDetails = new List<SalesRepNewAccountSummaryViewModel>()
                };
            }

            // -- Return Composite Model --
            return View(new SalesRepProfilePageViewModel
            {
                UserProfile = userProfile,
                Metrics = metrics,
                StartDate = parsedStartDate,
                EndDate = parsedEndDate,
            });
        }

        // KANBAN PAGE
        public IActionResult Kanban()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> TestGoogleTask()
        {
            var users_Id = Convert.ToInt32(HttpContext.Session.GetString("Users_Id"));

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();
            var cmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE Users_ID = @Users_ID", conn);
            cmd.Parameters.AddWithValue("@Users_ID", users_Id);

            string? accessToken = null;
            string? refreshToken = null;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    accessToken = reader["GoogleAccessToken"]?.ToString();
                    refreshToken = reader["GoogleRefreshToken"]?.ToString();
                }
            }

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                TempData["Error"] = "Google is not connected for this user.";
                return RedirectToAction("User");
            }

            // 👇 Call the service
            var taskService = new GoogleTasksService(_configuration);
            //var result = await taskService.CreateTaskAsync(accessToken, "SalesMetrics Test Task", "This is a test task created from SalesMetrics", "Notes", DateTime.UtcNow.AddHours(1));
            var result = await taskService.CreateTaskAsync(accessToken, refreshToken, users_Id.ToString(),"**TestTaskID**", "SalesMetrics Test Task", "This is a test task created from SalesMetrics", DateTime.UtcNow.AddHours(1));

            if (!string.IsNullOrEmpty(result))
                TempData["Success"] = $"Google Task created successfully! Task ID: {result}";
            else
                TempData["Error"] = "Failed to create Google Task.";

            return RedirectToAction("Profile");
        }

        [HttpPost]
        [Authorize] // Only admins (RoleID 1) can trigger this
        public IActionResult RehashAllUserPasswords()
        {
            int roleId = int.Parse(HttpContext.User.FindFirst("RoleId")?.Value ?? "0");

            if (roleId != 1)
            {
                //return Unauthorized();
                return RedirectToAction("Error404", "Pages");
            }

            string connStr = _configuration.GetConnectionString("SalesMetrics");

            var usersToFix = new List<(int Users_Id, string Password)>();

            // Step 1: Get all users with non-null passwords
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT Users_ID, Password 
                    FROM Users 
                    WHERE Password IS NOT NULL
                        AND PasswordHash is NULL
                        AND Salt IS NULL
                ", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    usersToFix.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }

            // Step 2: Update each user's PasswordHash and Salt
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                foreach (var user in usersToFix)
                {
                    string salt = PasswordSecurity.GenerateSalt();
                    string hash = PasswordSecurity.HashPassword(user.Password, salt);

                    var updateCmd = new SqlCommand(@"
                    UPDATE Users
                    SET PasswordHash = @Hash,
                        Salt = @Salt,
                        PasswordChangedDate = GETDATE()
                    WHERE Users_ID = @Users_ID", conn);

                    updateCmd.Parameters.AddWithValue("@Hash", hash);
                    updateCmd.Parameters.AddWithValue("@Salt", salt);
                    updateCmd.Parameters.AddWithValue("@UserID", user.Users_Id);

                    updateCmd.ExecuteNonQuery();
                }
            }

            TempData["Success"] = $"✅ Rehashed {usersToFix.Count} user passwords successfully.";
            return RedirectToAction("Index"); // Adjust if your user page action is named differently
        }

        [HttpGet]
        public async Task<IActionResult> GetEditUserModal(int id)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");
            RegisterViewModel user = new();

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM Users WHERE Users_ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    user = new RegisterViewModel
                    {
                        Users_Id = Convert.ToInt32(reader["Users_ID"]),
                        UserId = Convert.ToInt32(reader["UserID"]),
                        FirstName = reader["FirstName"]?.ToString(),
                        LastName = reader["LastName"]?.ToString(),
                        Username = reader["Username"]?.ToString(),
                        Email = reader["Email"]?.ToString(),
                        RoleId = Convert.ToInt32(reader["RoleID"]),
                        LocationId = Convert.ToInt32(reader["Location"]),
                        SalesmanId = reader["SalesmanID"] != DBNull.Value ? Convert.ToInt32(reader["SalesmanID"]) : null,
                        SalesmanNumber = reader["SalesmanNumber"]?.ToString()
                    };
                    ViewBag.IsActive = Convert.ToBoolean(reader["IsActive"]);
                }
                reader.Close();

                // Fetch assigned locations
                var assignedLocations = new List<int>();
                var locationCmd = new SqlCommand(@"
                    SELECT LocationID
                    FROM UserLocationAssignments
                    WHERE UserID = @UserId AND IsActive = 'YES'
                ", conn);
                locationCmd.Parameters.AddWithValue("@UserId", user.Users_Id);
                using (var locReader = locationCmd.ExecuteReader())
                {
                    while (locReader.Read())
                        assignedLocations.Add(Convert.ToInt32(locReader["LocationID"]));
                }
                user.AssignedLocationIds = assignedLocations;

                // Populate all possible locations for checkboxes
                user.AllLocations = new List<SelectListItem>
                {
                    new("LAX", "1"),
                    new("LSV", "2"),
                    new("CHN", "3"),
                    new("PHX", "4"),
                    new("SND", "5")
                };
            }

            // Load all available features for permission checkboxes
            var allFeatures = await _permissionService.GetAllFeaturesAsync();
            user.AllFeatures = allFeatures.Select(f => new SelectListItem
            {
                Text = f.FeatureName,
                Value = f.FeatureId.ToString()
            }).ToList();

            // Group features by category for organized display
            user.GroupedFeatures = allFeatures
                .GroupBy(f => f.Category ?? "Other")
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(f => f.DisplayOrder)
                          .Select(f => new FeatureItemViewModel
                          {
                              FeatureId = f.FeatureId,
                              FeatureName = f.FeatureName,
                              Category = f.Category,
                              DisplayOrder = f.DisplayOrder
                          }).ToList()
                );

            // Load user's current permissions (using Users_Id primary key)
            var userPermissionIds = await _permissionService.GetUserPermissionIdsAsync(user.Users_Id);
            user.AssignedFeatureIds = userPermissionIds;

            return PartialView("_EditUserModal", user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUser(RegisterViewModel model, bool IsActive)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid user data submitted.";
                return RedirectToAction("Index");
            }

            try
            {
                // Validate that the user exists
                if (model.Users_Id == 0)
                {
                    TempData["Error"] = "Invalid user ID.";
                    return RedirectToAction("Index");
                }

                var connStr = _configuration.GetConnectionString("SalesMetrics");

                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    var updates = new List<string>();
                    var cmd = new SqlCommand();
                    cmd.Connection = conn;

                    if (!string.IsNullOrWhiteSpace(model.FirstName))
                    {
                        updates.Add("FirstName = @FirstName");
                        cmd.Parameters.AddWithValue("@FirstName", model.FirstName);
                    }
                    if (!string.IsNullOrWhiteSpace(model.LastName))
                    {
                        updates.Add("LastName = @LastName");
                        cmd.Parameters.AddWithValue("@LastName", model.LastName);
                    }
                    if (!string.IsNullOrWhiteSpace(model.Username))
                    {
                        updates.Add("Username = @Username");
                        cmd.Parameters.AddWithValue("@Username", model.Username ?? "");
                    }
                    if (!string.IsNullOrWhiteSpace(model.Email))
                    {
                        updates.Add("Email = @Email");
                        cmd.Parameters.AddWithValue("@Email", model.Email ?? "");
                    }
                    if (!string.IsNullOrWhiteSpace(model.Password))
                    {
                        // Hash the new password with a fresh salt; never store plaintext.
                        string newSalt = PasswordSecurity.GenerateSalt();
                        string newHash = PasswordSecurity.HashPassword(model.Password, newSalt);

                        updates.Add("PasswordHash = @PasswordHash");
                        updates.Add("Salt = @Salt");
                        updates.Add("PasswordChangedDate = GETDATE()");
                        cmd.Parameters.AddWithValue("@PasswordHash", newHash);
                        cmd.Parameters.AddWithValue("@Salt", newSalt);
                    }
                    if (!string.IsNullOrWhiteSpace(model.UserId.ToString()))
                    {
                        updates.Add("UserID = @UserId");
                        cmd.Parameters.AddWithValue("@UserId", model.UserId.ToString() ?? "");
                    }
                    if (!string.IsNullOrWhiteSpace(model.RoleId.ToString()))
                    {
                        updates.Add("RoleID = @RoleId");
                        cmd.Parameters.AddWithValue("@RoleId", model.RoleId.ToString() ?? "");
                    }
                    if (!string.IsNullOrWhiteSpace(model.LocationId.ToString()))
                    {
                        updates.Add("Location = @LocationId");
                        cmd.Parameters.AddWithValue("@LocationId", model.LocationId.ToString() ?? "");
                    }
                    if (!string.IsNullOrWhiteSpace(model.SalesmanId.ToString()))
                    {
                        updates.Add("SalesmanID = @SalesmanId");
                        cmd.Parameters.AddWithValue("@SalesmanId", model.SalesmanId.ToString() ?? "");
                    }
                    if (!string.IsNullOrWhiteSpace(model.SalesmanNumber))
                    {
                        updates.Add("SalesmanNumber = @SalesmanNumber");
                        cmd.Parameters.AddWithValue("@SalesmanNumber", model.SalesmanNumber ?? "");
                    }

                    // Always include:
                    updates.Add("ModifiedDate = GETDATE()");
                    updates.Add("IsActive = @IsActive");
                    cmd.Parameters.AddWithValue("@IsActive", IsActive);
                    cmd.Parameters.AddWithValue("@Users_ID", model.Users_Id);

                    string updateSql = $"UPDATE Users SET {string.Join(", ", updates)} WHERE Users_ID = @Users_ID";
                    cmd.CommandText = updateSql;

                    cmd.ExecuteNonQuery();

                    // Remove all previous location assignments (use Users_Id primary key — same ID used by SwitchLocation)
                    var deleteCmd = new SqlCommand("DELETE FROM UserLocationAssignments WHERE UserID = @UserId", conn);
                    deleteCmd.Parameters.AddWithValue("@UserId", model.Users_Id);
                    deleteCmd.ExecuteNonQuery();

                    // Reinsert selected locations
                    if (model.AssignedLocationIds != null && model.AssignedLocationIds.Any())
                    {
                        foreach (var locId in model.AssignedLocationIds)
                        {
                            var insertCmd = new SqlCommand(@"
                            INSERT INTO UserLocationAssignments (UserID, LocationID, IsActive, DateAssigned)
                            VALUES (@UserId, @LocationId, 'YES', GETDATE())
                        ", conn);
                            insertCmd.Parameters.AddWithValue("@UserId", model.Users_Id);
                            insertCmd.Parameters.AddWithValue("@LocationId", locId);
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                }

                // Update user feature permissions using the PermissionService (using Users_Id primary key)
                var currentUserId = Convert.ToInt32(HttpContext.Session.GetString("Users_ID") ?? HttpContext.Session.GetString("UserId") ?? "0");
                if (model.AssignedFeatureIds != null && model.AssignedFeatureIds.Any())
                {
                    await _permissionService.UpdateUserPermissionsAsync(
                        model.Users_Id,
                        model.AssignedFeatureIds,
                        currentUserId
                    );
                }
                else
                {
                    // If no features selected, clear all permissions
                    await _permissionService.UpdateUserPermissionsAsync(
                        model.Users_Id,
                        new List<int>(),
                        currentUserId
                    );
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating user: {ex.Message}";
                return RedirectToAction("Index");
            }

            TempData["Success"] = "User updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetCreateUserModal()
        {
            var model = new RegisterViewModel
            {
                // Populate all possible locations for dropdown and checkboxes
                AllLocations = new List<SelectListItem>
                {
                    new("LAX", "1"),
                    new("LSV", "2"),
                    new("CHN", "3"),
                    new("PHX", "4"),
                    new("SND", "5")
                },
                AssignedLocationIds = new List<int>(), // Initialize empty list for new users
                AssignedFeatureIds = new List<int>() // Initialize empty list for new users
            };

            // Load all available features for permission checkboxes
            var allFeatures = await _permissionService.GetAllFeaturesAsync();
            model.AllFeatures = allFeatures.Select(f => new SelectListItem
            {
                Text = f.FeatureName,
                Value = f.FeatureId.ToString()
            }).ToList();

            // Group features by category for organized display
            model.GroupedFeatures = allFeatures
                .GroupBy(f => f.Category ?? "Other")
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(f => f.DisplayOrder)
                          .Select(f => new FeatureItemViewModel
                          {
                              FeatureId = f.FeatureId,
                              FeatureName = f.FeatureName,
                              Category = f.Category,
                              DisplayOrder = f.DisplayOrder
                          }).ToList()
                );

            // Set new users to active by default
            ViewBag.IsActive = true;

            return PartialView("_CreateUserModal", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateUser(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid form input.";
                return RedirectToAction("Index");
            }

            string salt = PasswordSecurity.GenerateSalt();
            string hash = PasswordSecurity.HashPassword(model.Password, salt);

            string connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            conn.Open();

            // Use SELECT SCOPE_IDENTITY() to capture the new Users_ID so location assignments use the primary key
            var cmd = new SqlCommand(@"
                INSERT INTO Users
                    (Username, Password, FirstName, LastName, Email, UserID, RoleID, Location, SalesmanID, SalesmanNumber, CreatedDate, IsActive, PasswordHash, Salt)
                VALUES
                    (@Username, @Password, @FirstName, @LastName, @Email, @UserId, @RoleId, @LocationId, @SalesmanId, @SalesmanNumber, GETDATE(), 1, @PasswordHash, @Salt);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
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
            cmd.Parameters.AddWithValue("@PasswordHash", hash);
            cmd.Parameters.AddWithValue("@Salt", salt);

            // Capture the new Users_ID (primary key) so location assignments are keyed correctly
            var newUsersId = Convert.ToInt32(cmd.ExecuteScalar());

            // Insert location assignments using the primary key (same column SwitchLocation queries)
            if (model.AssignedLocationIds != null && model.AssignedLocationIds.Any())
            {
                foreach (var locId in model.AssignedLocationIds)
                {
                    var insertCmd = new SqlCommand(@"
                        INSERT INTO UserLocationAssignments (UserID, LocationID, IsActive, DateAssigned)
                        VALUES (@UserId, @LocationId, 'YES', GETDATE())
                    ", conn);
                    insertCmd.Parameters.AddWithValue("@UserId", newUsersId);
                    insertCmd.Parameters.AddWithValue("@LocationId", locId);
                    insertCmd.ExecuteNonQuery();
                }
            }

            TempData["Success"] = "New user created.";
            return RedirectToAction("Index");
        }

        private List<FlaggedUserViewModel> GetFlaggedUsers()
        {
            var flaggedUsers = new List<FlaggedUserViewModel>();
            string connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT 
                    Users_ID, FirstName, LastName, Username, Email, Location, Password, Salt, PasswordHash, PasswordChangedDate
                FROM Users
                WHERE IsActive = 1 AND Password IS NOT NULL
            ", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string reason = "";
                int userId = Convert.ToInt32(reader["Users_ID"]);
                string username = reader["Username"].ToString()!;
                string email = reader["Email"]?.ToString() ?? "";
                string firstName = reader["FirstName"]?.ToString() ?? "";
                string lastName = reader["LastName"]?.ToString() ?? "";
                int locationid = Convert.ToInt32(reader["Location"]);
                string password = reader["Password"].ToString()!;
                string? salt = reader["Salt"]?.ToString();
                string? storedHash = reader["PasswordHash"]?.ToString();
                DateTime? changedDate = reader["PasswordChangedDate"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["PasswordChangedDate"]);
                // Define the date to check password change againt today at June 3rd 2025 at 10AM PST
                DateTime PasswordDateCheck = new DateTime(2025, 6, 3, 10, 0, 0, DateTimeKind.Utc);

                // Reason logic
                List<string> issues = new();
                if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(storedHash))
                    issues.Add("Missing hash or salt");
                if (changedDate == null || changedDate < PasswordDateCheck)
                    issues.Add("Account has not been updated");
                string simulatedHash = PasswordSecurity.HashPassword(password, salt);
                if (!string.Equals(simulatedHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    issues.Add("Password hash mismatch");

                reason = string.Join(" | ", issues);

                if (!string.IsNullOrEmpty(reason))
                {
                    flaggedUsers.Add(new FlaggedUserViewModel
                    {
                        Users_ID = userId,
                        FirstName = firstName,
                        LastName = lastName,
                        Username = username,
                        Email = email,
                        LocationId = locationid, 
                        Reason = reason
                    });
                }
            }

            return flaggedUsers;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult FixUserPassword(int userId, int locationId)
        {
            string connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            // 1. Retrieve plain password for user+location
            var cmd = new SqlCommand(@"
                SELECT Password 
                FROM Users 
                WHERE Users_ID = @UserId 
                    AND Location = @LocationId 
                    AND Password IS NOT NULL
            ", conn);

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@LocationId", locationId);

            string? plainPassword = cmd.ExecuteScalar()?.ToString();

            if (string.IsNullOrEmpty(plainPassword))
            {
                TempData["Error"] = "❌ Password missing for this user.";
                return RedirectToAction("Index");
            }

            // 2. Generate hash + salt
            string newSalt = PasswordSecurity.GenerateSalt();
            string newHash = PasswordSecurity.HashPassword(plainPassword, newSalt);

            // 3. Update password for that location
            var updateCmd = new SqlCommand(@"
                UPDATE Users
                SET PasswordHash = @Hash,
                    Salt = @Salt,
                    PasswordChangedDate = GETDATE()
                WHERE Users_ID = @UserId AND Location = @LocationId
            ", conn);

            updateCmd.Parameters.AddWithValue("@Hash", newHash);
            updateCmd.Parameters.AddWithValue("@Salt", newSalt);
            updateCmd.Parameters.AddWithValue("@UserId", userId);
            updateCmd.Parameters.AddWithValue("@LocationId", locationId);

            int affected = updateCmd.ExecuteNonQuery();

            if (affected > 0)
                TempData["Success"] = $"✅ Fixed user {userId} for location {locationId}.";
            else
                TempData["Error"] = $"❌ Update failed for user {userId}.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult SwitchLocation(string selectedLocation)
        {
            selectedLocation = selectedLocation ?? "LAX"; // or your default

            var roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            var userId = int.Parse(User.FindFirst("Users_ID")?.Value ?? "0");

            // Allow switching for Admins, GMs, Office Managers, and Office Staff
            // Admin (1) and GM (4) can switch to any location
            // Office Manager (5) and Office Staff (6) can only switch to assigned locations
            bool canSwitch = false;

            if (roleId == 1 || roleId == 4)
            {
                // Admin and GM can switch to any location
                canSwitch = true;
            }
            else if (roleId == 5 || roleId == 6)
            {
                // Office Manager and Office Staff - verify they have access to this location
                var targetLocationId = LocationHelper.GetLocationId(selectedLocation);

                string connStr = _configuration.GetConnectionString("SalesMetrics");
                using var conn = new SqlConnection(connStr);
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM UserLocationAssignments
                    WHERE UserID = @UserId AND LocationID = @LocationId AND IsActive = 'YES'
                ", conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@LocationId", targetLocationId);

                int count = (int)cmd.ExecuteScalar();
                canSwitch = count > 0;
            }

            if (canSwitch && !string.IsNullOrEmpty(selectedLocation))
            {
                HttpContext.Session.SetString("OfficeLocation", selectedLocation);

                // ✅ NEW: Also store LocationId for use across controllers
                int locationId = LocationHelper.GetLocationId(selectedLocation);
                HttpContext.Session.SetString("LocationId", locationId.ToString());
            }

            var referrer = Request.Headers["Referer"].ToString();
            return Redirect(!string.IsNullOrEmpty(referrer) ? referrer : "/");
        }

        // ======================================================================
        // BULK PERMISSIONS MANAGER
        // ======================================================================

        [HttpGet]
        public async Task<IActionResult> BulkPermissions(int? featureId, int? roleId, int? locationId)
        {
            var features = await _permissionService.GetAllFeaturesAsync();

            var vm = new BulkPermissionViewModel
            {
                SelectedFeatureId = featureId ?? 0,
                SelectedRoleId    = roleId,
                SelectedLocationId = locationId,
                AllFeatures = features
                    .Select(f => new SelectListItem
                    {
                        Value    = f.FeatureId.ToString(),
                        Text     = string.IsNullOrWhiteSpace(f.Category) ? f.FeatureName : $"{f.Category} › {f.FeatureName}",
                        Selected = f.FeatureId == featureId
                    }).ToList(),
                AllRoles = GetRolesSelectList(roleId),
            };
            vm.AllFeatures.Insert(0, new SelectListItem { Value = "", Text = "— Select a Feature —" });

            if (featureId.HasValue && featureId.Value > 0)
            {
                vm.FeatureName = features.FirstOrDefault(f => f.FeatureId == featureId.Value)?.FeatureName;
                var usersWithAccess = await _permissionService.GetUsersWithFeatureAccessAsync(featureId.Value);
                vm.Users = LoadUsersForBulkPermissions(roleId, locationId, usersWithAccess);
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdatePermissions(int featureId, int? roleId, int? locationId, List<int>? grantedUserIds)
        {
            var grantedByUserId = int.TryParse(User.FindFirst("Users_ID")?.Value, out var uid) ? uid : 0;
            var grantedSet = grantedUserIds ?? new List<int>();

            // Load the exact set of user IDs that were visible under the active filters.
            // This ensures Grant All / Revoke All only affects the filtered scope and
            // never touches permissions for users outside the current filter.
            var scopedUserIds = LoadUserIdsForBulkPermissions(roleId, locationId);

            var success = await _permissionService.BulkUpdateFeatureAccessForScopedUsersAsync(
                featureId, scopedUserIds, grantedSet, grantedByUserId);

            if (success)
                TempData["SuccessMessage"] = $"Permissions updated for {grantedSet.Count} user(s).";
            else
                TempData["ErrorMessage"] = "An error occurred while updating permissions. Please try again.";

            return RedirectToAction(nameof(BulkPermissions), new { featureId, roleId, locationId });
        }

        private List<BulkUserPermissionRow> LoadUsersForBulkPermissions(int? roleId, int? locationId, List<int> usersWithAccess)
        {
            var users = new List<BulkUserPermissionRow>();
            string connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            var where = new List<string> { "u.IsActive = 1" };
            var cmd = new SqlCommand();
            cmd.Connection = conn;

            if (roleId.HasValue)
            {
                where.Add("u.RoleID = @RoleId");
                cmd.Parameters.AddWithValue("@RoleId", roleId.Value);
            }
            if (locationId.HasValue)
            {
                where.Add("u.Location = @LocationId");
                cmd.Parameters.AddWithValue("@LocationId", locationId.Value);
            }

            cmd.CommandText = $@"
                SELECT u.Users_ID, u.FirstName, u.LastName, u.RoleID,
                       ISNULL(r.RoleName, 'Unknown') AS RoleName,
                       u.Location,
                       CASE u.Location WHEN 1 THEN 'LAX' WHEN 2 THEN 'LSV'
                           WHEN 3 THEN 'CHN' WHEN 4 THEN 'PHX' WHEN 5 THEN 'SND'
                           ELSE 'N/A' END AS LocationName
                FROM Users u
                LEFT JOIN Roles r ON u.RoleID = r.RoleID
                WHERE {string.Join(" AND ", where)}
                ORDER BY u.FirstName, u.LastName";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var usersId = reader.GetInt32(reader.GetOrdinal("Users_ID"));
                users.Add(new BulkUserPermissionRow
                {
                    Users_ID  = usersId,
                    FullName  = $"{reader["FirstName"]} {reader["LastName"]}".Trim(),
                    RoleName  = reader["RoleName"].ToString() ?? "",
                    RoleId    = reader.GetInt32(reader.GetOrdinal("RoleID")),
                    Location  = reader["LocationName"].ToString() ?? "",
                    LocationId = reader.GetInt32(reader.GetOrdinal("Location")),
                    HasAccess = usersWithAccess.Contains(usersId)
                });
            }

            return users;
        }

        private List<int> LoadUserIdsForBulkPermissions(int? roleId, int? locationId)
        {
            var userIds = new List<int>();
            string connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            var where = new List<string> { "IsActive = 1" };
            var cmd = new SqlCommand();
            cmd.Connection = conn;

            if (roleId.HasValue)
            {
                where.Add("RoleID = @RoleId");
                cmd.Parameters.AddWithValue("@RoleId", roleId.Value);
            }
            if (locationId.HasValue)
            {
                where.Add("Location = @LocationId");
                cmd.Parameters.AddWithValue("@LocationId", locationId.Value);
            }

            cmd.CommandText = $"SELECT Users_ID FROM Users WHERE {string.Join(" AND ", where)}";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                userIds.Add(reader.GetInt32(0));

            return userIds;
        }

        // ======================================================================
        // BULK LOCATION ASSIGNMENT
        // ======================================================================

        [HttpGet]
        public async Task<IActionResult> BulkLocationAssignment(int? locationId, int? roleId, int? primaryLocationId)
        {
            var vm = new BulkLocationAssignmentViewModel
            {
                SelectedLocationId     = locationId ?? 0,
                FilterRoleId           = roleId,
                FilterPrimaryLocationId = primaryLocationId,
                AllLocations = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "— Select a Location —" },
                    new SelectListItem { Value = "1", Text = "LAX — Los Angeles",  Selected = locationId == 1 },
                    new SelectListItem { Value = "2", Text = "LSV — Las Vegas",    Selected = locationId == 2 },
                    new SelectListItem { Value = "3", Text = "CHN — Chino",        Selected = locationId == 3 },
                    new SelectListItem { Value = "4", Text = "PHX — Phoenix",      Selected = locationId == 4 },
                    new SelectListItem { Value = "5", Text = "SND — San Diego",    Selected = locationId == 5 },
                },
                AllRoles = GetRolesSelectList(roleId),
            };

            if (locationId.HasValue && locationId.Value > 0)
            {
                vm.LocationName = vm.AllLocations
                    .FirstOrDefault(l => l.Value == locationId.Value.ToString())?.Text;

                var usersWithAssignment = await _permissionService.GetUsersWithLocationAssignmentAsync(locationId.Value);
                vm.Users = LoadUsersForBulkLocationAssignment(roleId, primaryLocationId, usersWithAssignment);
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateLocationAssignment(
            int locationId, int? roleId, int? primaryLocationId, List<int>? assignedUserIds)
        {
            var adminUserId = Convert.ToInt32(
                HttpContext.Session.GetString("Users_ID") ?? HttpContext.Session.GetString("UserId") ?? "0");
            var assignSet = assignedUserIds ?? new List<int>();

            // Reconstruct the exact filtered scope so we only touch visible users
            var scopedUserIds = LoadUserIdsForBulkPermissions(roleId, primaryLocationId);

            var success = await _permissionService.BulkUpdateLocationAssignmentsAsync(
                locationId, scopedUserIds, assignSet, adminUserId);

            if (success)
                TempData["SuccessMessage"] = $"Location assignments updated for {assignSet.Count} user(s).";
            else
                TempData["ErrorMessage"] = "An error occurred while updating location assignments. Please try again.";

            return RedirectToAction(nameof(BulkLocationAssignment), new { locationId, roleId, primaryLocationId });
        }

        private List<BulkUserLocationRow> LoadUsersForBulkLocationAssignment(
            int? roleId, int? primaryLocationId, List<int> usersWithAssignment)
        {
            var users = new List<BulkUserLocationRow>();
            string connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            conn.Open();

            var where = new List<string> { "u.IsActive = 1" };
            var cmd = new SqlCommand();
            cmd.Connection = conn;

            if (roleId.HasValue)
            {
                where.Add("u.RoleID = @RoleId");
                cmd.Parameters.AddWithValue("@RoleId", roleId.Value);
            }
            if (primaryLocationId.HasValue)
            {
                where.Add("u.Location = @PrimaryLocationId");
                cmd.Parameters.AddWithValue("@PrimaryLocationId", primaryLocationId.Value);
            }

            cmd.CommandText = $@"
                SELECT u.Users_ID, u.FirstName, u.LastName, u.RoleID,
                       ISNULL(r.RoleName, 'Unknown') AS RoleName,
                       u.Location,
                       CASE u.Location WHEN 1 THEN 'LAX' WHEN 2 THEN 'LSV'
                           WHEN 3 THEN 'CHN' WHEN 4 THEN 'PHX' WHEN 5 THEN 'SND'
                           ELSE 'N/A' END AS LocationName
                FROM Users u
                LEFT JOIN Roles r ON u.RoleID = r.RoleID
                WHERE {string.Join(" AND ", where)}
                ORDER BY u.FirstName, u.LastName";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var usersId = reader.GetInt32(reader.GetOrdinal("Users_ID"));
                users.Add(new BulkUserLocationRow
                {
                    Users_ID          = usersId,
                    FullName          = $"{reader["FirstName"]} {reader["LastName"]}".Trim(),
                    RoleName          = reader["RoleName"].ToString() ?? "",
                    RoleId            = reader.GetInt32(reader.GetOrdinal("RoleID")),
                    PrimaryLocation   = reader["LocationName"].ToString() ?? "",
                    PrimaryLocationId = reader.GetInt32(reader.GetOrdinal("Location")),
                    IsAssigned        = usersWithAssignment.Contains(usersId)
                });
            }

            return users;
        }

        private List<SelectListItem> GetRolesSelectList(int? selectedRoleId)
        {
            var roles = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "— All Roles —", Selected = !selectedRoleId.HasValue }
            };
            string connStr = _configuration.GetConnectionString("SalesMetrics");
            using var conn = new SqlConnection(connStr);
            conn.Open();
            var cmd = new SqlCommand("SELECT RoleID, RoleName FROM Roles ORDER BY RoleName", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(reader.GetOrdinal("RoleID"));
                roles.Add(new SelectListItem
                {
                    Value    = id.ToString(),
                    Text     = reader["RoleName"].ToString() ?? "",
                    Selected = selectedRoleId.HasValue && selectedRoleId.Value == id
                });
            }
            return roles;
        }
    }
}