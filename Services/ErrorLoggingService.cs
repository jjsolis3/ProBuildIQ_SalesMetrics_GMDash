using Microsoft.Data.SqlClient;
using SalesMetrics.Models;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace SalesMetrics.Services
{
    public class ErrorLoggingService : IErrorLoggingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ErrorLoggingService> _logger;

        public ErrorLoggingService(IConfiguration configuration, ILogger<ErrorLoggingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task LogErrorAsync(Exception exception, HttpContext? httpContext = null, string? additionalData = null, string source = "Application")
        {
            var errorLog = new ErrorLog
            {
                Level = "Error",
                Message = exception.Message,
                Exception = exception.ToString(),
                InnerException = exception.InnerException?.ToString(),
                Source = source,
                AdditionalData = additionalData
            };

            PopulateContextData(errorLog, httpContext);
            await SaveErrorLogAsync(errorLog);
        }

        public async Task LogErrorAsync(string message, string level = "Error", HttpContext? httpContext = null, string? additionalData = null, string source = "Application")
        {
            var errorLog = new ErrorLog
            {
                Level = level,
                Message = message,
                Source = source,
                AdditionalData = additionalData
            };

            PopulateContextData(errorLog, httpContext);
            await SaveErrorLogAsync(errorLog);
        }

        public async Task LogWarningAsync(string message, HttpContext? httpContext = null, string? additionalData = null, string source = "Application")
        {
            await LogErrorAsync(message, "Warning", httpContext, additionalData, source);
        }

        public async Task LogInformationAsync(string message, HttpContext? httpContext = null, string? additionalData = null, string source = "Application")
        {
            await LogErrorAsync(message, "Information", httpContext, additionalData, source);
        }

        private void PopulateContextData(ErrorLog errorLog, HttpContext? httpContext)
        {
            if (httpContext == null) return;

            try
            {
                // Add this check before accessing Session
                if (httpContext.Session == null)
                {
                    _logger.LogWarning("Session not available for error logging context");
                    return;
                }

                // Request information
                errorLog.RequestId = httpContext.TraceIdentifier;
                errorLog.RequestPath = httpContext.Request.Path.Value;
                errorLog.RequestMethod = httpContext.Request.Method;
                errorLog.QueryString = httpContext.Request.QueryString.Value;
                errorLog.UserAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
                errorLog.IPAddress = GetClientIPAddress(httpContext);
                errorLog.SessionID = httpContext.Session?.Id;

                // User information
                if (httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    errorLog.Username = httpContext.User.Identity.Name;

                    // Extract UserID from claims (adjust based on your claim structure)
                    var userIdClaim = httpContext.User.FindFirst("Users_ID") ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        errorLog.UserID = userId;
                    }
                }

                // Route information
                var routeData = httpContext.GetRouteData();
                if (routeData != null)
                {
                    errorLog.Controller = routeData.Values["controller"]?.ToString();
                    errorLog.Action = routeData.Values["action"]?.ToString();
                    errorLog.Area = routeData.Values["area"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                // Don't let context population errors prevent error logging
                _logger.LogWarning(ex, "Failed to populate error context data");
            }
        }

        private string? GetClientIPAddress(HttpContext httpContext)
        {
            // This handles various proxy scenarios to get the real client IP
            var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
                ipAddress = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            return ipAddress;
        }

        private async Task SaveErrorLogAsync(ErrorLog errorLog)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("SalesMetrics");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    INSERT INTO ErrorLog 
                    (Timestamp, Level, Message, Exception, InnerException, RequestId, TraceId, SessionID, 
                     UserID, Username, UserAgent, RequestPath, RequestMethod, QueryString, IPAddress, 
                     Controller, Action, Area, AdditionalData, Source)
                    VALUES 
                    (@Timestamp, @Level, @Message, @Exception, @InnerException, @RequestId, @TraceId, @SessionID,
                     @UserID, @Username, @UserAgent, @RequestPath, @RequestMethod, @QueryString, @IPAddress,
                     @Controller, @Action, @Area, @AdditionalData, @Source)";

                using var command = new SqlCommand(sql, connection);

                // Add parameters - this prevents SQL injection and handles null values properly
                command.Parameters.AddWithValue("@Timestamp", errorLog.Timestamp);
                command.Parameters.AddWithValue("@Level", errorLog.Level);
                command.Parameters.AddWithValue("@Message", errorLog.Message);
                command.Parameters.AddWithValue("@Exception", (object?)errorLog.Exception ?? DBNull.Value);
                command.Parameters.AddWithValue("@InnerException", (object?)errorLog.InnerException ?? DBNull.Value);
                command.Parameters.AddWithValue("@RequestId", (object?)errorLog.RequestId ?? DBNull.Value);
                command.Parameters.AddWithValue("@TraceId", (object?)errorLog.TraceId ?? DBNull.Value);
                command.Parameters.AddWithValue("@SessionID", (object?)errorLog.SessionID ?? DBNull.Value);
                command.Parameters.AddWithValue("@UserID", (object?)errorLog.UserID ?? DBNull.Value);
                command.Parameters.AddWithValue("@Username", (object?)errorLog.Username ?? DBNull.Value);
                command.Parameters.AddWithValue("@UserAgent", (object?)errorLog.UserAgent ?? DBNull.Value);
                command.Parameters.AddWithValue("@RequestPath", (object?)errorLog.RequestPath ?? DBNull.Value);
                command.Parameters.AddWithValue("@RequestMethod", (object?)errorLog.RequestMethod ?? DBNull.Value);
                command.Parameters.AddWithValue("@QueryString", (object?)errorLog.QueryString ?? DBNull.Value);
                command.Parameters.AddWithValue("@IPAddress", (object?)errorLog.IPAddress ?? DBNull.Value);
                command.Parameters.AddWithValue("@Controller", (object?)errorLog.Controller ?? DBNull.Value);
                command.Parameters.AddWithValue("@Action", (object?)errorLog.Action ?? DBNull.Value);
                command.Parameters.AddWithValue("@Area", (object?)errorLog.Area ?? DBNull.Value);
                command.Parameters.AddWithValue("@AdditionalData", (object?)errorLog.AdditionalData ?? DBNull.Value);
                command.Parameters.AddWithValue("@Source", errorLog.Source);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Fallback to standard logging if database logging fails
                _logger.LogError(ex, "Failed to save error log to database. Original error: {OriginalMessage}", errorLog.Message);
            }
        }

        public async Task<List<ErrorLog>> GetRecentErrorsAsync(int count = 50)
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
                _logger.LogError(ex, "Failed to retrieve recent errors");
            }

            return errors;
        }

        public async Task<List<ErrorLog>> GetErrorsByDateRangeAsync(DateTime startDate, DateTime endDate)
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
                    WHERE Timestamp >= @StartDate AND Timestamp <= @EndDate
                    ORDER BY Timestamp DESC";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@StartDate", startDate);
                command.Parameters.AddWithValue("@EndDate", endDate);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    errors.Add(MapFromDataReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve errors by date range");
            }

            return errors;
        }

        public async Task MarkAsResolvedAsync(long errorId, string resolvedBy, string? resolutionNotes = null)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("SalesMetrics");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    UPDATE ErrorLog 
                    SET IsResolved = 1, 
                        ResolvedBy = @ResolvedBy, 
                        ResolvedDate = GETDATE(), 
                        ResolutionNotes = @ResolutionNotes
                    WHERE ErrorID = @ErrorID";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ErrorID", errorId);
                command.Parameters.AddWithValue("@ResolvedBy", resolvedBy);
                command.Parameters.AddWithValue("@ResolutionNotes", (object?)resolutionNotes ?? DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark error as resolved");
            }
        }

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
}
