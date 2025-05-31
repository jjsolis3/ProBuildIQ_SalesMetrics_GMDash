using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using System.Collections.Generic;

using User = SalesMetrics.Models.User;
using EfUser = SalesMetrics.Models.EFCore.UserEntity;
using Calendar = SalesMetrics.Models.Calendar;
using Microsoft.Extensions.Localization;
using SalesMetrics.Services;
using Google.Apis.Tasks.v1.Data;
using System.Diagnostics.CodeAnalysis;
using SalesMetrics.Services.Helpers;

namespace SalesMetrics.Controllers
{

    public class TasksController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly SalesMetricsDbContext _context;

        public TasksController(IConfiguration configuration, SalesMetricsDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public IActionResult AdminTask(string filter = "all")
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int locationId = int.Parse(User.FindFirst("LocationId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            var tasks = GetAllTasksByLocation(Convert.ToInt32(locationId));
            // Backend Filtering
            if (filter == "active")
            {
                tasks = tasks.Where(t => t.Status != "Completed").ToList();
            }
            else if (filter == "completed")
            {
                tasks = tasks.Where(t => t.Status == "Completed").ToList();
            }

            var users = GetAllUsersForTaskDisplay(Convert.ToInt32(locationId), roleId);

            var viewModel = new TaskPageViewModel
            {
                Tasks = tasks,
                Users = users,
                NewTask = new SalesTask
                {
                    DueDate = DateTime.Now.AddHours(2) // ⏰ 2 hours from now
                }
            };

            // In Task() action and any other that renders the task modal:
            ViewBag.UserId = userId;
            ViewBag.RoleId = roleId;
            ViewBag.LocationId = locationId;
            ViewBag.SalesmanId = salesmanId;
            ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("AdminTask");

            return View(viewModel);
        }

        public IActionResult Task(string filter = "all")
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");
            
            int locationId = int.Parse(User.FindFirst("locationId")?.Value ?? "0");
            int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            var tasks = GetTasksByUserId(Convert.ToInt32(userId), locationId);

            // Backend Filtering
            if (filter == "active")
            {
                tasks = tasks.Where(t => t.Status != "Completed").ToList();
            }
            else if (filter == "completed")
            {
                tasks = tasks.Where(t => t.Status == "Completed").ToList();
            }

            var users = GetActiveUsers();

            var viewModel = new TaskPageViewModel
            {
                Tasks = tasks,
                Users = users,
                NewTask = new SalesTask
                {
                    DueDate = DateTime.Now.AddHours(2) // ⏰ 2 hours from now
                }
            };

            // In Task() action and any other that renders the task modal:
            ViewBag.UserId = userId;
            ViewBag.RoleId = roleId;
            ViewBag.SalesmanId = salesmanId;
            ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("Task");

            return View(viewModel);
        }

        public IActionResult Schedule()
        {
            int userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
            int locationId = int.Parse(User.FindFirst("locationId")?.Value ?? "0");
            
            List<SalesTask> tasks;
            List<User> users;

            if (roleId == 1 || roleId == 3)
            {
                tasks = GetAllTasksByLocation(locationId);
                users = GetAllUsersForTaskDisplay(locationId, roleId);

                // 💬 Filter users who have at least one task
                var usersWithTasks = users
                    .Where(u => tasks.Any(t => t.AssignedTo == u.UserID))
                    .ToList();

                ViewBag.FilterUsers = usersWithTasks;
                ViewBag.AllUsers = users;
            }
            else
            {
                tasks = GetTasksByUserId(userId, locationId);
                users = GetActiveUsers(); // just for modal use

                ViewBag.FilterUsers = users; // For regular users, just themselves
            }

            //methods to get user list
            ViewBag.RoleId = roleId;
            ViewBag.UserId = userId;

            var calendarTasks = tasks.Select(t => new TaskCalendarViewModel
            {
                TaskId = t.TaskID,
                Title = GetTaskTitle(t, users, roleId),
                Type = t.Type,
                Status = t.Status,
                Property = t.Property,
                Description = t.Description,
                DueDate = t.DueDate,
                AssignedTo = users.FirstOrDefault(u => u.UserID == t.AssignedTo) is var u && u != null ? $"{u.FirstName} {u.LastName}" : "Unassigned"
            }).ToList();
            
            return View(calendarTasks); // Updated model
        }

        public IActionResult TaskKanban()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");
            int locationId = int.Parse(User.FindFirst("locationId")?.Value ?? "0");

            // In Task() action and any other that renders the task modal:
            ViewBag.UserId = userId;
            ViewBag.RoleId = roleId;
            ViewBag.SalesmanId = salesmanId;

            var tasks = GetTasksByUserId(Convert.ToInt32(userId), locationId);
            var users = GetActiveUsers();

            var viewModel = new TaskPageViewModel
            {
                Tasks = tasks,
                Users = users,
                NewTask = new SalesTask
                {
                    DueDate = DateTime.Now.AddHours(2)
                }
            };

            return View("TaskKanban", viewModel); // 👈 This matches your .cshtml file
        }

