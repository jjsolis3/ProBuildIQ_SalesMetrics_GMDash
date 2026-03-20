namespace SalesMetrics.Middleware
{
    /// <summary>
    /// Adds security-related HTTP response headers to every response.
    /// Register early in the pipeline (before UseStaticFiles) so that even
    /// static assets are covered.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // Prevent MIME-type sniffing
            headers["X-Content-Type-Options"] = "nosniff";

            // Block clickjacking — deny framing entirely
            headers["X-Frame-Options"] = "DENY";

            // Legacy XSS filter (IE/older Edge) — kept for defence-in-depth
            headers["X-XSS-Protection"] = "1; mode=block";

            // Referrer policy — send origin only on same-site requests
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Disable browser features not used by this application
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // Content-Security-Policy
            // Allows scripts/styles from self and the CDNs already used in the app.
            // 'unsafe-inline' is required while inline scripts/styles are present;
            // migrate to nonces/hashes to tighten further.
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' " +
                    "https://cdn.jsdelivr.net " +
                    "https://cdnjs.cloudflare.com " +
                    "https://cdn.datatables.net " +
                    "https://ajax.googleapis.com; " +
                "style-src 'self' 'unsafe-inline' " +
                    "https://cdn.jsdelivr.net " +
                    "https://cdnjs.cloudflare.com " +
                    "https://cdn.datatables.net " +
                    "https://fonts.googleapis.com; " +
                "font-src 'self' " +
                    "https://fonts.gstatic.com " +
                    "https://cdnjs.cloudflare.com; " +
                "img-src 'self' data: https: blob:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'; " +
                "base-uri 'self';";

            await _next(context);
        }
    }
}
