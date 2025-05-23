using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Threading.Tasks;

public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;

        if (isAuthenticated)
        {
            var userIdSession = context.Session.GetString("UserId");
            var officeLocationSession = context.Session.GetString("OfficeLocation");

            // If session has expired but user is still authenticated via cookie
            if (string.IsNullOrEmpty(userIdSession) || string.IsNullOrEmpty(officeLocationSession))
            {
                // Clear auth + session
                await context.SignOutAsync();
                context.Session.Clear();

                // Redirect to login
                context.Response.Redirect("/Auth/Login");
                return;
            }
        }

        // Continue the request pipeline
        await _next(context);
    }
}
