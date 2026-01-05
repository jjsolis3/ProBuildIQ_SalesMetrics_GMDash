using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.DependencyInjection;
using SalesMetrics.Data;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Services.Erp;
using SalesMetrics.Services.Erp.Configuration;
using SalesMetrics.Services.Erp.Clients;
using SalesMetrics.Services;
using SalesMetrics.Services.Reports;
using SalesMetrics.Services.Mvc;
using SalesMetrics.Services.Signing;
using SalesMetrics.Services.Notifications;
using SalesMetrics.Hubs;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SalesMetrics");

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(); // Add SignalR

// Add Memory Cache for performance optimization
builder.Services.AddMemoryCache();

// Add Response Compression for faster data transfer
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = new[]
    {
        "text/html",
        "text/css",
        "text/javascript",
        "application/javascript",
        "application/json",
        "text/json"
    };
});

builder.Services.AddSingleton<IReportCatalog, ReportCatalog>();
builder.Services.AddSingleton<IReportRunner, ReportRunner>();
builder.Services.AddSingleton<IReportAuthorizationService, ReportAuthorizationService>();
builder.Services.AddSingleton<IReportExportService, ReportExportService>();

// ============================================================
// ERP ABSTRACTION LAYER - Multi-Provider Support
// ============================================================

// Configure ERP settings from appsettings.json
builder.Services.Configure<ErpSettings>(builder.Configuration.GetSection("ErpSettings"));

// Register ERP client implementations
builder.Services.AddSingleton<CompUFloorErpClient>();
builder.Services.AddSingleton<KuduErpClient>();
builder.Services.AddHttpClient<KuduErpClient>(); // Add HTTP client for Kudu API

// Register ERP client factory for provider selection
builder.Services.AddSingleton<ErpClientFactory>();

// Register default IErpDataClient (uses factory internally)
// This is for backward compatibility - controllers can still inject IErpDataClient
builder.Services.AddSingleton<IErpDataClient>(sp =>
{
    // Default to CompUFloor for now
    return sp.GetRequiredService<CompUFloorErpClient>();
});

// Keep legacy clients for backward compatibility during migration
builder.Services.AddSingleton<SqlServerErpDataClient>();
builder.Services.AddSingleton<HttpErpDataClient>();

// Add EF Core DbContext for SalesMetrics
builder.Services.AddDbContext<SalesMetricsDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add Error Logging Service
builder.Services.AddScoped<IErrorLoggingService, ErrorLoggingService>();

// Add Session Services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(90);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register IConfiguration to be accessible in controllers
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// Signing add-on services
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));
builder.Services.Configure<CompanyBrandingSettings>(builder.Configuration.GetSection("CompanyBranding"));
builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();
builder.Services.AddScoped<IErpMergeService, ErpMergeService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<SalesMetrics.Services.Signing.INotificationService, SalesMetrics.Services.Signing.NotificationService>(); // Email notification service
builder.Services.AddScoped<SalesMetrics.Services.Notifications.INotificationService, SalesMetrics.Services.Notifications.NotificationService>(); // In-app notification service
builder.Services.AddScoped<IEnvelopeService, EnvelopeService>();

// Announcements and Settings services
builder.Services.AddScoped<SalesMetrics.Services.Announcements.IAnnouncementService, SalesMetrics.Services.Announcements.AnnouncementService>();
builder.Services.AddScoped<SalesMetrics.Services.Settings.ISettingsService, SalesMetrics.Services.Settings.SettingsService>();

// Google OAuth + Cookie Auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Google";
})
.AddGoogle("Google", options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("https://www.googleapis.com/auth/calendar");
    options.Scope.Add("https://www.googleapis.com/auth/calendar.events");
    options.Scope.Add("https://www.googleapis.com/auth/tasks"); // ✅ ADD THIS LINE

    options.SaveTokens = true;
    options.AccessType = "offline";
})
.AddCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(90); // 15-min expiration
    options.SlidingExpiration = true; // Refresh Activity
    options.LoginPath = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/Login";
});

var app = builder.Build();

// Read config (if needed elsewhere)
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Enable Response Compression
app.UseResponseCompression();

app.UseStaticFiles();
app.UseRouting();

// Add Session Middleware
app.UseSession();

app.UseMiddleware<SessionValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<NotificationHub>("/notificationHub");

// ✅ Initialize Rotativa for PDF rendering
Rotativa.AspNetCore.RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

app.Run();
