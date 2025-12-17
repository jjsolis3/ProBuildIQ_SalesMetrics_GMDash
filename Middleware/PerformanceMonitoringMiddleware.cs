using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace SalesMetrics.Middleware
{
    public class PerformanceMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
        private const int SlowRequestThresholdMs = 3000; // 3 seconds

        public PerformanceMonitoringMiddleware(
            RequestDelegate next,
            ILogger<PerformanceMonitoringMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var path = context.Request.Path;
            var method = context.Request.Method;

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var elapsedMs = sw.ElapsedMilliseconds;
                var statusCode = context.Response.StatusCode;

                // Only monitor GM Dashboard endpoints
                if (path.StartsWithSegments("/GMDash", StringComparison.OrdinalIgnoreCase))
                {
                    var level = elapsedMs > SlowRequestThresholdMs ? LogLevel.Warning : LogLevel.Information;
                    _logger.Log(level,
                        "GMDash Request: {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
                        method, path, elapsedMs, statusCode);

                    // Add header if still possible
                    if (!context.Response.HasStarted)
                    {
                        context.Response.Headers["X-Response-Time"] = $"{elapsedMs}ms";
                    }
                }

                // Generic slow-request logging
                if (elapsedMs > SlowRequestThresholdMs)
                {
                    var qs = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
                    _logger.LogWarning(
                        "Slow request detected: {Method} {Path}{QueryString} took {ElapsedMs}ms (Status: {StatusCode})",
                        method, path, qs, elapsedMs, statusCode);
                }

                // Error responses
                if (statusCode >= 400)
                {
                    // Optional: skip noisy 404s for source maps
                    var isSourceMap404 = statusCode == 404
                        && path.StartsWithSegments("/assets/libs", StringComparison.OrdinalIgnoreCase)
                        && path.Value!.EndsWith(".map", StringComparison.OrdinalIgnoreCase);

                    if (!isSourceMap404)
                    {
                        _logger.LogError("Error response: {Method} {Path} returned {StatusCode} in {ElapsedMs}ms",
                            method, path, statusCode, elapsedMs);
                    }
                }
            }
        }

        // Optional: Send performance metrics to external service
        private async Task SendToAnalytics(string path, long elapsedMs, int statusCode)
        {
            try
            {
                // Example: Send to Application Insights, Datadog, etc.
                // This is optional and can be implemented based on your monitoring needs
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send analytics data");
            }
        }
    }
}