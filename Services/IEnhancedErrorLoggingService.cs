using SalesMetrics.Models;

namespace SalesMetrics.Services
{
    /// <summary>
    /// Enhanced interface that extends your existing IErrorLoggingService with task-specific methods
    /// This keeps everything in your centralized ErrorLog table - NO separate tables needed
    /// </summary>
    public interface IEnhancedErrorLoggingService : IErrorLoggingService
    {
        // Task-specific convenience methods that use your existing ErrorLog table
        Task LogTaskCreationErrorAsync(TaskCreateViewModel taskData, Exception exception, HttpContext? httpContext = null, string? additionalContext = null);
        Task LogTaskCreationWarningAsync(TaskCreateViewModel taskData, string warningMessage, HttpContext? httpContext = null, string? additionalContext = null);
        Task LogTaskValidationErrorAsync(TaskCreateViewModel taskData, List<string> validationErrors, HttpContext? httpContext = null);
        Task LogTaskDatabaseErrorAsync(SalesTask taskData, Exception exception, HttpContext? httpContext = null, string operation = "Save");
        Task LogGoogleSyncErrorAsync(int taskId, string service, Exception exception, HttpContext? httpContext = null);

        // Enhanced querying methods for task-related errors from your existing ErrorLog table
        Task<List<ErrorLog>> GetTaskRelatedErrorsAsync(int count = 50);
        Task<List<ErrorLog>> GetUnresolvedTaskErrorsAsync();
        Task<Dictionary<string, int>> GetTaskErrorStatisticsAsync(int days = 7);
    }
}
