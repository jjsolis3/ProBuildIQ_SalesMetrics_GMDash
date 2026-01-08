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

        public AccountsController(IConfiguration configuration, IPermissionService permissionService)
        {
            _configuration = configuration;
            _permissionService = permissionService;
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
                    locCmd.Parameters.AddWithValue("@UserId", user.UserId);

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
            var salesmanName = User.FindFirst("FullName")?.Value ?? "";
            
            var userProfile = new UserProfileViewModel();
            var metrics = new SalesRepMetricsViewModel
            {
                PropertyDetails = new List<SalesRepNewAccountSummaryViewModel>()
            };

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            conn.Open();

            // -- Fetch User Profile Info --
            var cmd = new SqlCommand(@"
                SELECT FirstName, LastName, Email, RoleID, GoogleEmail, GoogleAccessToken, GoogleRefreshToken, SalesmanID, SalesmanNumber 
                FROM Users 
                WHERE UserID = @UserID and Location = @LocationId
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
                }
            }

            // -- Fetch Sales Rep Metrics Query --
            var parsedStartDate = startDate ?? DateTime.UtcNow.AddDays(-30); // Default to last 30 days
            var parsedEndDate = endDate ?? DateTime.UtcNow; // Default to today

            //int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            var connectionString = _configuration.GetConnectionString(officeLocation);

            using var CUFconn = new SqlConnection(connectionString);
            CUFconn.Open();

            var metricsCmd = new SqlCommand(@"
                -- Main report
                WITH NewAccounts AS (
                    SELECT
                        C.CUM_CUSTOMER_NAME AS Property,
                        PC.IPC_DESCRIPTION AS [Mgmt Co],
                        SM.SMN_SALESMAN_NAME AS Salesperson,
                        SH.SOH_NUMBER AS [Order#],
                        IH.IHF_INVOICE_NUMBER AS [Invoice#],
                        CAST(C.CUM_ESTABLISHED_DATE AS DATE) AS [Date Created],
                        CAST(SH.SOH_DELIVERY_DATE AS DATE) AS [Date Installed],
                        ISNULL(SOH_TOTAL_AMOUNT, 0) AS OrderAmount,
                        ISNULL(IHF_TOTAL_AMOUNT, 0) AS InvoiceAmount
                    FROM CUSTOMER_MASTER C
                    LEFT JOIN SALESMAN_MASTER SM ON C.CUM_SMNMAS_ID = SM.SMN_SMNMAS_ID
                    LEFT JOIN PRICE_CODES PC ON C.CUM_PRICE_CODE = PC.IPC_PRICE_CODE
                    LEFT JOIN SALES_HEADER SH ON C.CUM_CUSTOMER_NUMBER = SH.SOH_CUSTOMER_NUMBER
                    LEFT JOIN INVOICE_HEADER IH ON SH.SOH_NUMBER = IH.IHF_ORDER_NUMBER
                    WHERE
                        C.CUM_ESTABLISHED_DATE >= @startDate
                        AND C.CUM_ESTABLISHED_DATE < @endDate
                        AND SH.SOH_CANCELED_DATE IS NULL
                        AND SH.SOH_WHSMAS_ID = 1
                        AND SM.SMN_SALESMAN_NAME = @salesperson
                )

                SELECT
                    Property,
                    [Mgmt Co] AS ManagementCompany,
                    MAX([Date Created]) as Established,
                    COUNT(DISTINCT [Order#]) AS Orders,
                    SUM(OrderAmount) AS TotalSalesAmount,
                    COUNT(DISTINCT [Invoice#]) AS Invoices,
                    SUM(InvoiceAmount) AS TotalInvoiceAmount
                FROM NewAccounts
                GROUP BY Property, [Mgmt Co]
                ORDER BY Property
            ", CUFconn);

            metricsCmd.Parameters.AddWithValue("@startDate", parsedStartDate);
            metricsCmd.Parameters.AddWithValue("@endDate", parsedEndDate);
            metricsCmd.Parameters.AddWithValue("@salesperson", salesmanName);

            using (var metricsReader = metricsCmd.ExecuteReader())
            {
                while (metricsReader.Read())
                {
                    var item = new SalesRepNewAccountSummaryViewModel
                    {
                        Property = metricsReader["Property"].ToString() ?? "",
                        ManagementCompany = metricsReader["ManagementCompany"].ToString() ?? "",
                        EstablishedDate = Convert.ToDateTime(metricsReader["Established"]),
                        Orders = Convert.ToInt32(metricsReader["Orders"]),
                        TotalSalesAmount = Convert.ToDecimal(metricsReader["TotalSalesAmount"]),
                        Invoices = Convert.ToInt32(metricsReader["Invoices"]),
                        TotalInvoiceAmount = Convert.ToDecimal(metricsReader["TotalInvoiceAmount"])
                    };
                    metrics.PropertyDetails.Add(item);
                }
            }

            // -- Fetch Sales Rep Metrics Summary --
            metrics.NewAccounts = metrics.PropertyDetails.Count;
            metrics.OrdersCount = metrics.PropertyDetails.Sum(x => x.Orders);
            metrics.InvoicesCount = metrics.PropertyDetails.Sum(x => x.Invoices);
            metrics.TotalSalesAmount = metrics.PropertyDetails.Sum(x => x.TotalSalesAmount);
            metrics.TotalInvoiceAmount = metrics.PropertyDetails.Sum(x => x.TotalInvoiceAmount);
            metrics.WithoutOrders = metrics.PropertyDetails.Count(x => x.Orders == 0);

            // -- Return Composite Model --
            var pageModel = new SalesRepProfilePageViewModel
            {
                UserProfile = userProfile,
                Metrics = metrics,
                StartDate = parsedStartDate,
                EndDate = parsedEndDate,
            };

            return View(pageModel);
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
                locationCmd.Parameters.AddWithValue("@UserId", user.UserId);
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
                        updates.Add("Password = @Password");
                        cmd.Parameters.AddWithValue("@Password", model.Password ?? "");
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

                    // Remove all previous location assignments
                    var deleteCmd = new SqlCommand("DELETE FROM UserLocationAssignments WHERE UserID = @UserId", conn);
                    deleteCmd.Parameters.AddWithValue("@UserId", model.UserId);
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
                            insertCmd.Parameters.AddWithValue("@UserId", model.UserId);
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
        public IActionResult GetCreateUserModal()
        {
            var model = new RegisterViewModel();
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
            cmd.Parameters.AddWithValue("@PasswordHash", hash);
            cmd.Parameters.AddWithValue("@Salt", salt);
            cmd.ExecuteNonQuery();

            // 🔻 assigned multiple locations for user
            if (model.AssignedLocationIds != null && model.AssignedLocationIds.Any())
            {
                foreach (var locId in model.AssignedLocationIds)
                {
                    var insertCmd = new SqlCommand(@"
                        INSERT INTO UserLocationAssignments (UserID, LocationID, IsActive, DateAssigned)
                        VALUES (@UserId, @LocationId, 'YES', GETDATE())
                    ", conn);
                    insertCmd.Parameters.AddWithValue("@UserId", model.UserId);
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

            // Allow switching only for Admins and GMs
            if ((roleId == 1 || roleId == 4) && !string.IsNullOrEmpty(selectedLocation))
            {
                HttpContext.Session.SetString("OfficeLocation", selectedLocation);

                // ✅ NEW: Also store LocationId for use across controllers
                int locationId = LocationHelper.GetLocationId(selectedLocation);
                HttpContext.Session.SetString("LocationId", locationId.ToString());
            }

            var referrer = Request.Headers["Referer"].ToString();
            return Redirect(!string.IsNullOrEmpty(referrer) ? referrer : "/");
        }
    }
}