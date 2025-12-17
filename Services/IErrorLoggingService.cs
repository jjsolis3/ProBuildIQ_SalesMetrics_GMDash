using SalesMetrics.Models;

namespace SalesMetrics.Services
{
    public interface IErrorLoggingService
    {
        Task LogErrorAsync(Exception exception, HttpContext? httpContext = null, string? additionalData = null, string source = "Application");
        Task LogErrorAsync(string message, string level = "Error", HttpContext? httpContext = null, string? additionalData = null, string source = "Application");
        Task LogWarningAsync(string message, HttpContext? httpContext = null, string? additionalData = null, string source = "Application");
        Task LogInformationAsync(string message, HttpContext? httpContext = null, string? additionalData = null, string source = "Application");
        Task<List<ErrorLog>> GetRecentErrorsAsync(int count = 50);
        Task<List<ErrorLog>> GetErrorsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task MarkAsResolvedAsync(long errorId, string resolvedBy, string? resolutionNotes = null);
    }    
}
