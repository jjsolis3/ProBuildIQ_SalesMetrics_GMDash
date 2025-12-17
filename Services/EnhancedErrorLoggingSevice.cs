using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesMetrics.Models;
using System.Data;
using System.Text.Json;

namespace SalesMetrics.Services
{
    /// <summary>
    /// Enhanced implementation that extends your existing ErrorLoggingService
    /// This uses your existing ErrorLog table and dashboard - NO separate systems
    /// </summary>
    public class EnhancedErrorLoggingService : ErrorLoggingService, IEnhancedErrorLoggingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EnhancedErrorLoggingService> _logger;

        public EnhancedErrorLoggingService(IConfiguration configuration, ILogger<EnhancedErrorLoggingService> logger)
            : base(configuration, logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task LogTaskCreationErrorAsync(
            TaskCreateViewModel taskData,
            Exception exception,
            HttpContext? httpContext = null,
            string? additionalContext = null)
        {
            try
            {
                // Create rich additional data with task context for your existing ErrorLog table
                var taskContextData = JsonSerializer.Serialize(new
                {
                    ErrorType = "TaskCreation",
                    ErrorCategory = "Task Management",
                    TaskData = new
                    {
                        TaskId = taskData?.TaskID,
                        Title = taskData?.Title,
                        Source = taskData?.Source,
                        AssignedTo = taskData?.AssignedTo,
                        PropertyID = taskData?.PropertyID,
                        Property = taskData?.Property,
                        DueDateDate = taskData?.DueDateDate,
                        DueDateTime = taskData?.DueDateTime,
                        Status = taskData?.Status,
                        Type = taskData?.Type
                    },
                    SessionContext = new
                    {
                        UserId = httpContext?.Session.GetString("Users_Id"),
                        Username = httpContext?.Session.GetString("Username"),
                        LocationId = httpContext?.Session.GetString("LocationId"),
                        RoleId = httpContext?.Session.GetString("RoleId")
                    },
                    AdditionalContext = additionalContext
                });

                // Use your existing error logging system - everything goes to ErrorLog table
                await LogErrorAsync(
                    exception,
                    httpContext,
                    taskContextData,
                    $"TaskCreation-{taskData?.Source ?? "Unknown"}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log task creation error. Original error: {OriginalError}",
                    exception.Message);
            }
        }

        public async Task LogTaskCreationWarningAsync(
            TaskCreateViewModel taskData,
            string warningMessage,
            HttpContext? httpContext = null,
            string? additionalContext = null)
        {
            try
            {
                var taskContextData = JsonSerializer.Serialize(new
                {
                    ErrorType = "TaskCreationWarning",
                    ErrorCategory = "Task Management",
                    TaskData = new
                    {
                        TaskId = taskData?.TaskID,
                        Title = taskData?.Title,
                        Source = taskData?.Source,
                        AssignedTo = taskData?.AssignedTo,
                        PropertyID = taskData?.PropertyID,
                        Property = taskData?.Property
                    },
                    SessionContext = new
                    {
                        UserId = httpContext?.Session.GetString("Users_Id"),
                        Username = httpContext?.Session.GetString("Username"),
                        LocationId = httpContext?.Session.GetString("LocationId"),
                        RoleId = httpContext?.Session.GetString("RoleId")
                    },
                    AdditionalContext = additionalContext
                });

                await LogWarningAsync(
                    $"Task Creation Warning: {warningMessage}",
                    httpContext,
                    taskContextData,
                    $"TaskWarning-{taskData?.Source ?? "Unknown"}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log task creation warning: {Warning}", warningMessage);
            }
        }

        public async Task LogTaskValidationErrorAsync(
            TaskCreateViewModel taskData,
            List<string> validationErrors,
            HttpContext? httpContext = null)
        {
            try
            {
                var validationMessage = $"Task validation failed: {string.Join("; ", validationErrors)}";

                var taskContextData = JsonSerializer.Serialize(new
                {
                    ErrorType = "TaskValidation",
                    ErrorCategory = "Task Management",
                    TaskData = new
                    {
                        TaskId = taskData?.TaskID,
                        Title = taskData?.Title,
                        Source = taskData?.Source,
                        AssignedTo = taskData?.AssignedTo,
                        PropertyID = taskData?.PropertyID,
                        Property = taskData?.Property
                    },
                    ValidationErrors = validationErrors,
                    SessionContext = new
                    {
                        UserId = httpContext?.Session.GetString("Users_Id"),
                        Username = httpContext?.Session.GetString("Username"),
                        LocationId = httpContext?.Session.GetString("LocationId"),
                        RoleId = httpContext?.Session.GetString("RoleId")
                    }
                });

                await LogWarningAsync(
                    validationMessage,
                    httpContext,
                    taskContextData,
                    $"TaskValidation-{taskData?.Source ?? "Unknown"}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log task validation error");
            }
        }

        public async Task LogTaskDatabaseErrorAsync(
            SalesTask taskData,
            Exception exception,
            HttpContext? httpContext = null,
            string operation = "Save")
        {
            try
            {
                var taskContextData = JsonSerializer.Serialize(new
                {
                    ErrorType = "TaskDatabase",
                    ErrorCategory = "Task Management",
                    TaskData = new
                    {
                        TaskId = taskData?.TaskID,
                        Title = taskData?.Title,
                        Source = taskData?.Source,
                        AssignedTo = taskData?.AssignedTo,
                        PropertyID = taskData?.PropertyID,
                        Property = taskData?.Property,
                        Status = taskData?.Status,
                        Type = taskData?.Type,
                        DueDate = taskData?.DueDate,
                        CreatedBy = taskData?.CreatedBy
                    },
                    DatabaseOperation = operation,
                    SessionContext = new
                    {
                        UserId = httpContext?.Session.GetString("Users_Id"),
                        Username = httpContext?.Session.GetString("Username"),
                        LocationId = httpContext?.Session.GetString("LocationId"),
                        RoleId = httpContext?.Session.GetString("RoleId")
                    }
                });

                await LogErrorAsync(
                    exception,
                    httpContext,
                    taskContextData,
                    $"TaskDatabase-{operation}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log task database error for operation: {Operation}", operation);
            }
        }

        public async Task LogGoogleSyncErrorAsync(
            int taskId,
            string service,
            Exception exception,
            HttpContext? httpContext = null)
        {
            try
            {
                var taskContextData = JsonSerializer.Serialize(new
                {
                    ErrorType = "GoogleSync",
                    ErrorCategory = "Task Management",
                    TaskId = taskId,
                    GoogleService = service,
                    SessionContext = new
                    {
                        UserId = httpContext?.Session.GetString("Users_Id"),
                        Username = httpContext?.Session.GetString("Username"),
                        HasAccessToken = !string.IsNullOrEmpty(httpContext?.Session.GetString("GoogleAccessToken")),
                        HasRefreshToken = !string.IsNullOrEmpty(httpContext?.Session.GetString("GoogleRefreshToken"))
                    }
                });

                await LogErrorAsync(
                    exception,
                    httpContext,
                    taskContextData,
                    $"GoogleSync-{service}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log Google sync error for service: {Service}", service);
            }
        }

        public async Task<List<ErrorLog>> GetTaskRelatedErrorsAsync(int count = 50)
        {
            var errors = new List<ErrorLog>();

            try
            {
                var connectionString = _configuration.GetConnectionString("SalesMetrics");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT TOP (@Count) * 
                    FROM ErrorLog 
                    WHERE Source LIKE 'Task%' 
                       OR Source LIKE '%TaskCreation%' 
                       OR Source LIKE '%TaskValidation%' 
                       OR Source LIKE '%TaskDatabase%' 
                       OR Source LIKE '%GoogleSync%'
                       OR AdditionalData LIKE '%TaskCreation%'
                       OR AdditionalData LIKE '%Task Management%'
                    ORDER BY Timestamp DESC";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Count", count);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    errors.Add(MapFromDataReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve task-related errors");
            }

            return errors;
        }

        public async Task<List<ErrorLog>> GetUnresolvedTaskErrorsAsync()
        {
            var errors = new List<ErrorLog>();

            try
            {
                var connectionString = _configuration.GetConnectionString("SalesMetrics");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT * 
                    FROM ErrorLog 
                    WHERE IsResolved = 0 
                      AND (Source LIKE 'Task%' 
                           OR Source LIKE '%TaskCreation%' 
                           OR Source LIKE '%TaskValidation%' 
                           OR Source LIKE '%TaskDatabase%' 
                           OR Source LIKE '%GoogleSync%'
                           OR AdditionalData LIKE '%TaskCreation%'
                           OR AdditionalData LIKE '%Task Management%')
                    ORDER BY Timestamp DESC";

                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    errors.Add(MapFromDataReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve unresolved task errors");
            }

            return errors;
        }

        public async Task<Dictionary<string, int>> GetTaskErrorStatisticsAsync(int days = 7)
        {
            var statistics = new Dictionary<string, int>();

            try
            {
                var connectionString = _configuration.GetConnectionString("SalesMetrics");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT 
                        COUNT(*) as TotalTaskErrors,
                        COUNT(CASE WHEN IsResolved = 0 THEN 1 END) as UnresolvedTaskErrors,
                        COUNT(CASE WHEN Level = 'Error' THEN 1 END) as TaskErrors,
                        COUNT(CASE WHEN Level = 'Warning' THEN 1 END) as TaskWarnings,
                        COUNT(CASE WHEN Source LIKE '%TaskCreation%' THEN 1 END) as CreationErrors,
                        COUNT(CASE WHEN Source LIKE '%TaskValidation%' THEN 1 END) as ValidationErrors,
                        COUNT(CASE WHEN Source LIKE '%TaskDatabase%' THEN 1 END) as DatabaseErrors,
                        COUNT(CASE WHEN Source LIKE '%GoogleSync%' THEN 1 END) as SyncErrors
                    FROM ErrorLog 
                    WHERE Timestamp >= DATEADD(day, -@Days, GETDATE())
                      AND (Source LIKE 'Task%' 
                           OR Source LIKE '%TaskCreation%' 
                           OR Source LIKE '%TaskValidation%' 
                           OR Source LIKE '%TaskDatabase%' 
                           OR Source LIKE '%GoogleSync%'
                           OR AdditionalData LIKE '%TaskCreation%'
                           OR AdditionalData LIKE '%Task Management%')";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Days", days);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    statistics["TotalTaskErrors"] = reader.GetInt32("TotalTaskErrors");
                    statistics["UnresolvedTaskErrors"] = reader.GetInt32("UnresolvedTaskErrors");
                    statistics["TaskErrors"] = reader.GetInt32("TaskErrors");
                    statistics["TaskWarnings"] = reader.GetInt32("TaskWarnings");
                    statistics["CreationErrors"] = reader.GetInt32("CreationErrors");
                    statistics["ValidationErrors"] = reader.GetInt32("ValidationErrors");
                    statistics["DatabaseErrors"] = reader.GetInt32("DatabaseErrors");
                    statistics["SyncErrors"] = reader.GetInt32("SyncErrors");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve task error statistics");
            }

            return statistics;
        }

        // Helper method to map data reader results using your existing method
        private ErrorLog MapFromDataReader(SqlDataReader reader)
        {
            return new ErrorLog
            {
                ErrorID = reader.GetInt64("ErrorID"),
                Timestamp = reader.GetDateTime("Timestamp"),
                Level = reader.GetString("Level"),
                Message = reader.GetString("Message"),
                Exception = reader.IsDBNull("Exception") ? null : reader.GetString("Exception"),
                InnerException = reader.IsDBNull("InnerException") ? null : reader.GetString("InnerException"),
                RequestId = reader.IsDBNull("RequestId") ? null : reader.GetString("RequestId"),
                TraceId = reader.IsDBNull("TraceId") ? null : reader.GetString("TraceId"),
                SessionID = reader.IsDBNull("SessionID") ? null : reader.GetString("SessionID"),
                UserID = reader.IsDBNull("UserID") ? null : reader.GetInt32("UserID"),
                Username = reader.IsDBNull("Username") ? null : reader.GetString("Username"),
                UserAgent = reader.IsDBNull("UserAgent") ? null : reader.GetString("UserAgent"),
                RequestPath = reader.IsDBNull("RequestPath") ? null : reader.GetString("RequestPath"),
                RequestMethod = reader.IsDBNull("RequestMethod") ? null : reader.GetString("RequestMethod"),
                QueryString = reader.IsDBNull("QueryString") ? null : reader.GetString("QueryString"),
                IPAddress = reader.IsDBNull("IPAddress") ? null : reader.GetString("IPAddress"),
                Controller = reader.IsDBNull("Controller") ? null : reader.GetString("Controller"),
                Action = reader.IsDBNull("Action") ? null : reader.GetString("Action"),
                Area = reader.IsDBNull("Area") ? null : reader.GetString("Area"),
                AdditionalData = reader.IsDBNull("AdditionalData") ? null : reader.GetString("AdditionalData"),
                Source = reader.IsDBNull("Source") ? null : reader.GetString("Source"),
                IsResolved = reader.GetBoolean("IsResolved"),
                ResolvedBy = reader.IsDBNull("ResolvedBy") ? null : reader.GetString("ResolvedBy"),
                ResolvedDate = reader.IsDBNull("ResolvedDate") ? null : reader.GetDateTime("ResolvedDate"),
                ResolutionNotes = reader.IsDBNull("ResolutionNotes") ? null : reader.GetString("ResolutionNotes")
            };
        }
    }

