using SalesMetrics.Services;
using System.Net;
using System.Text.Json;

namespace SalesMetrics.Middleware
{
    public class GlobalErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalErrorHandlingMiddleware> _logger;

        public GlobalErrorHandlingMiddleware(RequestDelegate next,
                                             ILogger<GlobalErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");

                // Resolve scoped logger per request (your existing approach)
                var errorLoggingService =
                    context.RequestServices.GetRequiredService<IErrorLoggingService>();
                await errorLoggingService.LogErrorAsync(ex, context, source: "GlobalMiddleware");

                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // If the response already started, we can’t change headers/body safely.
            if (context.Response.HasStarted)
            {
                var logger = context.RequestServices
                                    .GetRequiredService<ILogger<GlobalErrorHandlingMiddleware>>();
                logger.LogWarning("Response has already started; skipping error body.");
                return;
            }

            // It’s still safe to change the response: clear and write a fresh JSON payload
            context.Response.Clear();

            // Map status code
            context.Response.StatusCode = exception switch
            {
                ArgumentNullException => (int)HttpStatusCode.BadRequest,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                _ => (int)HttpStatusCode.InternalServerError
            };

            context.Response.ContentType = "application/json; charset=utf-8";

            var env = context.RequestServices.GetService<IWebHostEnvironment>();
            var response = new
            {
                error = new
                {
                    message = "An internal server error occurred.",
                    details = env?.IsDevelopment() == true
                        ? exception.Message
                        : "Please contact support if this problem persists.",
                    timestamp = DateTime.UtcNow,
                    requestId = context.TraceIdentifier
                }
            };

            await context.Response.WriteAsJsonAsync(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
    }
}
