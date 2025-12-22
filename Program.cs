using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.DependencyInjection;
using SalesMetrics.Data;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Services.Erp;
using SalesMetrics.Services.Reports;
using SalesMetrics.Services.Mvc;
using SalesMetrics.Services.Signing;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SalesMetrics");

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IReportCatalog, ReportCatalog>();
builder.Services.AddSingleton<IReportRunner, ReportRunner>();
builder.Services.AddSingleton<IReportAuthorizationService, ReportAuthorizationService>();
builder.Services.AddSingleton<IReportExportService, ReportExportService>();
builder.Services.AddSingleton<IErpDataClient, SqlServerErpDataClient>();
builder.Services.AddSingleton<HttpErpDataClient>();

// Add EF Core DbContext for SalesMetrics
builder.Services.AddDbContext<SalesMetricsDbContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();
builder.Services.AddScoped<IErpMergeService, ErpMergeService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEnvelopeService, EnvelopeService>();

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

// ✅ Initialize Rotativa for PDF rendering
Rotativa.AspNetCore.RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

app.Run();