    /// <summary>
    /// Extension methods for easier task error logging in controllers
    /// These use your existing centralized error logging system
    /// </summary>
    public static class TaskErrorLoggingExtensions
    {
        public static async Task LogTaskCreationErrorAsync(this Controller controller,
            TaskCreateViewModel taskData, Exception exception, string? additionalContext = null)
        {
            var errorLoggingService = controller.HttpContext.RequestServices
                .GetRequiredService<IEnhancedErrorLoggingService>();
            await errorLoggingService.LogTaskCreationErrorAsync(taskData, exception,
                controller.HttpContext, additionalContext);
        }

        public static async Task LogTaskCreationWarningAsync(this Controller controller,
            TaskCreateViewModel taskData, string warningMessage, string? additionalContext = null)
        {
            var errorLoggingService = controller.HttpContext.RequestServices
                .GetRequiredService<IEnhancedErrorLoggingService>();
            await errorLoggingService.LogTaskCreationWarningAsync(taskData, warningMessage,
                controller.HttpContext, additionalContext);
        }

        public static async Task LogTaskValidationErrorAsync(this Controller controller,
            TaskCreateViewModel taskData, List<string> validationErrors)
        {
            var errorLoggingService = controller.HttpContext.RequestServices
                .GetRequiredService<IEnhancedErrorLoggingService>();
            await errorLoggingService.LogTaskValidationErrorAsync(taskData, validationErrors,
                controller.HttpContext);
        }

        public static async Task LogTaskDatabaseErrorAsync(this Controller controller,
            SalesTask taskData, Exception exception, string operation = "Save")
        {
            var errorLoggingService = controller.HttpContext.RequestServices
                .GetRequiredService<IEnhancedErrorLoggingService>();
            await errorLoggingService.LogTaskDatabaseErrorAsync(taskData, exception,
                controller.HttpContext, operation);
        }

        public static async Task LogGoogleSyncErrorAsync(this Controller controller,
            int taskId, string service, Exception exception)
        {
            var errorLoggingService = controller.HttpContext.RequestServices
                .GetRequiredService<IEnhancedErrorLoggingService>();
            await errorLoggingService.LogGoogleSyncErrorAsync(taskId, service, exception,
                controller.HttpContext);
        }
    }
}
    