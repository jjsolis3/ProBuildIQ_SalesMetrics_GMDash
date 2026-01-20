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
using SalesMetrics.Services.Helpers;
using SalesMetrics.Services.Notifications;

using Google.Apis.Tasks.v1.Data;
using System.Diagnostics.CodeAnalysis;
using Azure.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Data;
using System.Threading.Tasks;
using Google.Apis.Gmail.v1.Data;
using Microsoft.Extensions.Caching.Memory;


namespace SalesMetrics.Controllers
{

    public class TasksController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly SalesMetricsDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IMemoryCache _cache;

        public TasksController(IConfiguration configuration, SalesMetricsDbContext context, INotificationService notificationService, IMemoryCache cache)
        {
            _configuration = configuration;
            _context = context;
            _notificationService = notificationService;
            _cache = cache;
        }

        private UserContext GetUserContext()
        {
            return new UserContext
            {
                Users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0"),
                UserId = int.Parse(User.FindFirst("UserId")?.Value ?? "0"),
                RoleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0"),
                SalesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0"),
                LocationId = LocationHelper.GetCurrentLocationId(HttpContext),
                Username = HttpContext.Session.GetString("Username") ?? "system",
                FullName = HttpContext.Session.GetString("FullName") ?? ""
            };
        }
        
        private void SetUserViewBags(UserContext user)
        {
            ViewBag.Users_Id = user.Users_Id;
            ViewBag.UserId = user.UserId;
            ViewBag.RoleId = user.RoleId;
            ViewBag.SalesmanId = user.SalesmanId;
            ViewBag.LocationId = user.LocationId;
        }

        private List<SalesTask> FilterTasks(List<SalesTask> tasks, string filter)
        {
            return filter switch
            {
                "active" => tasks.Where(t => t.Status != "Completed" && t.Status != "Deleted").ToList(),
                "completed" => tasks.Where(t => t.Status == "Completed").ToList(),
                _ => tasks
            };
        }

        private DateTime? ParseDueDate(TaskCreateViewModel model)
        {
            if (!string.IsNullOrEmpty(model.DueDateDate) && !string.IsNullOrEmpty(model.DueDateTime))
            {
                var combined = $"{model.DueDateDate} {model.DueDateTime}";
                return DateTime.TryParse(combined, out var parsed) ? parsed : null;
            }
            return null;
        }

        public IActionResult AdminTask(string filter = "all")
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            // SERVER-SIDE PAGINATION: Don't load tasks here, they'll be loaded via AJAX
            // Only load users for the task creation form
            var users = GetCachedUsers(Convert.ToInt32(locationId), roleId);

            var viewModel = new TaskPageViewModel
            {
                Tasks = new List<SalesTask>(), // Empty list - tasks loaded via AJAX
                Users = users,
                NewTask = new SalesTask
                {
                    DueDate = DateTime.Now.AddHours(2) // ⏰ 2 hours from now
                }
            };

            ViewBag.Users_Id = users_Id;
            ViewBag.UserId = userId;
            ViewBag.RoleId = roleId;
            ViewBag.LocationId = locationId;
            ViewBag.SalesmanId = salesmanId;
            ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("AdminTask");
            ViewBag.CurrentFilter = filter; // Pass filter to view

            // Add ReturnUrl ViewBag
            ViewBag.ReturnUrl = Url.Action("AdminTask", "Tasks", new { filter });