        // Add methods like Create, Update Status, Delete, etc
        [HttpPost]
        public async Task<IActionResult> Create(TaskModalViewModel modal)
        {
            modal.TaskTypes = TaskTypeHelper.GetTaskTypes("Task"); // 👈 Key line

            var model = modal.Task;
            int userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");
            int locationId = int.Parse(User.FindFirst("locationId")?.Value ?? "0");

            if (!string.IsNullOrEmpty(model.DueDateDate) && !string.IsNullOrEmpty(model.DueDateTime))
            {
                var combinedString = $"{model.DueDateDate} {model.DueDateTime}";
                if (DateTime.TryParse(combinedString, out var parsed))
                {
                    model.DueDate = parsed;
                }
                else
                {
                    ModelState.AddModelError("Task.DueDate", "Invalid date and time.");
                }
            }
            else
            {
                ModelState.AddModelError("Task.DueDate", "Both date and time are required.");
            }

            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState)
                {
                    Console.WriteLine($"{error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                }

                // Reload page with existing task/user list
                var viewModel = new TaskPageViewModel
                {
                    Tasks = GetTasksByUserId(userId, locationId),
                    Users = GetActiveUsers(),
                    NewTask = new SalesTask
                    {
                        Title = model.Title,
                        Description = model.Description,
                        DueDate = model.DueDate,
                        Status = model.Status,
                        AssignedTo = model.AssignedTo,
                        Property = model.Property,
                        Type = model.Type,
                        PropertyID = model.PropertyID,
                        Location = locationId
                    }
                };

                ViewBag.UserId = userId;
                ViewBag.RoleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
                ViewBag.SalesmanId = salesmanId;
                ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("Task");

                return View("Task", viewModel);
            }

            if (model.AssignedTo == 0)
            {
                model.AssignedTo = userId;
            }

            var task = new SalesTask
            {
                Title = model.Title,
                Description = model.Description,
                DueDate = model.DueDate,
                Status = model.Status ?? "Pending",
                AssignedTo = model.AssignedTo,
                Property = model.Property,
                Type = model.Type,
                CreatedBy = HttpContext.Session.GetString("Username") ?? "admin",
                CreatedDate = DateTime.Now,
            };

            Console.WriteLine($"AssignedTo from modal: {model.AssignedTo}");

            var locationStr = HttpContext.Session.GetString("LocationId");
            if (int.TryParse(locationStr, out var locId))
                task.Location = locId;

            int taskId = SaveTaskToDatabase(task);

            // Add to Google Task
            string? googleTaskId = null;
            string? googleEventId = null;

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE UserID = @UserID", conn);
                cmd.Parameters.AddWithValue("@UserID", model.AssignedTo);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        var accessToken = reader["GoogleAccessToken"]?.ToString();
                        var refreshToken = reader["GoogleRefreshToken"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken))
                        {
                            var tasksService = new GoogleTasksService(_configuration);
                            googleTaskId = await tasksService.CreateTaskAsync(
                                accessToken, refreshToken, model.AssignedTo.ToString(), task.Title, task.Description, task.DueDate
                            );

                            // Add to Google Calendar
                            if (task.Type?.Equals("QC", StringComparison.OrdinalIgnoreCase) == true || task.Type?.Equals("Site Visit", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var calendarService = new GoogleCalendarService(_configuration);
                                googleEventId = await calendarService.AddTaskEventAsync(
                                    (int)model.AssignedTo, accessToken, refreshToken, task.Title, task.Description, task.DueDate ?? DateTime.Now
                                );
                            }

                            if (!string.IsNullOrEmpty(googleEventId) || !string.IsNullOrEmpty(googleTaskId))
                            {
                                TempData["Success"] = "Task created and synced to Google Task & Calendar.";
                            }
                            else if (!string.IsNullOrEmpty(googleEventId) && string.IsNullOrEmpty(googleTaskId))
                            {
                                TempData["Success"] = "Task created and synced to Google Calendar.";
                            }
                            else if (string.IsNullOrEmpty(googleEventId) && !string.IsNullOrEmpty(googleTaskId))
                            {
                                TempData["Success"] = "Task created and synced to Google Task.";
                            }
                            else
                            {
                                TempData["Success"] = "Task created Successfully.";
                            }
                        }
                        else
                        {
                            TempData["Warning"] = "Task Created!";
                        }
                    }
                }

                // Save GoogleTaskId and GoogleEventId to Task Record
                if (!string.IsNullOrEmpty(googleTaskId) || !string.IsNullOrEmpty(googleEventId))
                {
                    var updateCmd = new SqlCommand(@"
                        UPDATE Tasks
                        SET GoogleTaskId = @GoogleTaskId,
                            GoogleEventId = @GoogleEventId
                        WHERE TaskID = @TaskID
                            and Title = @Title 
                            and AssignedTo = @AssignedTo 
                            and CreatedDate = @CreatedDate
                    ", conn);

                    updateCmd.Parameters.AddWithValue("@GoogleTaskId", (object?)googleTaskId ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@GoogleEventId", (object?)googleEventId ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@TaskID", taskId);
                    updateCmd.Parameters.AddWithValue("@Title", task.Title);
                    updateCmd.Parameters.AddWithValue("@AssignedTo", task.AssignedTo);
                    updateCmd.Parameters.AddWithValue("@CreatedDate", task.CreatedDate);

                    await updateCmd.ExecuteNonQueryAsync();
                }
            }

            if (TempData["Success"] == null && TempData["Warning"] == null)
            {
                TempData["Success"] = "Task created successfully.";
            }

            var returnUrl = Request.Form["ReturnUrl"].ToString();

            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Task"); // fallback just in case
            }
        }

        private List<User> GetActiveUsers()
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");

            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            var users = new List<User>();

            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();

                string query;

                if (roleId == 1)
                {
                    // Admins and Sales Admins see all users at the same location
                    query = @"SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Location = @Location";
                }
                else if (roleId == 3)
                {
                    // Admins and Sales Admins see all users at the same location
                    query = @"SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE RoleID in (2, 3) AND Location = @Location";
                }
                else
                {
                    // Sales reps only see themselves
                    query = @"SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE UserID = @UserId";
                }

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (roleId == 1 || roleId == 3)
                        cmd.Parameters.AddWithValue("@Location", locationId);
                    else
                        cmd.Parameters.AddWithValue("@UserId", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                UserID = reader.GetInt32(0),
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2),
                                RoleID = reader.GetInt32(3),
                                Location = reader.GetInt32(4),
                                CreatedDate = reader.GetDateTime(5),
                                SalesmanID = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                            });
                        }
                    }
                }
            }
            return users;
        }

        private List<User> GetAllUsersForTaskDisplay(int locationId, int roleId)
        {
            var users = new List<User>();

            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();

                string query;

                if (roleId == 1)
                {
                    query = @"
                        SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Location = @Location";
                }
                else if (roleId == 3)
                {
                    query = @"
                        SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE RoleId <> 1 AND Location = @Location";
                }
                else
                {
                    query = @"
                        SELECT UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE RoleId = 2 AND Location = @Location";
                }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Location", locationId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new User
                                {
                                    UserID = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    RoleID = reader.GetInt32(3),
                                    Location = reader.GetInt32(4),
                                    CreatedDate = reader.GetDateTime(5),
                                    SalesmanID = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                                });
                            }
                        }
                    }
            }

            return users;
        }

        private List<SalesTask> GetAllTasksByLocation(int locationId)
        {
            string connectionString = _configuration.GetConnectionString("SalesMetrics");
            var tasks = new List<SalesTask>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(@"
                        SELECT TaskID,
                               Title,
                               Description,
                               DueDate,
                               Status,
                               AssignedTo,
                               Location,
                               Property,
                               Type,
                               CreatedBy,
                               CreatedDate,
                               ModifiedDate,
                               CompletedDate,
                               CancelledDate
                        FROM Tasks
                        WHERE Location = @LocationId
                            AND Status != 'Deleted'
                    ", conn);

                    cmd.Parameters.AddWithValue("@LocationId", locationId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var task = new SalesTask
                            {
                                TaskID = reader.GetInt32(reader.GetOrdinal("TaskID")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
                                DueDate = reader.GetDateTime(reader.GetOrdinal("DueDate")),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                AssignedTo = reader.GetInt32(reader.GetOrdinal("AssignedTo")),
                                Location = reader.GetInt32(reader.GetOrdinal("Location")),
                                Property = reader.IsDBNull(reader.GetOrdinal("Property")) ? "" : reader.GetString(reader.GetOrdinal("Property")),
                                Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? "" : reader.GetString(reader.GetOrdinal("Type")),
                                CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),
                                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ModifiedDate")),
                                CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                                CancelledDate = reader.IsDBNull(reader.GetOrdinal("CancelledDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CancelledDate"))
                            };

                            tasks.Add(task);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving tasks: " + ex.Message);
                // You could also log this exception or rethrow it as needed
            }

            return tasks;
        }

        private List<SalesTask> GetTasksByUserId(int userId, int locationId)
        {
            string connectionString = _configuration.GetConnectionString("SalesMetrics");
            var tasks = new List<SalesTask>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(@"
                        SELECT TaskID,
                               Title,
                               Description,
                               DueDate,
                               Status,
                               AssignedTo,
                               Location,
                               Property,
                               Type,
                               CreatedBy,
                               CreatedDate,
                               ModifiedDate,
                               CompletedDate,
                               CancelledDate
                        FROM Tasks
                        WHERE AssignedTo = @UserId
                            AND Location = @LocationId
                            AND Status != 'Deleted'
                    ", conn);

                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@LocationId", locationId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var task = new SalesTask
                            {
                                TaskID = reader.GetInt32(reader.GetOrdinal("TaskID")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
                                DueDate = reader.GetDateTime(reader.GetOrdinal("DueDate")),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                AssignedTo = reader.GetInt32(reader.GetOrdinal("AssignedTo")),
                                Location = reader.GetInt32(reader.GetOrdinal("Location")),
                                Property = reader.IsDBNull(reader.GetOrdinal("Property")) ? "" : reader.GetString(reader.GetOrdinal("Property")),
                                Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? "" : reader.GetString(reader.GetOrdinal("Type")),
                                CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy")),
                                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ModifiedDate")),
                                CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                                CancelledDate = reader.IsDBNull(reader.GetOrdinal("CancelledDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CancelledDate"))
                            };

                            tasks.Add(task);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving tasks: " + ex.Message);
                // You could also log this exception or rethrow it as needed
            }

            return tasks;
        }

        private string GetTaskTitle(SalesTask task, List<User> users, int roleId)
        {
            var assignedUser = users.FirstOrDefault(u => u.UserID == task.AssignedTo);

            if (roleId == 1 || roleId == 3) // Admin or Sales Admin
            {
                return (assignedUser != null ? assignedUser.NameandInitial() + " - " : "") + task.Title;
            }
            else
            {
                return task.Title;
            }
        }

        private int SaveTaskToDatabase(SalesTask task)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
                {
                    conn.Open();
                    var query = @"
                        INSERT INTO Tasks 
                            (Title, Description, DueDate, Status, AssignedTo, Location, Property, Type, CreatedBy, CreatedDate, ModifiedDate, CompletedDate, CancelledDate, RoleID, PropertyID) 
                        OUTPUT INSERTED.TaskID
                        VALUES 
                            (@Title, @Description, @DueDate, @Status, @AssignedTo, @Location, @Property, @Type, @CreatedBy, @CreatedDate, @ModifiedDate, @CompletedDate, @CancelledDate, @RoleId, @PropertyId)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", task.Title ?? "");
                        cmd.Parameters.AddWithValue("@Description", task.Description ?? "");
                        cmd.Parameters.AddWithValue("@DueDate", task.DueDate);
                        cmd.Parameters.AddWithValue("@Status", task.Status ?? "Pending");
                        cmd.Parameters.AddWithValue("@AssignedTo", task.AssignedTo);
                        cmd.Parameters.AddWithValue("@Location", task.Location);  // Ensure this is set somewhere
                        cmd.Parameters.AddWithValue("@Property", task.Property ?? "");
                        cmd.Parameters.AddWithValue("@Type", task.Type ?? "");
                        cmd.Parameters.AddWithValue("@CreatedBy", task.CreatedBy ?? "System");
                        cmd.Parameters.AddWithValue("@CreatedDate", task.CreatedDate == default ? DateTime.Now : task.CreatedDate);
                        cmd.Parameters.AddWithValue("@ModifiedDate", (object?)task.ModifiedDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CompletedDate", (object?)task.CompletedDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@CancelledDate", (object?)task.CancelledDate ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@RoleId", (object?)task.RoleId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PropertyId", (object?)task.PropertyID ?? DBNull.Value);

                        //cmd.ExecuteNonQuery();

                        return (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] Failed to insert task: {ex.Message}");
                throw;
            }
        }

        [HttpGet]
        public IActionResult GetCreateTaskModal(string source = "Task")
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));

            var users = GetActiveUsers(); // This method is already defined in your controller

            var modalVM = new TaskModalViewModel
            {
                Task = new TaskCreateViewModel(),
                Users = users,
                LoggedInUserId = userId,
                RoleId = roleId,
                TaskTypes = TaskTypeHelper.GetTaskTypes(source)
            };

            return PartialView("_CreateTaskModal", modalVM);
        }
        
        [HttpGet]
        public IActionResult EditModalPartial(int id, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Task", "Tasks");

            int userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            int locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));
            SalesTask? task;

            if (roleId == 1 || roleId == 3)
            {
                // Admins and Sales Admins can see all tasks by location
                task = GetAllTasksByLocation(locationId).FirstOrDefault(t => t.TaskID == id);
            }
            else
            {
                // Sales users only see their own tasks
                task = GetTasksByUserId(userId, locationId).FirstOrDefault(t => t.TaskID == id);
            }

            if (task == null)
                return Content("Task not found.");

            //return PartialView("_EditTaskModalPartial", task);
            return PartialView("_EditTaskModalPartial", new TaskModalViewModel
            {
                Task = new TaskCreateViewModel
                {
                    Title = task.Title,
                    Description = task.Description,
                    DueDate = task.DueDate ?? DateTime.Now,
                    Status = task.Status,
                    Property = task.Property,
                    AssignedTo = task.AssignedTo ?? 0,
                    Type = task.Type,
                    TaskID = task.TaskID
                },
                Users = GetActiveUsers(),
                LoggedInUserId = userId,
                RoleId = roleId,
                TaskTypes = TaskTypeHelper.GetTaskTypes("Task")
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateFromAdmin(TaskModalViewModel modal)
        {
            string connStr = _configuration.GetConnectionString("SalesMetrics");
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));

            var updatedTask = modal.Task;

            try
            {
                if (!string.IsNullOrEmpty(updatedTask.DueDateDate) && !string.IsNullOrEmpty(updatedTask.DueDateTime))
                {
                    var combinedString = $"{updatedTask.DueDateDate} {updatedTask.DueDateTime}";
                    if (DateTime.TryParse(combinedString, out var parsedDate))
                    {
                        updatedTask.DueDate = parsedDate;
                    }
                    else
                    {
                        TempData["Error"] = "Invalid date/time provided.";
                        return RedirectToAction("AdminTask");
                    }
                }
            }
            catch
            {
                updatedTask.DueDate = DateTime.Now;
            }


            var returnUrl = Request.Form["ReturnUrl"].ToString();

            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string query = @"
                        UPDATE Tasks SET
                            Title = @Title,
                            Description = @Description,
                            DueDate = @DueDate,
                            Status = @Status,
                            Property = @Property,
                            Type = @Type,
                            AssignedTo = @AssignedTo,
                            ModifiedDate = @ModifiedDate
                        WHERE TaskID = @TaskID;

                        IF @Status = 'Completed'
                            UPDATE Tasks SET CompletedDate = @ModifiedDate WHERE TaskID = @TaskID;

                        IF @Status = 'Cancelled'
                            UPDATE Tasks SET CancelledDate = @ModifiedDate WHERE TaskID = @TaskID;
                    ";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", updatedTask.Title ?? "");
                        cmd.Parameters.AddWithValue("@Description", updatedTask.Description ?? "");
                        cmd.Parameters.AddWithValue("@DueDate", updatedTask.DueDate);

                        cmd.Parameters.AddWithValue("@Status", updatedTask.Status ?? "Pending");
                        cmd.Parameters.AddWithValue("@Property", updatedTask.Property ?? "");
                        cmd.Parameters.AddWithValue("@Type", updatedTask.Type ?? "");
                        if (updatedTask.AssignedTo == 0)
                        {
                            TempData["Error"] = "Please select a valid user to assign the task.";

                            if (!string.IsNullOrEmpty(returnUrl))
                                return RedirectToAction("AdminTask");
                            else
                                return RedirectToAction("Task");
                        }
                        cmd.Parameters.AddWithValue("@AssignedTo", updatedTask.AssignedTo);
                        cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@TaskID", updatedTask.TaskID);

                        cmd.ExecuteNonQuery();
                    }

                    // Pull Google token info
                    var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE UserID = @UserID", conn);
                    tokenCmd.Parameters.AddWithValue("@UserID", userId);
                    using var reader = tokenCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        var accessToken = reader["GoogleAccessToken"]?.ToString();
                        var refreshToken = reader["GoogleRefreshToken"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken))
                        {
                            var calendarService = new GoogleCalendarService(_configuration);

                            // NOTE: You need to store & retrieve GoogleEventId in Tasks table for accurate updates/deletes
                            // Assuming you're doing that and it’s mapped to the TaskID for now:
                            await calendarService.UpdateTaskEventAsync(userId, accessToken, refreshToken, updatedTask.TaskID.ToString(), updatedTask.Title, updatedTask.Description, updatedTask.DueDate);
                        }
                    }
                }

                // update logic
                TempData["Success"] = "Task updated from Admin.";
                //TempData["Success"] = "Task updated and synced with Google Calendar.";

            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating task: " + ex.Message;
            }

            if (!string.IsNullOrEmpty(returnUrl))
            {
                if (returnUrl.Contains("/Task")) { return RedirectToAction("Task"); }
                else if (returnUrl.Contains("/AdminTask")) { return Redirect("AdminTask"); }
                else if (returnUrl.Contains("/Properties")) { return Redirect("/Sales/Properties"); }
                else if (returnUrl.Contains("/Yardi")) { return Redirect("/Yardi/YardiProperties"); }
                else return RedirectToAction("AdminTask");
            }
            else
                return RedirectToAction("AdminTask");

        }

        [HttpPost]
        public async Task<IActionResult> Update(TaskModalViewModel modal)
        {
            string connStr = _configuration.GetConnectionString("SalesMetrics");
            
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));

            var updatedTask = modal.Task;

            try
            {
                if (!string.IsNullOrEmpty(updatedTask.DueDateDate) && !string.IsNullOrEmpty(updatedTask.DueDateTime))
                {
                    var combinedString = $"{updatedTask.DueDateDate} {updatedTask.DueDateTime}";
                    if (DateTime.TryParse(combinedString, out var parsedDate))
                    {
                        updatedTask.DueDate = parsedDate;
                    }
                    else
                    {
                        TempData["Error"] = "Invalid date/time provided.";
                        return RedirectToAction("AdminTask");
                    }
                }
            }
            catch
            {
                updatedTask.DueDate = DateTime.Now;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string query = @"
                        UPDATE Tasks 
                        SET
                            Title = @Title,
                            Description = @Description,
                            DueDate = @DueDate,
                            Status = @Status,
                            Property = @Property,
                            Type = @Type,
                            ModifiedDate = @ModifiedDate,
                            AssignedTo = @AssignedTo
                        WHERE TaskID = @TaskID;

                        IF @Status = 'Completed'
                            UPDATE Tasks SET CompletedDate = @ModifiedDate WHERE TaskID = @TaskID;

                        IF @Status = 'Cancelled'
                            UPDATE Tasks SET CancelledDate = @ModifiedDate WHERE TaskID = @TaskID;
                    ";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", updatedTask.Title ?? "");
                        cmd.Parameters.AddWithValue("@Description", updatedTask.Description ?? "");
                        cmd.Parameters.AddWithValue("@DueDate", updatedTask.DueDate);
                        cmd.Parameters.AddWithValue("@Status", updatedTask.Status ?? "Pending");
                        cmd.Parameters.AddWithValue("@Property", updatedTask.Property ?? "");
                        cmd.Parameters.AddWithValue("@Type", updatedTask.Type ?? "");
                        cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@AssignedTo", updatedTask.AssignedTo);
                        cmd.Parameters.AddWithValue("@TaskID", updatedTask.TaskID);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            Console.WriteLine($"[WARNING] No rows updated. TaskID = {updatedTask.TaskID}");
                            TempData["Error"] = "Update failed. Task not found or no changes applied.";
                            return RedirectToAction("Task");
                        }
                        //cmd.ExecuteNonQuery();
                    }

                    // Pull Google token info
                    var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE UserID = @UserID", conn);
                    tokenCmd.Parameters.AddWithValue("@UserID", userId);
                    using var reader = tokenCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        var accessToken = reader["GoogleAccessToken"]?.ToString();
                        var refreshToken = reader["GoogleRefreshToken"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken))
                        {
                            var calendarService = new GoogleCalendarService(_configuration);

                            // NOTE: You need to store & retrieve GoogleEventId in Tasks table for accurate updates/deletes
                            // Assuming you're doing that and it’s mapped to the TaskID for now:
                            await calendarService.UpdateTaskEventAsync(userId, accessToken, refreshToken, updatedTask.TaskID.ToString(), updatedTask.Title, updatedTask.Description, updatedTask.DueDate);
                        }
                    }
                }

                TempData["Success"] = $"Task {updatedTask.TaskID.ToString()}  |  {updatedTask.Title.ToString()} | has been updated.";

            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating task: " + ex.Message;
            }

            // fallback
            return RedirectToAction("Task");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int taskId)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
                await conn.OpenAsync();

                // Get the GoogleAccessToken and RefreshToken
                var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE UserID = @UserID and Location = @Location", conn);
                tokenCmd.Parameters.AddWithValue("@UserID", userId);
                tokenCmd.Parameters.AddWithValue("@Location", locationId);

                string? accessToken = null, refreshToken = null;
                using (var reader = await tokenCmd.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        accessToken = reader["GoogleAccessToken"]?.ToString();
                        refreshToken = reader["GoogleRefreshToken"]?.ToString();
                    }
                }

                // Delete the Google Calendar event first
                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    var calendarService = new GoogleCalendarService(_configuration);
                    await calendarService.DeleteTaskEventAsync(userId, accessToken, refreshToken, taskId.ToString());
                }

                // Delete from DB
                //var deleteCmd = new SqlCommand("DELETE FROM Tasks WHERE TaskID = @TaskID", conn);
                var deleteCmd = new SqlCommand(@"
                    UPDATE Tasks
                    SET Status = 'Deleted',
                        ModifiedDate = @ModifiedDate
                    WHERE TaskID = @TaskID
                ", conn);
                deleteCmd.Parameters.AddWithValue("@TaskID", taskId);
                deleteCmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
                await deleteCmd.ExecuteNonQueryAsync();

                TempData["Success"] = "Task has been deleted as requested.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting task: {ex.Message}";
            }

            return RedirectToAction("Task");
        }

        [HttpPost]
        public JsonResult UpdateStatus([FromBody] StatusUpdateModel model)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
                {
                    conn.Open();

                    string query = @"UPDATE Tasks 
                             SET Status = @Status, 
                                 ModifiedDate = @ModifiedDate 
                             WHERE TaskID = @TaskID;

                             IF @Status = 'Completed'
                                 UPDATE Tasks SET CompletedDate = @ModifiedDate WHERE TaskID = @TaskID;

                             IF @Status = 'Cancelled'
                                 UPDATE Tasks SET CancelledDate = @ModifiedDate WHERE TaskID = @TaskID;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", model.NewStatus);
                        cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@TaskID", model.TaskId);
                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateFromYardi(TaskModalViewModel modal)
        {
            var model = modal.Task;

            if (model == null || string.IsNullOrWhiteSpace(model.Property))
            {
                TempData["Error"] = "Invalid property info.";
                return RedirectToAction("YardiProperties", "Yardi");
            }

            Console.WriteLine($"[DEBUG] Incoming Task.AssignedTo = {modal.Task?.AssignedTo}");
            Console.WriteLine($"[DEBUG] ModelState.IsValid = {ModelState.IsValid}");

            // ✅ Convert TaskCreateViewModel ➜ TaskEntity
            var task = new TaskEntity
            {
                Title = string.IsNullOrWhiteSpace(model.Title) ? $"Follow up for {model.Property}" : model.Title,
                Description = model.Description ?? "Yardi Matrix Related Task",
                DueDate = model.DueDate == default ? DateTime.Now.AddDays(1) : model.DueDate,
                PropertyID = model.PropertyID,
                Property = model.Property,
                Status = string.IsNullOrWhiteSpace(model.Status) ? "Pending" : model.Status,
                Type = model.Type ?? "Follow Up",
                CreatedBy = HttpContext.Session.GetString("Username") ?? "system",
                CreatedDate = DateTime.Now
            };

            // Handle nullable session-based properties
            if (int.TryParse(HttpContext.Session.GetString("LocationId"), out var locId))
                task.Location = locId;

            var roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));

            if (roleId == 1 || roleId == 3)
            {
                if (model.AssignedTo == null || model.AssignedTo == 0)
                {
                    TempData["Error"] = "Please select a valid user to assign the task.";
                    return RedirectToAction("YardiProperties", "Yardi");
                }

                task.AssignedTo = model.AssignedTo;
            }
            else
            {
                if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var assignedTo))
                {
                    TempData["Error"] = "Unable to determine assigned user.";
                    return RedirectToAction("Index", "Home");
                }

                task.AssignedTo = assignedTo;
            }


            Console.WriteLine($"[DEBUG] Creating Yardi Task: PropertyID = {task.PropertyID}, Property = {task.Property}");

            _context.Tasks.Add(task);
            _context.SaveChanges();

            TempData["Success"] = $"Task created for {model.Property}";
            return RedirectToAction("YardiProperties", "Yardi");
        }

        [HttpPost]
        public IActionResult CreateFromProperties(TaskModalViewModel modal)
        {
            var model = modal.Task;

            if (!string.IsNullOrEmpty(model.DueDateDate) && !string.IsNullOrEmpty(model.DueDateTime))
            {
                var combined = $"{model.DueDateDate} {model.DueDateTime}";
                if (DateTime.TryParse(combined, out var parsed))
                {
                    model.DueDate = parsed;
                }
                else
                {
                    ModelState.AddModelError("DueDate", "Invalid date/time.");
                }
            }
            else
            {
                ModelState.AddModelError("DueDate", "Date and time are required.");
            }


            if (model == null || string.IsNullOrWhiteSpace(model.Property))
            {
                TempData["Error"] = "Invalid property info.";
                return RedirectToAction("YardiProperties", "Yardi");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Form is missing required fields.";
                return RedirectToAction("YardiProperties", "Yardi");
            }

            try
            {
                int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
                int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

                // ✅ Convert TaskCreateViewModel ➜ TaskEntity
                var task = new SalesTask
                {
                    Title = string.IsNullOrWhiteSpace(model.Title) ? $"Follow up for {model.Property}" : model.Title,
                    Description = model.Description ?? "ERP Property Task",
                    DueDate = model.DueDate == default ? DateTime.Now.AddDays(1) : model.DueDate,
                    PropertyID = model.PropertyID,
                    Property = model.Property,
                    Status = string.IsNullOrWhiteSpace(model.Status) ? "Pending" : model.Status,
                    Type = model.Type ?? "Follow Up",
                    CreatedBy = HttpContext.Session.GetString("Username") ?? "system",
                    CreatedDate = DateTime.Now,
                    Location = int.TryParse(HttpContext.Session.GetString("LocationId"), out var locId) ? locId : 0,
                    AssignedTo = (roleId == 1 || roleId == 3) ? model.AssignedTo : Convert.ToInt32(HttpContext.Session.GetString("UserId"))
                };

                // Handle nullable session-based properties
                //if (int.TryParse(HttpContext.Session.GetString("LocationId"), out var locId))
                //    task.Location = locId;

                if (roleId == 1 || roleId == 3)
                {
                    task.AssignedTo = (int)model.AssignedTo;
                }
                else
                {
                    // fallback for Sales role (2) using hidden input
                    if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var assignedTo))
                    {
                        TempData["Error"] = "Unable to determine assigned user.";
                        return RedirectToAction("Login", "Auth");
                    }
                    task.AssignedTo = assignedTo;
                }

                //_context.Tasks.Add(task);
                //_context.SaveChanges();
                int TaskID = SaveTaskToDatabase(task);

                TempData["Success"] = $"Task created successfully for {task.Property} | Task ID: {TaskID}";
                return RedirectToAction("Properties", "Sales");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating task: {ex.Message}");
                TempData["Error"] = $"Error creating Task for {model.Property}";
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromWorkOrder(TaskCreateViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Property))
            {
                TempData["Error"] = "Missing task data.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // Pull values from session
                var username = HttpContext.Session.GetString("Username") ?? "system";
                var userIdStr = HttpContext.Session.GetString("UserId");
                var roleIdStr = HttpContext.Session.GetString("RoleId");
                var locationIdStr = HttpContext.Session.GetString("LocationId");

                if (!int.TryParse(userIdStr, out int sessionUserId) ||
                    !int.TryParse(roleIdStr, out int roleId) ||
                    !int.TryParse(locationIdStr, out int locationId))
                {
                    return BadRequest("Session data missing or invalid.");
                }

                // Determine AssignedTo
                Console.WriteLine("AssignedTo in CreateFromWorkOrder: " + model.AssignedTo);

                int assignedTo = ((roleId == 1 || roleId == 3) ? model.AssignedTo : sessionUserId);

                if (assignedTo == 0)
                {
                    return BadRequest("Invalid AssignedTo value.");
                }

                var task = new SalesTask
                {
                    Title = string.IsNullOrWhiteSpace(model.Title) ? $"Site Visit for {model.Property}" : model.Title,
                    Description = string.IsNullOrWhiteSpace(model.Description) ? "Follow-up visit scheduled." : model.Description,
                    DueDate = model.DueDate == default ? DateTime.Now.AddDays(1) : model.DueDate,
                    PropertyID = model.PropertyID,
                    Property = model.Property,
                    Type = model.Type ?? "Site Visit",
                    Status = model.Status ?? "Pending",
                    CreatedBy = username,
                    CreatedDate = DateTime.Now,
                    AssignedTo = assignedTo,
                    Location = locationId
                };

                int TaskID = SaveTaskToDatabase(task);

                TempData["Success"] = $"Task created successfully. Task ID: {TaskID}";
                return RedirectToAction("Index", "Dashboard");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating task: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDueDate([FromBody] Calendar.CalendarUpdateModel model)
        {
            try
            {
                using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
                {
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("UPDATE Tasks SET DueDate = @DueDate, ModifiedDate = @ModifiedDate WHERE TaskID = @TaskId", conn);
                    cmd.Parameters.AddWithValue("@DueDate", model.NewDate);
                    cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@TaskId", model.TaskId);

                    var result = await cmd.ExecuteNonQueryAsync();
                    return Json(new { success = result > 0 });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
                
        [HttpGet]
        public JsonResult GetCalendarEvents(bool hideCompleted = false)
        {
            int userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int locationId = int.Parse(User.FindFirst("LocationId")?.Value ?? "0");

            List<SalesTask> tasks;
            List<User> users;

            if (roleId == 1 || roleId == 3)  // Admin or Sales Admin
            {
                tasks = GetAllTasksByLocation(locationId);
                users = GetAllUsersForTaskDisplay(locationId, roleId);
            }
            else
            {
                tasks = GetTasksByUserId(userId, locationId);
                users = GetActiveUsers();
            }

            // 🧹 If requested, filter out completed tasks
            if (hideCompleted)
            {
                tasks = tasks.Where(t => t.Status != "Completed").ToList();
            }

            var calendarTasks = tasks.Select(t => new
            {
                id = t.TaskID,
                title = GetTaskTitle(t, users, roleId),
                start = t.DueDate?.ToString("yyyy-MM-ddTHH:mm:ss"),
                allDay = false,
                className = t.Status == "In Progress" ? "bg-inprogress" :
                            t.Status == "Pending" ? "bg-pending" :
                            t.Status == "Completed" ? "bg-completed" :
                            "bg-primary",
                extendedProps = new
                {
                    type = t.Type,
                    status = t.Status,
                    property = t.Property,
                    assignedTo = users.FirstOrDefault(u => u.UserID == t.AssignedTo)?.FullName() ?? "Unassigned",
                    description = t.Description
                }
            }).ToList();

            return Json(calendarTasks);
        }

        [HttpGet]
        public JsonResult GetNotesForTask(int taskId)
        {
            var notes = new List<TaskNote>();

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM TaskNotes WHERE TaskID = @TaskID ORDER BY CreatedDate DESC", conn);
                cmd.Parameters.AddWithValue("@TaskID", taskId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        notes.Add(new TaskNote
                        {
                            NoteID = reader.GetInt32(0),
                            TaskID = reader.GetInt32(1),
                            NoteText = reader.GetString(2),
                            CreatedBy = reader.GetString(3),
                            CreatedDate = reader.GetDateTime(4)
                        });
                    }
                }
            }

            return Json(notes);
        }

        [HttpPost]
        public IActionResult AddNoteToTask(int taskId, string noteText)
        {
            string createdBy = HttpContext.Session.GetString("Username") ?? "system";

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand("INSERT INTO TaskNotes (TaskID, NoteText, CreatedBy) VALUES (@TaskID, @NoteText, @CreatedBy)", conn);
                cmd.Parameters.AddWithValue("@TaskID", taskId);
                cmd.Parameters.AddWithValue("@NoteText", noteText);
                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                cmd.ExecuteNonQuery();
            }

            return Ok();
        }

        // END CLASS
    }
}