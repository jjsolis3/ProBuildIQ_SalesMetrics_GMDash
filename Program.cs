using System.Data.SqlClient;
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
using SalesMetrics.Services.Settings;
using SalesMetrics.Services.Signing;
using SalesMetrics.Services.Notifications;
using SalesMetrics.Hubs;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SalesMetrics");

// Add services to the container
builder.Services.AddControllersWithViews()
    .AddSessionStateTempDataProvider(); // Store TempData in Session instead of cookies

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
// QUERY BUILDER - Multi-Source Data Adapters
// ============================================================

// Register all data source adapters as Scoped (per HTTP request)
builder.Services.AddScoped<SalesMetrics.Services.Reports.QueryBuilder.DataSources.SqlDataSourceAdapter>();
builder.Services.AddScoped<SalesMetrics.Services.Reports.QueryBuilder.DataSources.ApiDataSourceAdapter>();
builder.Services.AddScoped<SalesMetrics.Services.Reports.QueryBuilder.DataSources.KuduDataSourceAdapter>();

// Register data source registry as Singleton (factory pattern)
// Registry resolves adapters on-demand from IServiceProvider
builder.Services.AddSingleton<SalesMetrics.Services.Reports.QueryBuilder.DataSources.IDataSourceRegistry, SalesMetrics.Services.Reports.QueryBuilder.DataSources.DataSourceRegistry>();

// Add HttpClient for API adapter (for future REST/GraphQL API integration)
builder.Services.AddHttpClient("ErpApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    // BaseUrl will be configured in appsettings.json when API is ready
});

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
builder.Services.AddScoped<SalesMetrics.Services.Signing.IBrandingSettingsProvider, SalesMetrics.Services.Signing.BrandingSettingsProvider>(); // DB-first branding settings
builder.Services.AddScoped<SalesMetrics.Services.Settings.IAppCredentialsProvider, SalesMetrics.Services.Settings.AppCredentialsProvider>(); // Encrypted credential store
builder.Services.AddScoped<SalesMetrics.Services.Signing.ISmtpSettingsProvider, SalesMetrics.Services.Signing.SmtpSettingsProvider>(); // DB-first SMTP settings
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>(); // Branded email template wrapper
builder.Services.AddScoped<SalesMetrics.Services.Notifications.INotificationService, SalesMetrics.Services.Notifications.NotificationService>(); // In-app notification service
builder.Services.AddScoped<IEnvelopeService, EnvelopeService>();

// Announcements and Settings services
builder.Services.AddScoped<SalesMetrics.Services.Announcements.IAnnouncementService, SalesMetrics.Services.Announcements.AnnouncementService>();
builder.Services.AddScoped<SalesMetrics.Services.Settings.ISettingsService, SalesMetrics.Services.Settings.SettingsService>();

// Feature Permissions Service
builder.Services.AddScoped<SalesMetrics.Services.Permissions.IPermissionService, SalesMetrics.Services.Permissions.PermissionService>();

// ── Google OAuth: bootstrap credentials from DB (or fall back to appsettings on first run) ──
// AddGoogle() must be called before builder.Build(), so the DI container is not yet available.
// We use raw ADO.NET to read the encrypted credentials from AppCredentials at startup.
static string BootstrapCredential(string connStr, string encKey, string credKey, string fallback)
{
    try
    {
        using var conn = new SqlConnection(connStr);
        conn.Open();
        using var cmd = new SqlCommand(
            "SELECT EncryptedValue FROM AppCredentials WHERE CredentialKey = @k", conn);
        cmd.Parameters.AddWithValue("@k", credKey);
        var val = cmd.ExecuteScalar()?.ToString();
        if (!string.IsNullOrWhiteSpace(val))
            return AesEncryption.Decrypt(val, encKey);
    }
    catch
    {
        // Table may not exist yet on first boot — fall back to appsettings value.
    }
    return fallback;
}

var encKey         = builder.Configuration["Encryption:Key"] ?? "";
var smConnStr      = builder.Configuration.GetConnectionString("SalesMetrics") ?? "";
var googleClientId = BootstrapCredential(smConnStr, encKey, "Google_ClientId",
    builder.Configuration["Authentication:Google:ClientId"] ?? "");
