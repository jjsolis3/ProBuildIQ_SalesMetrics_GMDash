using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using SalesMetrics.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SalesMetrics.Controllers
{
    public class AccountsController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ACCOUNTS PAGE
        [HttpGet]
        public IActionResult Index()
        {
            var users = new List<UserEntity>(); // Use your model class here
            string connStr = _configuration.GetConnectionString("SalesMetrics");

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM Users", conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    users.Add(new UserEntity
                    {
                        Users_ID = Convert.ToInt32(reader["Users_ID"]),
                        FirstName = reader["FirstName"].ToString(),
                        LastName = reader["LastName"].ToString(),
                        Username = reader["Username"].ToString(),
                        Email = reader["Email"].ToString(),
                        RoleId = Convert.ToInt32(reader["RoleID"]),
                        Location = Convert.ToInt32(reader["Location"]),
                        CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                        IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                        LastLoginDate = reader["LastLoginDate"] != DBNull.Value ? Convert.ToDateTime(reader["LastLoginDate"]) : null
                    });
                }
            }

            return View(users);
        }

        // USER PROFILE PAGE
        public IActionResult Profile()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            conn.Open();
            var cmd = new SqlCommand("SELECT FirstName, LastName, Email, GoogleEmail, GoogleAccessToken, GoogleRefreshToken FROM Users WHERE UserID = @UserID and Location = @LocationId", conn);
            cmd.Parameters.AddWithValue("@UserID", userId);
            cmd.Parameters.AddWithValue("@LocationId", locationId);

            var model = new UserProfileViewModel();

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    model.FullName = reader["FirstName"] + " " + reader["LastName"];
                    model.Email = reader["Email"]?.ToString();
                    model.GoogleEmail = reader["GoogleEmail"]?.ToString();
                    model.GoogleAccessToken = reader["GoogleAccessToken"]?.ToString();
                    model.GoogleRefreshToken = reader["GoogleRefreshToken"]?.ToString();

                }
            }

            return View(model);
        }

        // KANBAN PAGE
        public IActionResult Kanban()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> TestGoogleTask()
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();
            var cmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE UserID = @UserID", conn);
            cmd.Parameters.AddWithValue("@UserID", userId);

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
            var result = await taskService.CreateTaskAsync(accessToken, refreshToken, userId.ToString(), "SalesMetrics Test Task", "This is a test task created from SalesMetrics", DateTime.UtcNow.AddHours(1));

            if (!string.IsNullOrEmpty(result))
                TempData["Success"] = $"Google Task created successfully! Task ID: {result}";
            else
                TempData["Error"] = "Failed to create Google Task.";

            return RedirectToAction("User");
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

            var users = new List<(int UserId, string Password)>();

            // Step 1: Get all users with non-null passwords
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT UserID, Password FROM Users WHERE Password IS NOT NULL", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    users.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }

            // Step 2: Update each user's PasswordHash and Salt
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                foreach (var user in users)
                {
                    string salt = PasswordSecurity.GenerateSalt();
                    string hash = PasswordSecurity.HashPassword(user.Password, salt);

                    var updateCmd = new SqlCommand(@"
                    UPDATE Users
                    SET PasswordHash = @Hash,
                        Salt = @Salt,
                        PasswordChangedDate = GETDATE()
                    WHERE UserID = @UserID", conn);

                    updateCmd.Parameters.AddWithValue("@Hash", hash);
                    updateCmd.Parameters.AddWithValue("@Salt", salt);
                    updateCmd.Parameters.AddWithValue("@UserID", user.UserId);

                    updateCmd.ExecuteNonQuery();
                }
            }

            TempData["Success"] = $"✅ Rehashed {users.Count} user passwords successfully.";
            return RedirectToAction("Index"); // Adjust if your user page action is named differently
        }

        [HttpGet]
        public IActionResult GetEditUserModal(int id)
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
                        UserId = Convert.ToInt32(reader["Users_ID"]),
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
            }

            return PartialView("_EditUserModal", user);
        }

        [HttpPost]
        public IActionResult UpdateUser(RegisterViewModel model, bool IsActive)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid user data submitted.";
                return RedirectToAction("Index");
            }

            var connStr = _configuration.GetConnectionString("SalesMetrics");

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                var cmd = new SqlCommand(@"
                    UPDATE Users
                    SET FirstName = @FirstName,
                        LastName = @LastName,
                        Username = @Username,
                        Email = @Email,
                        RoleID = @RoleID,
                        Location = @LocationID,
                        SalesmanID = @SalesmanID,
                        SalesmanNumber = @SalesmanNumber,
                        IsActive = @IsActive,
                        ModifiedDate = GETDATE()
                    WHERE Users_ID = @UserID
                ", conn);

                cmd.Parameters.AddWithValue("@FirstName", model.FirstName ?? "");
                cmd.Parameters.AddWithValue("@LastName", model.LastName ?? "");
                cmd.Parameters.AddWithValue("@Username", model.Username ?? "");
                cmd.Parameters.AddWithValue("@Email", model.Email ?? "");
                cmd.Parameters.AddWithValue("@RoleID", model.RoleId);
                cmd.Parameters.AddWithValue("@LocationID", model.LocationId);
                cmd.Parameters.AddWithValue("@SalesmanID", (object?)model.SalesmanId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SalesmanNumber", (object?)model.SalesmanNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsActive", IsActive);
                cmd.Parameters.AddWithValue("@UserID", model.UserId);

                cmd.ExecuteNonQuery();
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

            TempData["Success"] = "New user created.";
            return RedirectToAction("Index");
        }


    }
}