            return View(viewModel);
        }

        /// <summary>
        /// Server-side DataTables endpoint for AdminTask page with pagination
        /// </summary>
        [HttpPost]
        public IActionResult GetAdminTasksData(string filter = "all")
        {
            try
            {
                // Get current user context
                int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
                int locationId = LocationHelper.GetCurrentLocationId(HttpContext);

                // Get DataTables parameters from request
                var draw = HttpContext.Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                var sortColumn = Request.Form["order[0][column]"].FirstOrDefault();
                var sortDirection = Request.Form["order[0][dir]"].FirstOrDefault();

                int pageSize = length != null ? Convert.ToInt32(length) : 10;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                // Get cached users for lookup
                var users = GetCachedUsers(locationId, roleId);
                var userLookup = users.ToDictionary(u => u.Users_ID, u => $"{u.FirstName} {u.LastName}");

                // Get paginated tasks
                var (tasks, totalRecords, filteredRecords) = GetPaginatedTasksByLocation(
                    locationId, filter, skip, pageSize, searchValue, sortColumn, sortDirection, userLookup);

                // Format data for DataTables
                var data = tasks.Select(task => new
                {
                    taskId = task.TaskID,
                    title = task.Title ?? "",
                    description = task.Description ?? "",
                    status = task.Status ?? "",
                    dueDate = task.DueDate?.ToString("MM/dd/yyyy hh:mm tt") ?? "",
                    assignedTo = userLookup.ContainsKey(task.AssignedTo) ? userLookup[task.AssignedTo] : "Unassigned",
                    assignedToId = task.AssignedTo,
                    property = task.Property ?? "",
                    type = task.Type ?? "",
                    createdDate = task.CreatedDate?.ToString("MM/dd/yyyy hh:mm tt") ?? ""
                }).ToList();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = filteredRecords,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = 0,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        public IActionResult Task(string filter = "all")
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");

            var tasks = GetTasksByUserId(users_Id, locationId);

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
            ViewBag.Users_Id = users_Id;
            ViewBag.UserId = userId;
            ViewBag.RoleId = roleId;
            ViewBag.SalesmanId = salesmanId;
            ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("Task");

            // Add ReturnUrl ViewBag
            ViewBag.ReturnUrl = Url.Action("Task", "Tasks", new { filter });

            return View(viewModel);
        }

        public IActionResult Schedule()
        {
            int userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            
            List<SalesTask> tasks;
            List<User> users;

            // Management roles can see all tasks in their location
            if (RoleHelper.CanManageLocationTasks(roleId))
            {
                tasks = GetAllTasksByLocation(locationId);
                users = GetAllUsersForTaskDisplay(locationId, roleId);

                // 💬 Filter users who have at least one task
                var usersWithTasks = users
                    .Where(u => tasks.Any(t => t.AssignedTo == u.Users_ID))
                    .ToList();

                ViewBag.FilterUsers = usersWithTasks;
                ViewBag.AllUsers = users;
            }
            else
            {
                tasks = GetTasksByUserId(users_Id, locationId);
                users = GetActiveUsers(); // just for modal use

                ViewBag.FilterUsers = users; // For regular users, just themselves
            }

            //methods to get user list
            ViewBag.RoleId = roleId;
            ViewBag.UserId = userId;
            ViewBag.Users_Id = users_Id;
            ViewBag.LocationId = locationId;

            var calendarTasks = tasks.Select(t => new TaskCalendarViewModel
            {
                TaskId = t.TaskID,
                Title = GetTaskTitle(t, users, roleId),
                Type = t.Type,
                Status = t.Status,
                Property = t.Property,
                Description = t.Description,
                DueDate = t.DueDate,
                AssignedTo = users.FirstOrDefault(u => u.Users_ID == t.AssignedTo) is var u && u != null ? $"{u.FirstName} {u.LastName}" : "Unassigned"
            }).ToList();

            // Add ReturnUrl ViewBag
            ViewBag.ReturnUrl = Url.Action("Schedule", "Tasks");

            return View(calendarTasks); // Updated model
        }

        public IActionResult TaskKanban()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
            int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);

            // In Task() action and any other that renders the task modal:
            ViewBag.UserId = userId;
            ViewBag.Users_Id = users_Id;
            ViewBag.RoleId = roleId;
            ViewBag.SalesmanId = salesmanId;

            var tasks = GetTasksByUserId(Convert.ToInt32(users_Id), locationId);
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

        public UserContext GetAssignedUserContext(int? assignedTo)
        {
            var loggedIn = GetUserContext();

            // Sales can't assign to others, so return themselves
            if (loggedIn.RoleId == 2 || assignedTo == null || assignedTo == 0 || assignedTo == loggedIn.Users_Id)
                return loggedIn;

            var assigned = GetUserData((int)assignedTo, loggedIn.LocationId).FirstOrDefault();
            if (assigned == null)
                return loggedIn;

            return new UserContext
            {
                Users_Id = assigned.Users_ID,
                UserId = assigned.UserID,
                RoleId = assigned.RoleID,
                SalesmanId = assigned.SalesmanID,
                LocationId = assigned.Location,
                Username = assigned.Username
            };
        }

        // Add methods like Create, Update Status, Delete, etc
        [HttpPost]
        public async Task<IActionResult> Create(TaskModalViewModel modal, string? returnUrl)
        {
            modal.TaskTypes = TaskTypeHelper.GetTaskTypes("Task"); // 👈 Key line
            var model = modal.Task;

            var user = GetUserContext();
            var assignedUser = GetAssignedUserContext(model.AssignedTo);

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
                    Tasks = GetTasksByUserId(assignedUser.Users_Id, assignedUser.LocationId),
                    Users = GetActiveUsers(),
                    NewTask = new SalesTask
                    {
                        Title = model.Title,
                        Description = model.Description,
                        DueDate = model.DueDate,
                        Status = model.Status,
                        AssignedTo = assignedUser.Users_Id,
                        Property = model.Property,
                        Type = model.Type,
                        PropertyID = model.PropertyID,
                        Location = assignedUser.LocationId,
                        RoleId = assignedUser.RoleId
                    }
                };

                SetUserViewBags(assignedUser);

                ViewBag.TaskTypes = TaskTypeHelper.GetTaskTypes("Task");
                ViewBag.LoggedInUserId = assignedUser.Users_Id;

                return View("Task", viewModel);
            }

            if (model.AssignedTo == 0)
            {
                model.AssignedTo = assignedUser.Users_Id;
            }

            var task = new SalesTask
            {
                Title = model.Title,
                Description = model.Description,
                DueDate = model.DueDate,
                Status = model.Status ?? "Pending",
                AssignedTo = model.AssignedTo,
                Property = model.Property,
                PropertyID = model.PropertyID,
                Type = model.Type,
                CreatedBy = GetUserContext().Username ?? "admin",
                CreatedDate = DateTime.Now,
                RoleId = assignedUser.RoleId,
                Location = assignedUser.LocationId,
                CreatedById = GetUserContext().Users_Id // Store the ID of the user who created the task
            };

            var locationName = LocationHelper.GetLocationName(assignedUser.LocationId);
            Console.WriteLine($"New Task Created for {assignedUser.FullName} (id: {model.AssignedTo} ) from the {locationName} branch");

            int taskId = SaveTaskToDatabase(task);
            task.TaskID = taskId;

            // Send notification to assigned user
            try
            {
                await _notificationService.NotifyTaskAssignedAsync(taskId, model.AssignedTo, user.Users_Id, task.Title ?? "New Task");
            }
            catch (Exception ex)
            {
                // Log error but don't fail task creation
                Console.WriteLine($"Error sending task assignment notification: {ex.Message}");
            }

            // Add to Google Task
            var (googleTaskId, googleEventId) = await TryGoogleSyncTaskAsync(task);

            if (!string.IsNullOrEmpty(googleTaskId) && !string.IsNullOrEmpty(googleEventId))
                TempData["Success"] = "Task created and synced to Google Task & Calendar.";
            else if (!string.IsNullOrEmpty(googleTaskId))
                TempData["Success"] = "Task created and synced to Google Task.";
            else if (!string.IsNullOrEmpty(googleEventId))
                TempData["Success"] = "Task created and synced to Google Calendar.";
            else
                TempData["Success"] = $"Task (ID:{task.TaskID}) has been created successfully.";

            //var returnUrl = Request.Form["ReturnUrl"].ToString();

            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Task"); // fallback just in case
            }
        }

        private List<User> GetUserData(int users_Id, int locationId)
        {
            var users = new List<User>();

            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                string query = @"
                    SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID, SalesmanNumber
                    FROM Users
                    WHERE Users_ID = @Users_Id AND Location = @Location and IsActive = 1";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Users_Id", users_Id);
                    cmd.Parameters.AddWithValue("@Location", locationId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                Users_ID = reader.GetInt32(reader.GetOrdinal("Users_ID")),
                                UserID = reader.GetInt32(reader.GetOrdinal("UserId")),
                                FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                                LastName = reader.GetString(reader.GetOrdinal("LastName")),
                                RoleID = reader.GetInt32(reader.GetOrdinal("RoleID")),
                                Location = reader.GetInt32(reader.GetOrdinal("Location")),
                                CreatedDate = reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                SalesmanID = reader.IsDBNull(reader.GetOrdinal("SalesmanID")) ? 0 : reader.GetInt32(reader.GetOrdinal("SalesmanID")),
                                SalesmanNumber = reader.IsDBNull(reader.GetOrdinal("SalesmanNumber")) ? null : reader.GetString(reader.GetOrdinal("SalesmanNumber"))
                            });
                        }
                    }
                }
            }
            return users;
        }

        private List<User> GetActiveUsers()
        {
            var user = GetUserContext();
            var users = new List<User>();

            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();

                string query;

                if (user.RoleId == 1)
                {
                    // Admins and Sales Admins see all users at the same location
                    query = @"SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Location = @Location";
                }
                else if (user.RoleId == 3 || user.RoleId == 4)
                {
                    // Admins and Sales Admins see all users at the same location
                    query = @"SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE RoleID in (2, 3) AND Location = @Location";
                }
                else
                {
                    // Sales reps only see themselves
                    query = @"SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Users_ID = @Users_Id and Location = @Location";
                }

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (user.RoleId != 3 || user.RoleId != 4)
                        cmd.Parameters.AddWithValue("@Users_Id", user.Users_Id);

                    cmd.Parameters.AddWithValue("@Location", user.LocationId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                Users_ID = reader.GetInt32(0), // Assuming UserID is the first column
                                UserID = reader.GetInt32(1),
                                FirstName = reader.GetString(2),
                                LastName = reader.GetString(3),
                                RoleID = reader.GetInt32(4),
                                Location = reader.GetInt32(5),
                                CreatedDate = reader.GetDateTime(6),
                                SalesmanID = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
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

                // Admin sees all users
                if (roleId == RoleHelper.ROLE_ADMIN)
                {
                    query = @"
                        SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Location = @Location";
                }
                // Management roles (President/Owner, Regional, GM, Sales Admin) see all users INCLUDING admins
                // This is for DISPLAY purposes - they need to see who tasks are assigned to, even if it's an admin
                else if (RoleHelper.CanManageLocationTasks(roleId))
                {
                    query = @"
                        SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
                        FROM Users
                        WHERE Location = @Location";
                }
                // Other roles see only salespeople
                else
                {
                    query = @"
                        SELECT Users_ID, UserID, FirstName, LastName, RoleID, Location, CreatedDate, SalesmanID
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
                                    Users_ID = reader.GetInt32(0),
                                    UserID = reader.GetInt32(1),
                                    FirstName = reader.GetString(2),
                                    LastName = reader.GetString(3),
                                    RoleID = reader.GetInt32(4),
                                    Location = reader.GetInt32(5),
                                    CreatedDate = reader.GetDateTime(6),
                                    SalesmanID = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                                });
                            }
                        }
                    }
            }

            return users;
        }

        private List<SalesTask> GetAllTasksByLocation(int locationId, string filter = "all")
        {
            string connectionString = _configuration.GetConnectionString("SalesMetrics");
            var tasks = new List<SalesTask>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // OPTIMIZATION: Build SQL filter dynamically to reduce data transfer
                    string statusFilter = filter switch
                    {
                        "active" => "AND Status != 'Completed' AND Status != 'Cancelled'",
                        "completed" => "AND Status = 'Completed'",
                        _ => "" // "all" - no additional filter
                    };

                    string query = $@"
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
                               CancelledDate,
                               IsSyncedToGoogle
                        FROM Tasks
                        WHERE Location = @LocationId
                            AND Status != 'Deleted'
                            {statusFilter}
                    ";

                    SqlCommand cmd = new SqlCommand(query, conn);
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
                                DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? DateTime.Today.AddDays(1) : reader.GetDateTime(reader.GetOrdinal("DueDate")),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                AssignedTo = reader.GetInt32(reader.GetOrdinal("AssignedTo")),
                                Location = reader.IsDBNull(reader.GetOrdinal("Location")) ? 0 :reader.GetInt32(reader.GetOrdinal("Location")),
                                Property = reader.IsDBNull(reader.GetOrdinal("Property")) ? "" : reader.GetString(reader.GetOrdinal("Property")),
                                Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? "" : reader.GetString(reader.GetOrdinal("Type")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? "" : reader.GetString(reader.GetOrdinal("CreatedBy")),
                                CreatedDate = reader.IsDBNull(reader.GetOrdinal("CreatedDate")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ModifiedDate")),
                                CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                                CancelledDate = reader.IsDBNull(reader.GetOrdinal("CancelledDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CancelledDate")),
                                IsSyncedToGoogle = reader.IsDBNull(reader.GetOrdinal("IsSyncedToGoogle")) ? false : reader.GetBoolean(reader.GetOrdinal("IsSyncedToGoogle"))
                            };

                            tasks.Add(task);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving tasks by Locations: " + ex.Message);
                // You could also log this exception or rethrow it as needed
            }

            return tasks;
        }

        /// <summary>
        /// Get paginated tasks with sorting and searching for DataTables server-side processing
        /// </summary>
        private (List<SalesTask> tasks, int totalRecords, int filteredRecords) GetPaginatedTasksByLocation(
            int locationId, string filter, int skip, int pageSize, string searchValue,
            string sortColumn, string sortDirection, Dictionary<int, string> userLookup)
        {
            string connectionString = _configuration.GetConnectionString("SalesMetrics");
            var tasks = new List<SalesTask>();
            int totalRecords = 0;
            int filteredRecords = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Build status filter
                    string statusFilter = filter switch
                    {
                        "active" => "AND Status != 'Completed' AND Status != 'Cancelled'",
                        "completed" => "AND Status = 'Completed'",
                        _ => "" // "all" - no additional filter
                    };

                    // Build search filter
                    string searchFilter = "";
                    if (!string.IsNullOrEmpty(searchValue))
                    {
                        searchFilter = @"AND (
                            Title LIKE @SearchValue OR
                            Description LIKE @SearchValue OR
                            Property LIKE @SearchValue OR
                            Type LIKE @SearchValue OR
                            Status LIKE @SearchValue
                        )";
                    }

                    // Map column index to column name for sorting
                    string orderByColumn = sortColumn switch
                    {
                        "0" => "TaskID",
                        "1" => "Title",
                        "2" => "Status",
                        "3" => "DueDate",
                        "4" => "AssignedTo",
                        "5" => "Property",
                        "6" => "Type",
                        _ => "DueDate" // Default sort by DueDate
                    };

                    string orderByDirection = sortDirection == "asc" ? "ASC" : "DESC";

                    // Get total count (before search filter)
                    string countQuery = $@"
                        SELECT COUNT(*)
                        FROM Tasks
                        WHERE Location = @LocationId
                            AND Status != 'Deleted'
                            {statusFilter}
                    ";

                    SqlCommand countCmd = new SqlCommand(countQuery, conn);
                    countCmd.Parameters.AddWithValue("@LocationId", locationId);
                    totalRecords = (int)countCmd.ExecuteScalar();

                    // Get filtered count (with search filter)
                    string filteredCountQuery = $@"
                        SELECT COUNT(*)
                        FROM Tasks
                        WHERE Location = @LocationId
                            AND Status != 'Deleted'
                            {statusFilter}
                            {searchFilter}
                    ";

                    SqlCommand filteredCountCmd = new SqlCommand(filteredCountQuery, conn);
                    filteredCountCmd.Parameters.AddWithValue("@LocationId", locationId);
                    if (!string.IsNullOrEmpty(searchValue))
                    {
                        filteredCountCmd.Parameters.AddWithValue("@SearchValue", $"%{searchValue}%");
                    }
                    filteredRecords = (int)filteredCountCmd.ExecuteScalar();

                    // Get paginated data with indexes
                    string dataQuery = $@"
                        SELECT TaskID, Title, Description, DueDate, Status, AssignedTo,
                               Location, Property, Type, CreatedBy, CreatedDate, ModifiedDate,
                               CompletedDate, CancelledDate, IsSyncedToGoogle
                        FROM Tasks WITH (INDEX(IX_Tasks_Location_Status))
                        WHERE Location = @LocationId
                            AND Status != 'Deleted'
                            {statusFilter}
                            {searchFilter}
                        ORDER BY {orderByColumn} {orderByDirection}
                        OFFSET @Skip ROWS
                        FETCH NEXT @PageSize ROWS ONLY
                    ";

                    SqlCommand dataCmd = new SqlCommand(dataQuery, conn);
                    dataCmd.Parameters.AddWithValue("@LocationId", locationId);
                    dataCmd.Parameters.AddWithValue("@Skip", skip);
                    dataCmd.Parameters.AddWithValue("@PageSize", pageSize);
                    if (!string.IsNullOrEmpty(searchValue))
                    {
                        dataCmd.Parameters.AddWithValue("@SearchValue", $"%{searchValue}%");
                    }

                    using (SqlDataReader reader = dataCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var task = new SalesTask
                            {
                                TaskID = reader.GetInt32(reader.GetOrdinal("TaskID")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
                                DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? DateTime.Today.AddDays(1) : reader.GetDateTime(reader.GetOrdinal("DueDate")),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                AssignedTo = reader.GetInt32(reader.GetOrdinal("AssignedTo")),
                                Location = reader.IsDBNull(reader.GetOrdinal("Location")) ? 0 : reader.GetInt32(reader.GetOrdinal("Location")),
                                Property = reader.IsDBNull(reader.GetOrdinal("Property")) ? "" : reader.GetString(reader.GetOrdinal("Property")),
                                Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? "" : reader.GetString(reader.GetOrdinal("Type")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? "" : reader.GetString(reader.GetOrdinal("CreatedBy")),
                                CreatedDate = reader.IsDBNull(reader.GetOrdinal("CreatedDate")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ModifiedDate")),
                                CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                                CancelledDate = reader.IsDBNull(reader.GetOrdinal("CancelledDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CancelledDate")),
                                IsSyncedToGoogle = reader.IsDBNull(reader.GetOrdinal("IsSyncedToGoogle")) ? false : reader.GetBoolean(reader.GetOrdinal("IsSyncedToGoogle"))
                            };

                            tasks.Add(task);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving paginated tasks: " + ex.Message);
            }

            return (tasks, totalRecords, filteredRecords);
        }

        private List<SalesTask> GetTasksByUserId(int users_Id, int locationId)
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
                               CancelledDate,
                               IsSyncedToGoogle
                        FROM Tasks
                        WHERE AssignedTo = @Users_Id
                            AND Location = @LocationId
                            AND Status != 'Deleted'
                    ", conn);

                    cmd.Parameters.AddWithValue("@Users_Id", users_Id);
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
                                DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? DateTime.Today.AddDays(1) : reader.GetDateTime(reader.GetOrdinal("DueDate")),
                                Status = reader.GetString(reader.GetOrdinal("Status")),
                                AssignedTo = reader.GetInt32(reader.GetOrdinal("AssignedTo")),
                                Location = reader.IsDBNull(reader.GetOrdinal("Location")) ? 0 : reader.GetInt32(reader.GetOrdinal("Location")),
                                Property = reader.IsDBNull(reader.GetOrdinal("Property")) ? "" : reader.GetString(reader.GetOrdinal("Property")),
                                Type = reader.IsDBNull(reader.GetOrdinal("Type")) ? "" : reader.GetString(reader.GetOrdinal("Type")),
                                CreatedBy = reader.IsDBNull(reader.GetOrdinal("CreatedBy")) ? "" : reader.GetString(reader.GetOrdinal("CreatedBy")),
                                CreatedDate = reader.IsDBNull(reader.GetOrdinal("CreatedDate")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("CreatedDate")),
                                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("ModifiedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ModifiedDate")),
                                CompletedDate = reader.IsDBNull(reader.GetOrdinal("CompletedDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedDate")),
                                CancelledDate = reader.IsDBNull(reader.GetOrdinal("CancelledDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CancelledDate")),
                                IsSyncedToGoogle = reader.IsDBNull(reader.GetOrdinal("IsSyncedToGoogle")) ? false : reader.GetBoolean(reader.GetOrdinal("IsSyncedToGoogle"))
                            };

                            tasks.Add(task);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving tasks by User: " + ex.Message);
                // You could also log this exception or rethrow it as needed
            }

            return tasks;
        }

        private string GetTaskTitle(SalesTask task, List<User> users, int roleId)
        {
            var assignedUser = users.FirstOrDefault(u => u.Users_ID == task.AssignedTo);

            // Management roles see who the task is assigned to in the title
            if (RoleHelper.CanManageLocationTasks(roleId))
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
                            (Title, Description, DueDate, Status, AssignedTo, Location, Property, Type, CreatedBy, CreatedDate, ModifiedDate, CompletedDate, CancelledDate, RoleID, PropertyID, ArchiveAssignedID) 
                        OUTPUT INSERTED.TaskID
                        VALUES 
                            (@Title, @Description, @DueDate, @Status, @AssignedTo, @Location, @Property, @Type, @CreatedBy, @CreatedDate, @ModifiedDate, @CompletedDate, @CancelledDate, @RoleId, @PropertyId, @ArchiveAssignedID)";

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
                        cmd.Parameters.AddWithValue("@ArchiveAssignedID", (object?)task.CreatedById ?? DBNull.Value);

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
        public IActionResult GetCreateTaskModal( string source = "Orders", string? property = null, string? orderId = null, int? propertyId = null, decimal? balance = null, int? daysInactive = null, int? assignedTo = null, string? unit = null)
        {
            var assignedUser = GetAssignedUserContext(assignedTo);

            var defaultDueDate = DateTime.Now.AddDays(1);
            var calcDueDate = defaultDueDate.ToString("yyyy-MM-dd");
            var calcDueTime = defaultDueDate.Minute < 45
                ? defaultDueDate.AddMinutes(-(int)defaultDueDate.Minute).ToString("HH:mm tt")
                : defaultDueDate.AddMinutes(60 - defaultDueDate.Minute).ToString("HH:mm tt");

            var users = GetActiveUsers(); // This method is already defined in your controller

            string title = null;
            string description = null;

            switch (source.ToLower())
            {
                case "ar":
                    title = orderId != null ? $"Follow-up on Invoice #{orderId} from {property}" : "AR Follow-up";
                    if (balance.HasValue)
                    {
                        description = $"Outstanding balance of ${balance.Value:N2} for Invoice #{orderId}.";
                    }
                    break;
                case "inactive":
                    title = $"Follow-up with Inactive Customer: {property}";
                    description = daysInactive.HasValue && daysInactive > 0
                        ? $"No recent Activity. {property} has been inactive for {daysInactive} days. Need to follow up to re-engage them."
                        : $"No recent Activity. {property} has been inactive.";
                    break;
                case "orders":
                default:
                    title = orderId != null ? $"Follow-up for Order {orderId} from {property}" : "New Task";
                    description = orderId != null 
                        ? $"Follow-up task for {property} (Order ID: {orderId})" 
                        : null;
                    break;
            }            

            var modalVM = new TaskModalViewModel
            {
                Task = new TaskCreateViewModel
                {
                    Title = title,
                    Description = description,
                    DueDateDate = calcDueDate,
                    DueDateTime = calcDueTime,
                    DueDate = defaultDueDate, // Default to 1 day from now
                    Property = property,
                    PropertyID = propertyId, 
                    Source = source
                },
                Users = users,
                LoggedInUserId = assignedUser.Users_Id,
                RoleId = assignedUser.RoleId,
                TaskTypes = TaskTypeHelper.GetTaskTypes(source)
            };

            return PartialView("_CreateTaskModal", modalVM);
        }
        
        [HttpGet]
        public IActionResult EditModalPartial(int id, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Task", "Tasks");

            int userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            //int locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            SalesTask? task;

            // Management roles can see all tasks in their location
            if (RoleHelper.CanManageLocationTasks(roleId))
            {
                // Management roles can see all tasks by location
                task = GetAllTasksByLocation(locationId).FirstOrDefault(t => t.TaskID == id);
            }
            else
            {
                // Other users only see their own tasks
                task = GetTasksByUserId(users_Id, locationId).FirstOrDefault(t => t.TaskID == id);
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
                LoggedInUserId = users_Id,
                RoleId = roleId,
                TaskTypes = TaskTypeHelper.GetTaskTypes("Task")
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateFromAdmin(TaskModalViewModel modal)
        {
            string connStr = _configuration.GetConnectionString("SalesMetrics");
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");

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

                    // If you want to update Google Task and Calendar, you need to pull the Google tokens
                    string? googleEventId = null;
                    var eventIdCmd = new SqlCommand("SELECT GoogleEventId FROM Tasks WHERE TaskID = @TaskID", conn);
                    eventIdCmd.Parameters.AddWithValue("@TaskID", updatedTask.TaskID);
                    using (var gEreader = eventIdCmd.ExecuteReader())
                    {
                        if (gEreader.Read())
                        {
                            googleEventId = gEreader["GoogleEventId"]?.ToString();
                        }
                    }

                    // Pull Google token info
                    var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE Users_ID = @Users_ID", conn);
                    tokenCmd.Parameters.AddWithValue("@Users_ID", users_Id);
                    using var reader = tokenCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        var accessToken = reader["GoogleAccessToken"]?.ToString();
                        var refreshToken = reader["GoogleRefreshToken"]?.ToString();

                        if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken) && !string.IsNullOrWhiteSpace(googleEventId))
                        {
                            var calendarService = new GoogleCalendarService(_configuration);

                            // NOTE: You need to store & retrieve GoogleEventId in Tasks table for accurate updates/deletes
                            // Assuming you're doing that and it’s mapped to the TaskID for now:
                            await calendarService.UpdateTaskEventAsync(users_Id, accessToken, refreshToken, googleEventId, updatedTask.Title, updatedTask.Description, updatedTask.DueDate);
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
            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
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
                    var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE Users_ID = @Users_ID", conn);
                    tokenCmd.Parameters.AddWithValue("@Users_ID", users_Id);
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
        public async Task<IActionResult> Delete(int taskId, string? returnUrl = null)
        {
            var userId = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
                await conn.OpenAsync();

                // Get the GoogleAccessToken and RefreshToken
                var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE Users_ID = @Users_ID", conn);
                tokenCmd.Parameters.AddWithValue("@Users_ID", users_Id);

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
                    await calendarService.DeleteTaskEventAsync(users_Id, accessToken, refreshToken, taskId.ToString());
                }

                // Delete from DB
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
                TempData["Error"] = $"Error deleting taskID({taskId}) : {ex.Message}";
            }

            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);

            // ✅ Try to redirect back to the referring page
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }

            // Fallback redirect to Task View Page
            return RedirectToAction("Task");
        }

        [HttpPost]
        public async Task<JsonResult> UpdateStatus([FromBody] StatusUpdateModel model)
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

                // Send notification about status change
                try
                {
                    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == model.TaskId);
                    if (task != null && task.AssignedTo > 0)
                    {
                        await _notificationService.NotifyTaskStatusChangedAsync(model.TaskId, task.AssignedTo, task.Title ?? "Task", model.NewStatus);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending status change notification: {ex.Message}");
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateFromYardi(TaskModalViewModel modal, string? returnUrl)
        {
            var model = modal.Task;

            if (model == null || string.IsNullOrWhiteSpace(model.Property))
            {
                TempData["Error"] = "Invalid property info.";
                return RedirectToAction("YardiProperties", "Yardi");
            }

            Console.WriteLine($"[DEBUG] Incoming Task.AssignedTo = {modal.Task?.AssignedTo}");
            Console.WriteLine($"[DEBUG] ModelState.IsValid = {ModelState.IsValid}");

            // ✅ Convert TaskCreateViewModel ➜ SalesTask
            var task = new SalesTask
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

            // Management roles can assign tasks to team members
            if (RoleHelper.CanManageLocationTasks(roleId))
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
                if (!int.TryParse(HttpContext.Session.GetString("Users_ID"), out var assignedTo))
                {
                    TempData["Error"] = "Unable to determine assigned user.";
                    return RedirectToAction("Index", "Home");
                }

                task.AssignedTo = assignedTo;
            }


            Console.WriteLine($"[DEBUG] Creating Yardi Task: PropertyID = {task.PropertyID}, Property = {task.Property}");

            int taskId = SaveTaskToDatabase(task);
            if (taskId <= 0)
            {
                TempData["Error"] = "Failed to create task in the database.";
                return RedirectToAction("YardiProperties", "Yardi");
            }
            else
            {
                task.TaskID = taskId;

                // Add to Google Task
                var (googleTaskId, googleEventId) = await TryGoogleSyncTaskAsync(task);

                if (!string.IsNullOrEmpty(googleTaskId) && !string.IsNullOrEmpty(googleEventId))
                    TempData["Success"] = "Task created and synced to Google Task & Calendar.";
                else if (!string.IsNullOrEmpty(googleTaskId))
                    TempData["Success"] = "Task created and synced to Google Task.";
                else if (!string.IsNullOrEmpty(googleEventId))
                    TempData["Success"] = "Task created and synced to Google Calendar.";
                else
                    TempData["Success"] = $"Task (ID:{task.TaskID}) has been created successfully.";

                //var returnUrl = Request.Form["ReturnUrl"].ToString();

                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction("YardiProperties", "Yardi"); // fallback just in case
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateFromProperties(TaskModalViewModel modal, string? returnUrl)
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
                    //ModelState.AddModelError("DueDate", "Invalid date/time.");
                    return Json(new { success = false, message = "Invalid date/time." });
                }
            }
            else
            {
                //ModelState.AddModelError("DueDate", "Date and time are required.");
                return Json(new { success = false, message = "Date and time are required." });
            }


            if (model == null || string.IsNullOrWhiteSpace(model.Property))
            {
                TempData["Error"] = "Invalid property info.";
                //return RedirectToAction("Properties", "Properties");
                return Json(new { success = false, message = "Invalid property info." });
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Form is missing required fields.";
                return RedirectToAction("Properties", "Properties");
            }

            // ✅ ADD THIS BLOCK before the try-catch
            if (model.AssignedTo == null || model.AssignedTo == 0)
            {
                model.AssignedTo = int.TryParse(HttpContext.Session.GetString("Users_ID"), out var fallbackAssignedTo)
                    ? fallbackAssignedTo
                    : 0;
            }        

            try
            {
                int roleId = int.Parse(User.FindFirst("roleId")?.Value ?? "0");
                int salesmanId = int.Parse(User.FindFirst("SalesmanId")?.Value ?? "0");
                int locationId = int.TryParse(HttpContext.Session.GetString("LocationId"), out var locId) ? locId : 0;

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
                    Location = locationId,
                    AssignedTo = (roleId == 2)
                        ? Convert.ToInt32(HttpContext.Session.GetString("Users_ID"))
                        : (model.AssignedTo > 0 ? model.AssignedTo : 0)

                };

                if (task.AssignedTo == null || task.AssignedTo == 0)
                {
                    TempData["Error"] = "Assigned user is missing or invalid.";
                    //return RedirectToAction("Properties", "Properties");
                    return Json(new { success = false, message = "Assigned user is missing or invalid." });
                }

                int taskId = SaveTaskToDatabase(task);
                task.TaskID = taskId;

                if (taskId <= 0)
                {
                    TempData["Error"] = "Failed to create task in the database.";
                    return RedirectToAction("Properties", "Properties");
                }
                else
                {
                    task.TaskID = taskId;

                    // Add to Google Task
                    var (googleTaskId, googleEventId) = await TryGoogleSyncTaskAsync(task);

                    if (!string.IsNullOrEmpty(googleTaskId) && !string.IsNullOrEmpty(googleEventId))
                        TempData["Success"] = "Task created and synced to Google Task & Calendar.";
                    else if (!string.IsNullOrEmpty(googleTaskId))
                        TempData["Success"] = "Task created and synced to Google Task.";
                    else if (!string.IsNullOrEmpty(googleEventId))
                        TempData["Success"] = "Task created and synced to Google Calendar.";
                    else
                        TempData["Success"] = $"Task (ID:{task.TaskID}) has been created successfully.";

                    //var returnUrl = Request.Form["ReturnUrl"].ToString();

                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        return RedirectToAction("Properties", "Properties"); // fallback just in case
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating task: {ex.Message}");
                TempData["Error"] = $"Error creating Task for {model.Property}";
                //return StatusCode(500, $"Internal server error: {ex.Message}");
                return Json(new { success = false, message = "Internal server error." });
            }
        }

        public async Task<IActionResult> CreateFromWorkOrderSchedule(TaskModalViewModel modal, string? returnUrl)
        {
            var model = modal.Task;

            if (model == null || string.IsNullOrWhiteSpace(model.Property))
            {
                TempData["Error"] = "Missing task data.";
                return RedirectToAction("Index", "Dashboard");
            }

            try
            {
                // Pull values from session
                var username = HttpContext.Session.GetString("Username") ?? "system";
                int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
                int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
                int locationId = LocationHelper.GetCurrentLocationId(HttpContext);

                modal.RoleId = roleId;
                modal.LoggedInUserId = users_Id;

                // Determine AssignedTo: Management roles can assign to team members, others assign to themselves
                Console.WriteLine("AssignedTo in CreateFromWorkOrder: " + model.AssignedTo);

                int assignedTo = (RoleHelper.CanManageLocationTasks(roleId) ? model.AssignedTo : users_Id);

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
                    RoleId = roleId,
                    AssignedTo = assignedTo,
                    Location = locationId
                };

                int taskId = SaveTaskToDatabase(task);
                task.TaskID = taskId;

                if (taskId <= 0)
                {
                    TempData["Error"] = "Failed to create task in the database.";
                    return RedirectToAction("Schedule", "Tasks");
                }
                else
                {
                    task.TaskID = taskId;

                    // Add to Google Task
                    var (googleTaskId, googleEventId) = await TryGoogleSyncTaskAsync(task);

                    if (!string.IsNullOrEmpty(googleTaskId) && !string.IsNullOrEmpty(googleEventId))
                        TempData["Success"] = "Task created and synced to Google Task & Calendar.";
                    else if (!string.IsNullOrEmpty(googleTaskId))
                        TempData["Success"] = "Task created and synced to Google Task.";
                    else if (!string.IsNullOrEmpty(googleEventId))
                        TempData["Success"] = "Task created and synced to Google Calendar.";
                    else
                        TempData["Success"] = $"Task (ID:{task.TaskID}) has been created successfully.";

                    //var returnUrl = Request.Form["ReturnUrl"].ToString();

                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        return RedirectToAction("Schedule", "Tasks"); // fallback just in case
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating task: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromWorkOrder(TaskCreateViewModel model, string? returnUrl)
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
                var userIdStr = HttpContext.Session.GetString("Users_Id");
                var roleIdStr = HttpContext.Session.GetString("RoleId");
                var locationIdStr = HttpContext.Session.GetString("LocationId");

                if (!int.TryParse(userIdStr, out int sessionUserId) ||
                    !int.TryParse(roleIdStr, out int roleId) ||
                    !int.TryParse(locationIdStr, out int locationId))
                {
                    return BadRequest("Session data missing or invalid.");
                }

                // Determine AssignedTo: Management roles can assign to team members, others assign to themselves
                Console.WriteLine("AssignedTo in CreateFromWorkOrder: " + model.AssignedTo);

                int assignedTo = (RoleHelper.CanManageLocationTasks(roleId) ? model.AssignedTo : sessionUserId);

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
                task.TaskID = TaskID;

                // Add to Google Task
                var (googleTaskId, googleEventId) = await TryGoogleSyncTaskAsync(task);


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
            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
            //int locationId = int.Parse(User.FindFirst("LocationId")?.Value ?? "0");
            int locationId = LocationHelper.GetCurrentLocationId(HttpContext);

            List<SalesTask> tasks;
            List<User> users;

            // Management roles can see all location tasks
            if (RoleHelper.CanManageLocationTasks(roleId))
            {
                tasks = GetAllTasksByLocation(locationId);
                users = GetAllUsersForTaskDisplay(locationId, roleId);
            }
            else
            {
                tasks = GetTasksByUserId(users_Id, locationId);
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
                    assignedTo = users.FirstOrDefault(u => u.Users_ID == t.AssignedTo)?.FullName() ?? "Unassigned",
                    description = t.Description
                }
            }).ToList();

            return Json(calendarTasks);
        }

        [HttpGet]
        public JsonResult GetNotesForTask(int taskId)
        {
            var notes = new List<object>(); // ✅ MUST be List<object> for anonymous types
            string currentUser = HttpContext.Session.GetString("Username") ?? "system";
            int currentUserRole = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT NoteID, TaskID, NoteText, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate
                    FROM TaskNotes
                    WHERE TaskID = @TaskID AND DeletedDate IS NULL
                    ORDER BY CreatedDate DESC
                ", conn);
                cmd.Parameters.AddWithValue("@TaskID", taskId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var noteCreatedBy = reader.GetString("CreatedBy");

                        // ✅ CRITICAL: Create anonymous object with explicit camelCase property names
                        notes.Add(new
                        {
                            noteID = reader.GetInt32("NoteID"),  // ✅ Capital ID
                            taskID = reader.GetInt32("TaskID"),
                            noteText = reader.GetString("NoteText"),
                            createdBy = noteCreatedBy,
                            createdDate = reader.GetDateTime("CreatedDate"),
                            modifiedBy = reader.IsDBNull("ModifiedBy") ? null : reader.GetString("ModifiedBy"),
                            modifiedDate = reader.IsDBNull("ModifiedDate") ? (DateTime?)null : reader.GetDateTime("ModifiedDate"),
                            canEdit = noteCreatedBy == currentUser || currentUserRole == 1 || currentUserRole == 3 || currentUserRole == 4
                        });
                    }
                }
            }

            return Json(notes);
        }

        [HttpPost]
        public async Task<IActionResult> AddNoteToTask(int taskId, string noteText)
        {
            string createdBy = HttpContext.Session.GetString("Username") ?? "system";
            var currentUserId = GetUserContext().Users_Id;

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                conn.Open();
                var cmd = new SqlCommand("INSERT INTO TaskNotes (TaskID, NoteText, CreatedBy) VALUES (@TaskID, @NoteText, @CreatedBy)", conn);
                cmd.Parameters.AddWithValue("@TaskID", taskId);
                cmd.Parameters.AddWithValue("@NoteText", noteText);
                cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                cmd.ExecuteNonQuery();
            }

            // Send notification to task owner
            try
            {
                var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
                if (task != null && task.AssignedTo > 0)
                {
                    await _notificationService.NotifyNoteAddedAsync(taskId, task.AssignedTo, currentUserId, task.Title ?? "Task");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending note notification: {ex.Message}");
            }

            return Ok();
        }

        [HttpPost]
        public IActionResult UpdateNoteText(int noteId, string noteText)
        {
            try
            {
                string currentUser = HttpContext.Session.GetString("Username") ?? "system";
                int currentUserRole = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");

                using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
                {
                    conn.Open();

                    // First check if user owns this note or is an admin
                    var checkCmd = new SqlCommand("SELECT CreatedBy FROM TaskNotes WHERE NoteID = @NoteID", conn);
                    checkCmd.Parameters.AddWithValue("@NoteID", noteId);

                    var noteCreator = checkCmd.ExecuteScalar()?.ToString();

                    // Allow if user owns the note or is admin/manager (roles 1, 3, 4)
                    if (noteCreator != currentUser && currentUserRole != 1 && currentUserRole != 3 && currentUserRole != 4)
                    {
                        return Json(new { success = false, message = "You can only edit your own notes." });
                    }

                    // Update the note
                    var cmd = new SqlCommand(@"
                UPDATE TaskNotes 
                SET NoteText = @NoteText, 
                    ModifiedBy = @ModifiedBy, 
                    ModifiedDate = @ModifiedDate 
                WHERE NoteID = @NoteID", conn);

                    cmd.Parameters.AddWithValue("@NoteID", noteId);
                    cmd.Parameters.AddWithValue("@NoteText", noteText);
                    cmd.Parameters.AddWithValue("@ModifiedBy", currentUser);
                    cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        return Json(new { success = true });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Note not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating note: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeleteNote(int noteId)
        {
            try
            {
                string currentUser = HttpContext.Session.GetString("Username") ?? "system";
                int currentUserRole = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");

                using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
                {
                    conn.Open();

                    // First check if user owns this note or is an admin
                    var checkCmd = new SqlCommand("SELECT CreatedBy FROM TaskNotes WHERE NoteID = @NoteID", conn);
                    checkCmd.Parameters.AddWithValue("@NoteID", noteId);

                    var noteCreator = checkCmd.ExecuteScalar()?.ToString();

                    // Allow if user owns the note or is admin/manager (roles 1, 3, 4)
                    if (noteCreator != currentUser && currentUserRole != 1 && currentUserRole != 3 && currentUserRole != 4)
                    {
                        return Json(new { success = false, message = "You can only delete your own notes." });
                    }

                    // Soft delete the note
                    var cmd = new SqlCommand(@"
                UPDATE TaskNotes 
                SET DeletedBy = @DeletedBy, 
                    DeletedDate = @DeletedDate 
                WHERE NoteID = @NoteID", conn);

                    cmd.Parameters.AddWithValue("@NoteID", noteId);
                    cmd.Parameters.AddWithValue("@DeletedBy", currentUser);
                    cmd.Parameters.AddWithValue("@DeletedDate", DateTime.Now);

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        return Json(new { success = true });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Note not found." });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting note: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DynamicCreate(TaskModalViewModel model, string? returnUrl)
        {
            if (!ModelState.IsValid)
            {
                // Handle invalid model state
                TempData["TaskFormError"] = "Please complete all required fields before submitting the task.";
                // make sure returnUrl is valid (fallback if null)
                return Redirect(returnUrl ?? Url.Action("Schedule", "Tasks"));
            }

            switch (model.Task.Source)
            {
                case "Orders":
                    return await CreateFromWorkOrderSchedule(model, returnUrl);
                case "AR":
                case "Inactive":
                case "Properties":
                    return await CreateFromProperties(model, returnUrl);
                case "Yardi":
                    return await CreateFromYardi(model, returnUrl);
                default:
                    return await Create(model, returnUrl);
            }
        }


        [HttpGet]
        public IActionResult DuplicateTaskModal(int id)
        {
            int users_Id = int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
            int userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            int roleId = int.Parse(User.FindFirst("RoleId")?.Value ?? "0");

            var locationId = LocationHelper.GetCurrentLocationId(HttpContext);
            var task = GetAllTasksByLocation(locationId).FirstOrDefault(t => t.TaskID == id);
            if (task == null)
                return Content("Task not found.");

            var due = DateTime.Now.AddHours(2);

            var modalVM = new TaskModalViewModel
            {
                Task = new TaskCreateViewModel
                {
                    Title = task.Title,
                    Description = task.Description,
                    DueDate = due,
                    DueDateDate = due.ToString("yyyy-MM-dd"), // ✅ Fix for HTML5 <input type="date">
                    DueDateTime = due.ToString("hh:mm tt"),
                    Type = task.Type,
                    Status = "Pending",
                    Property = task.Property,
                    PropertyID = task.PropertyID,
                    AssignedTo = task.AssignedTo ?? users_Id
                },
                Users = GetActiveUsers(),
                LoggedInUserId = users_Id,
                RoleId = roleId,
                TaskTypes = TaskTypeHelper.GetTaskTypes("Task")
            };

            return PartialView("_DuplicateTaskModal", modalVM);
        }

        private async Task<(string? GoogleTaskId, string? GoogleEventId)> TryGoogleSyncTaskAsync(SalesTask task)
        {
            string? googleTaskId = null;
            string? googleEventId = null;

            using (var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics")))
            {
                await conn.OpenAsync();

                var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE Users_ID = @Users_ID", conn);
                tokenCmd.Parameters.AddWithValue("@Users_ID", task.AssignedTo ?? 0);

                string? accessToken = null, refreshToken = null;
                using (var reader = await tokenCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        accessToken = reader["GoogleAccessToken"]?.ToString();
                        refreshToken = reader["GoogleRefreshToken"]?.ToString();
                    }
                }

                if (!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken))
                {
                    var tasksService = new GoogleTasksService(_configuration);
                    googleTaskId = await tasksService.CreateTaskAsync(
                        accessToken, refreshToken, task.AssignedTo.ToString(), task.TaskID.ToString(), task.Title, task.Description, task.DueDate
                    );

                    if (task.Type?.Equals("QC", StringComparison.OrdinalIgnoreCase) == true || task.Type?.Equals("Site Visit", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var calendarService = new GoogleCalendarService(_configuration);
                        googleEventId = await calendarService.AddTaskEventAsync(
                            task.AssignedTo ?? 0, accessToken, refreshToken, task.TaskID.ToString(), task.Title, task.Description, task.DueDate ?? DateTime.Now
                        );
                    }

                    // Update DB with Google info
                    var updateCmd = new SqlCommand(@"
                        UPDATE Tasks
                        SET GoogleTaskId = @GoogleTaskId,
                            GoogleEventId = @GoogleEventId,
                            IsSyncedToGoogle = 1
                        WHERE TaskID = @TaskID
                    ", conn);

                    updateCmd.Parameters.AddWithValue("@GoogleTaskId", (object?)googleTaskId ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@GoogleEventId", (object?)googleEventId ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@TaskID", task.TaskID);

                    await updateCmd.ExecuteNonQueryAsync();
                }
            }

            return (googleTaskId, googleEventId);
        }


        [HttpPost]
        public async Task<IActionResult> ManualGoogleSync(int taskId)
        {
            var user = GetUserContext();

            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();

            // Fetch the task
            SalesTask? task = null;
            var fetchCmd = new SqlCommand("SELECT * FROM Tasks WHERE TaskID = @TaskID", conn);
            fetchCmd.Parameters.AddWithValue("@TaskID", taskId);

            using (var reader = await fetchCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    task = new SalesTask
                    {
                        TaskID = reader.GetInt32(reader.GetOrdinal("TaskID")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Description = reader["Description"]?.ToString(),
                        DueDate = reader.GetDateTime(reader.GetOrdinal("DueDate")),
                        Type = reader["Type"]?.ToString(),
                        AssignedTo = reader.GetInt32(reader.GetOrdinal("AssignedTo")),
                        // add IsSyncedToGoogle bit from SQL
                        IsSyncedToGoogle = reader.IsDBNull(reader.GetOrdinal("IsSyncedToGoogle")) ? false : reader.GetBoolean(reader.GetOrdinal("IsSyncedToGoogle"))
                    };
                }
            }

            if (task == null)
            {
                TempData["Error"] = "Task not found.";
                return RedirectToAction("Task");
            }

            // Fetch Google tokens
            string? accessToken = null, refreshToken = null;
            var tokenCmd = new SqlCommand("SELECT GoogleAccessToken, GoogleRefreshToken FROM Users WHERE Users_ID = @Users_ID", conn);
            tokenCmd.Parameters.AddWithValue("@Users_ID", task.AssignedTo);
            using (var reader = await tokenCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    accessToken = reader["GoogleAccessToken"]?.ToString();
                    refreshToken = reader["GoogleRefreshToken"]?.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                TempData["Error"] = "User is not linked to Google.";
                return RedirectToAction("Task");
            }

            // Perform Google sync
            var tasksService = new GoogleTasksService(_configuration);
            var calendarService = new GoogleCalendarService(_configuration);

            var googleTaskId = await tasksService.CreateTaskAsync(accessToken, refreshToken, task.AssignedTo.ToString(), task.TaskID.ToString(), task.Title, task.Description, task.DueDate);
            string? googleEventId = null;

            if (task.Type?.Equals("QC", StringComparison.OrdinalIgnoreCase) == true || task.Type?.Equals("Site Visit", StringComparison.OrdinalIgnoreCase) == true)
            {
                googleEventId = await calendarService.AddTaskEventAsync(task.AssignedTo.Value, accessToken, refreshToken, task.TaskID.ToString(), task.Title, task.Description, task.DueDate.Value);
            }
            else
            {
                TempData["Error"] = "Missing AssignedTo or DueDate — cannot sync with Google Calendar.";
                return RedirectToAction("Task");
            }

            var updateCmd = new SqlCommand(@"
                UPDATE Tasks SET
                    GoogleTaskId = @GoogleTaskId,
                    GoogleEventId = @GoogleEventId,
                    IsSyncedToGoogle = 1
                WHERE TaskID = @TaskID
            ", conn);

            updateCmd.Parameters.AddWithValue("@GoogleTaskId", (object?)googleTaskId ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@GoogleEventId", (object?)googleEventId ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@TaskID", taskId);

            await updateCmd.ExecuteNonQueryAsync();

            TempData["Success"] = "Task synced with Google.";
            return RedirectToAction("Task");
        }

        // ==================== SERVER-SIDE DATATABLES FOR PERFORMANCE ====================

        /// <summary>
        /// Server-side DataTables endpoint for AdminTask page with caching
        /// Supports pagination, sorting, searching, and filtering
        /// </summary>
        [HttpPost]
        public JsonResult GetAdminTasksData()
        {
            try
            {
                var userContext = GetUserContext();

                // DataTables parameters
                var draw = int.Parse(Request.Form["draw"].FirstOrDefault() ?? "1");
                var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
                var searchValue = Request.Form["search[value]"].FirstOrDefault() ?? "";
                var sortColumnIndex = int.Parse(Request.Form["order[0][column]"].FirstOrDefault() ?? "10");
                var sortDirection = Request.Form["order[0][dir]"].FirstOrDefault() ?? "desc";

                // Get cached user list or fetch it
                var users = GetCachedUsers(userContext.LocationId, userContext.RoleId);

                // Get all tasks for location (consider adding caching here too with short TTL)
                var allTasks = GetAllTasksByLocation(userContext.LocationId);

                // Apply search filter
                if (!string.IsNullOrEmpty(searchValue))
                {
                    allTasks = allTasks.Where(t =>
                        (t.Title != null && t.Title.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Description != null && t.Description.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Type != null && t.Type.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Property != null && t.Property.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Status != null && t.Status.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }

                // Apply sorting
                allTasks = ApplyTaskSorting(allTasks, sortColumnIndex, sortDirection, users);

                // Get total count before pagination
                var recordsTotal = allTasks.Count;

                // Apply pagination
                var pagedTasks = allTasks.Skip(start).Take(length).ToList();

                // Format data for DataTables
                var data = pagedTasks.Select(task =>
                {
                    var assignedUser = users?.FirstOrDefault(u => u.Users_ID == task.AssignedTo);
                    var createdByUser = users?.FirstOrDefault(u => u.Users_ID.ToString() == task.CreatedBy);

                    return new
                    {
                        taskID = task.TaskID,
                        title = task.Title ?? "",
                        description = task.Description ?? "",
                        type = task.Type ?? "",
                        property = task.Property ?? "",
                        location = task.Location,
                        dueDate = task.DueDate.ToString(),
                        completedDate = task.CompletedDate?.ToString() ?? "",
                        status = task.Status ?? "Pending",
                        assignedTo = task.AssignedTo,
                        assignedToName = assignedUser != null ? $"{assignedUser.FirstName} {assignedUser.LastName}" : "Unassigned",
                        createdBy = createdByUser != null ? $"{createdByUser.FirstName} {createdByUser.LastName}" : task.CreatedBy,
                        createdDate = task.CreatedDate.ToString()
                    };
                }).ToList();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = GetAllTasksByLocation(userContext.LocationId).Count,
                    recordsFiltered = recordsTotal,
                    data = data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAdminTasksData: {ex.Message}");
                return Json(new { draw = 1, recordsTotal = 0, recordsFiltered = 0, data = new List<object>(), error = ex.Message });
            }
        }

        /// <summary>
        /// Server-side DataTables endpoint for user's own tasks with caching
        /// </summary>
        [HttpPost]
        public JsonResult GetMyTasksData()
        {
            try
            {
                var userContext = GetUserContext();

                // DataTables parameters
                var draw = int.Parse(Request.Form["draw"].FirstOrDefault() ?? "1");
                var start = int.Parse(Request.Form["start"].FirstOrDefault() ?? "0");
                var length = int.Parse(Request.Form["length"].FirstOrDefault() ?? "10");
                var searchValue = Request.Form["search[value]"].FirstOrDefault() ?? "";
                var sortColumnIndex = int.Parse(Request.Form["order[0][column]"].FirstOrDefault() ?? "5");
                var sortDirection = Request.Form["order[0][dir]"].FirstOrDefault() ?? "desc";

                // Get user's tasks
                var allTasks = GetTasksByUserId(userContext.Users_Id, userContext.LocationId);

                // Apply search filter
                if (!string.IsNullOrEmpty(searchValue))
                {
                    allTasks = allTasks.Where(t =>
                        (t.Title != null && t.Title.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Description != null && t.Description.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Type != null && t.Type.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Property != null && t.Property.Contains(searchValue, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Status != null && t.Status.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }

                // Apply sorting
                allTasks = ApplyMyTaskSorting(allTasks, sortColumnIndex, sortDirection);

                // Get total count before pagination
                var recordsTotal = allTasks.Count;

                // Apply pagination
                var pagedTasks = allTasks.Skip(start).Take(length).ToList();

                // Format data for DataTables
                var data = pagedTasks.Select(task => new
                {
                    taskID = task.TaskID,
                    title = task.Title ?? "",
                    description = task.Description ?? "",
                    type = task.Type ?? "",
                    property = task.Property ?? "",
                    location = task.Location,
                    createdDate = task.CreatedDate.ToString(),
                    dueDate = task.DueDate.ToString(),
                    status = task.Status ?? "Pending",
                    isSyncedToGoogle = task.IsSyncedToGoogle
                }).ToList();

                return Json(new
                {
                    draw = draw,
                    recordsTotal = GetTasksByUserId(userContext.Users_Id, userContext.LocationId).Count,
                    recordsFiltered = recordsTotal,
                    data = data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetMyTasksData: {ex.Message}");
                return Json(new { draw = 1, recordsTotal = 0, recordsFiltered = 0, data = new List<object>(), error = ex.Message });
            }
        }

        /// <summary>
        /// Get cached user list with 5-minute TTL
        /// Cache is keyed by location and role since different roles see different users
        /// </summary>
        private List<User> GetCachedUsers(int locationId, int roleId)
        {
            string cacheKey = $"Users_Location_{locationId}_Role_{roleId}";

            if (!_cache.TryGetValue(cacheKey, out List<User> users))
            {
                users = GetAllUsersForTaskDisplay(locationId, roleId);

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(cacheKey, users, cacheOptions);
            }

            return users ?? new List<User>();
        }

        /// <summary>
        /// Apply sorting for AdminTask table
        /// </summary>
        private List<SalesTask> ApplyTaskSorting(List<SalesTask> tasks, int columnIndex, string direction, List<User> users)
        {
            var orderedTasks = columnIndex switch
            {
                0 => direction == "asc" ? tasks.OrderBy(t => t.TaskID) : tasks.OrderByDescending(t => t.TaskID),
                1 => direction == "asc" ? tasks.OrderBy(t => t.Title) : tasks.OrderByDescending(t => t.Title),
                2 => direction == "asc" ? tasks.OrderBy(t => t.Description) : tasks.OrderByDescending(t => t.Description),
                3 => direction == "asc" ? tasks.OrderBy(t => t.Type) : tasks.OrderByDescending(t => t.Type),
                4 => direction == "asc" ? tasks.OrderBy(t => t.Property) : tasks.OrderByDescending(t => t.Property),
                5 => direction == "asc" ? tasks.OrderBy(t => t.Location) : tasks.OrderByDescending(t => t.Location),
                6 => direction == "asc" ? tasks.OrderBy(t => t.DueDate) : tasks.OrderByDescending(t => t.DueDate),
                7 => direction == "asc" ? tasks.OrderBy(t => t.CompletedDate) : tasks.OrderByDescending(t => t.CompletedDate),
                8 => direction == "asc" ? tasks.OrderBy(t => t.Status) : tasks.OrderByDescending(t => t.Status),
                9 => direction == "asc" ? tasks.OrderBy(t => t.AssignedTo) : tasks.OrderByDescending(t => t.AssignedTo),
                10 => direction == "asc" ? tasks.OrderBy(t => t.CreatedDate) : tasks.OrderByDescending(t => t.CreatedDate),
                _ => tasks.OrderByDescending(t => t.CreatedDate)
            };

            return orderedTasks.ToList();
        }

        /// <summary>
        /// Apply sorting for My Tasks table
        /// </summary>
        private List<SalesTask> ApplyMyTaskSorting(List<SalesTask> tasks, int columnIndex, string direction)
        {
            var orderedTasks = columnIndex switch
            {
                0 => direction == "asc" ? tasks.OrderBy(t => t.TaskID) : tasks.OrderByDescending(t => t.TaskID),
                1 => direction == "asc" ? tasks.OrderBy(t => t.Title) : tasks.OrderByDescending(t => t.Title),
                2 => direction == "asc" ? tasks.OrderBy(t => t.Description) : tasks.OrderByDescending(t => t.Description),
                3 => direction == "asc" ? tasks.OrderBy(t => t.Type) : tasks.OrderByDescending(t => t.Type),
                4 => direction == "asc" ? tasks.OrderBy(t => t.Property) : tasks.OrderByDescending(t => t.Property),
                5 => direction == "asc" ? tasks.OrderBy(t => t.CreatedDate) : tasks.OrderByDescending(t => t.CreatedDate),
                6 => direction == "asc" ? tasks.OrderBy(t => t.DueDate) : tasks.OrderByDescending(t => t.DueDate),
                7 => direction == "asc" ? tasks.OrderBy(t => t.Status) : tasks.OrderByDescending(t => t.Status),
                _ => tasks.OrderByDescending(t => t.CreatedDate)
            };

            return orderedTasks.ToList();
        }

        // END CLASS
    }

    public class UserContext
    {
        public int Users_Id { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public int SalesmanId { get; set; }
        public int LocationId { get; set; }
        public string Username { get; set; }
        public string FName { get; set; }
        public string LName { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
    }
}