var googleClientSecret = BootstrapCredential(smConnStr, encKey, "Google_ClientSecret",
    builder.Configuration["Authentication:Google:ClientSecret"] ?? "");

// Google OAuth + Cookie Auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Google";
})
.AddGoogle("Google", options =>
{
    // If credentials haven't been saved in the DB yet, use non-empty placeholders so
    // the app starts cleanly. Google OAuth will fail its redirect until real credentials
    // are entered via Settings → App Credentials (which requires an app pool restart).
    options.ClientId     = string.IsNullOrWhiteSpace(googleClientId)     ? "PENDING_CONFIGURATION" : googleClientId;
    options.ClientSecret = string.IsNullOrWhiteSpace(googleClientSecret) ? "PENDING_CONFIGURATION" : googleClientSecret;

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

// ── SQL Migration Runner ──────────────────────────────────────────────────────
// Runs *.sql files in the Migrations folder exactly once, tracking applied
// files in a lightweight [_MigrationsApplied] table. Skips already-applied
// files so broken/legacy scripts never run again after their first attempt.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SalesMetrics.Data.SalesMetricsDbContext>();
    var migrationsPath = Path.Combine(app.Environment.ContentRootPath, "Migrations");

    if (Directory.Exists(migrationsPath))
    {
        // Ensure the tracking table exists (bootstrapped separately, not via a migration file)
        db.Database.ExecuteSqlRaw(@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.tables
                WHERE name = '_MigrationsApplied' AND schema_id = SCHEMA_ID('dbo')
            )
            BEGIN
                CREATE TABLE [dbo].[_MigrationsApplied] (
                    [MigrationFile] NVARCHAR(260) NOT NULL,
                    [AppliedAtUtc]  DATETIME       NOT NULL DEFAULT GETUTCDATE(),
                    [Succeeded]     BIT            NOT NULL DEFAULT 1,
                    CONSTRAINT [PK__MigrationsApplied] PRIMARY KEY ([MigrationFile])
                );
            END");

        // Load the set of already-applied file names
        var applied = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            if (cmd.Connection!.State != System.Data.ConnectionState.Open)
                cmd.Connection.Open();
            cmd.CommandText = "SELECT [MigrationFile] FROM [dbo].[_MigrationsApplied]";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) applied.Add(reader.GetString(0));
        }

        var goSplitter = new System.Text.RegularExpressions.Regex(
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var file in Directory.GetFiles(migrationsPath, "*.sql").OrderBy(Path.GetFileName))
        {
            var fileName = Path.GetFileName(file);
            if (applied.Contains(fileName))
                continue; // already ran — skip

            bool succeeded = true;
            try
            {
                var sql = File.ReadAllText(file);
                foreach (var batch in goSplitter.Split(sql))
                {
                    var trimmed = batch.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        db.Database.ExecuteSqlRaw(trimmed);
                }
                Console.WriteLine($"[Migration] Applied: {fileName}");
            }
            catch (Exception ex)
            {
                succeeded = false;
                // Log but don't crash startup
                Console.WriteLine($"[Migration] Failed: {fileName} — {ex.Message}");
            }

            // Record the attempt (succeeded or not) so it won't run again
            try
            {
                db.Database.ExecuteSqlRaw(
                    "INSERT INTO [dbo].[_MigrationsApplied] ([MigrationFile],[Succeeded]) VALUES ({0},{1})",
                    fileName, succeeded);
            }
            catch
            {
                // Ignore tracking insert errors
            }
        }
    }
}
// ─────────────────────────────────────────────────────────────────────────────

// Read config (if needed elsewhere)
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Configure the HTTP request pipeline
// Use custom error handling middleware that logs to database
app.UseMiddleware<SalesMetrics.Middleware.GlobalErrorHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Enable Response Compression
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}

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

// ✅ Initialize Rotativa for PDF rendering (moved to Tools folder to avoid wwwroot bloat)
Rotativa.AspNetCore.RotativaConfiguration.Setup(
    app.Environment.ContentRootPath,  // Root: C:\ProBuildIQ\ProBuildIQ SalesMetrics
    "Tools/Rotativa"                   // Relative path from root
);

app.Run();
