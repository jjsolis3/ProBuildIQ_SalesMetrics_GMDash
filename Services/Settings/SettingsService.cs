using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;

namespace SalesMetrics.Services.Settings
{
    public class SettingsService : ISettingsService
    {
        private readonly SalesMetricsDbContext _context;

        public SettingsService(SalesMetricsDbContext context)
        {
            _context = context;
        }

        public async Task<SettingsDashboardViewModel> GetDashboardDataAsync()
        {
            var notificationSettings = await GetNotificationSettingsAsync();
            var securitySettings = await GetSecuritySettingsAsync();
            var envelopeNotificationSettings = await GetEnvelopeNotificationSettingsAsync();
            var brandingSettings = await GetBrandingSettingsAsync();
            var generalSettings = await GetGeneralSettingsAsync();

            return new SettingsDashboardViewModel
            {
                NotificationSettings = notificationSettings,
                SecuritySettings = securitySettings,
                EnvelopeNotificationSettings = envelopeNotificationSettings,
                BrandingSettings = brandingSettings,
                GeneralSettings = generalSettings,
                ActiveTab = "notifications"
            };
        }

        // ======================================================================
        // Notification Settings
        // ======================================================================

        public async Task<List<NotificationSettingsViewModel>> GetNotificationSettingsAsync()
        {
            var settings = await _context.NotificationSettings
                .OrderBy(ns => ns.CategoryName)
                .ToListAsync();

            return settings.Select(s => MapToNotificationSettingsViewModel(s)).ToList();
        }

        public async Task<NotificationSettingsViewModel?> GetNotificationSettingByIdAsync(int id)
        {
            var setting = await _context.NotificationSettings.FindAsync(id);
            return setting != null ? MapToNotificationSettingsViewModel(setting) : null;
        }

        public async Task<NotificationSettingsViewModel?> GetNotificationSettingByCategoryAsync(string categoryName)
        {
            var setting = await _context.NotificationSettings
                .FirstOrDefaultAsync(ns => ns.CategoryName == categoryName);
            return setting != null ? MapToNotificationSettingsViewModel(setting) : null;
        }

        public async Task UpdateNotificationSettingAsync(UpdateNotificationSettingRequest request, int modifiedByUserId)
        {
            var setting = await _context.NotificationSettings.FindAsync(request.NotificationSettingsId);
            if (setting == null)
                throw new Exception("Notification setting not found");

            setting.IsEnabled = request.IsEnabled;
            setting.NotifyAssignee = request.NotifyAssignee;
            setting.NotifyManager = request.NotifyManager;
            setting.NotifyTaskOwner = request.NotifyTaskOwner;
            setting.EnableInAppNotification = request.EnableInAppNotification;
            setting.EnableEmailNotification = request.EnableEmailNotification;
            setting.ReminderHoursBefore = request.ReminderHoursBefore;
            setting.SpecificStatuses = request.SpecificStatusIds != null ? string.Join(",", request.SpecificStatusIds) : null;
            setting.LastModifiedDate = DateTime.Now;
            setting.LastModifiedByUserId = modifiedByUserId;

            await _context.SaveChangesAsync();
        }

        public async Task InitializeDefaultNotificationSettingsAsync()
        {
            // Check if already initialized
            if (await _context.NotificationSettings.AnyAsync())
                return;

            var defaultSettings = new List<NotificationSettingsEntity>
            {
                new NotificationSettingsEntity
                {
                    CategoryName = "TaskAssigned",
                    IsEnabled = true,
                    NotifyAssignee = true,
                    NotifyManager = false,
                    NotifyTaskOwner = false,
                    EnableInAppNotification = true,
                    EnableEmailNotification = false,
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new NotificationSettingsEntity
                {
                    CategoryName = "StatusChanged",
                    IsEnabled = true,
                    NotifyAssignee = true,
                    NotifyManager = false,
                    NotifyTaskOwner = true,
                    EnableInAppNotification = true,
                    EnableEmailNotification = false,
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new NotificationSettingsEntity
                {
                    CategoryName = "NoteAdded",
                    IsEnabled = true,
                    NotifyAssignee = true,
                    NotifyManager = false,
                    NotifyTaskOwner = false,
                    EnableInAppNotification = true,
                    EnableEmailNotification = false,
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new NotificationSettingsEntity
                {
                    CategoryName = "DueDateReminder",
                    IsEnabled = true,
                    NotifyAssignee = true,
                    NotifyManager = false,
                    NotifyTaskOwner = false,
                    EnableInAppNotification = true,
                    EnableEmailNotification = false,
                    ReminderHoursBefore = 24,
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new NotificationSettingsEntity
                {
                    CategoryName = "DocumentSigning",
                    IsEnabled = true,
                    NotifyAssignee = true,
                    NotifyManager = false,
                    NotifyTaskOwner = false,
                    EnableInAppNotification = true,
                    EnableEmailNotification = false,
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                }
            };

            _context.NotificationSettings.AddRange(defaultSettings);
            await _context.SaveChangesAsync();
        }

        // ======================================================================
        // Security Settings
        // ======================================================================

        public async Task<List<SecuritySettingViewModel>> GetSecuritySettingsAsync()
        {
            var settings = await _context.SecuritySettings
                .Where(ss => ss.Category != "Branding" && ss.Category != "General")
                .OrderBy(ss => ss.Category)
                .ThenBy(ss => ss.SettingKey)
                .ToListAsync();

            return settings.Select(s => MapToSecuritySettingViewModel(s)).ToList();
        }

        public async Task<List<SecuritySettingViewModel>> GetSecuritySettingsByCategoryAsync(string category)
        {
            var settings = await _context.SecuritySettings
                .Where(ss => ss.Category == category)
                .OrderBy(ss => ss.SettingKey)
                .ToListAsync();

            return settings.Select(s => MapToSecuritySettingViewModel(s)).ToList();
        }

        public async Task<SecuritySettingViewModel?> GetSecuritySettingByKeyAsync(string settingKey)
        {
            var setting = await _context.SecuritySettings
                .FirstOrDefaultAsync(ss => ss.SettingKey == settingKey);
            return setting != null ? MapToSecuritySettingViewModel(setting) : null;
        }

        public async Task UpdateSecuritySettingAsync(UpdateSecuritySettingRequest request, int modifiedByUserId)
        {
            var setting = await _context.SecuritySettings.FindAsync(request.SecuritySettingsId);
            if (setting == null)
                throw new Exception("Security setting not found");

            setting.SettingValue = request.SettingValue;
            setting.LastModifiedDate = DateTime.Now;
            setting.LastModifiedByUserId = modifiedByUserId;

            await _context.SaveChangesAsync();
        }

        public async Task<string?> GetSecuritySettingValueAsync(string settingKey)
        {
            var setting = await _context.SecuritySettings
                .FirstOrDefaultAsync(ss => ss.SettingKey == settingKey);
            return setting?.SettingValue;
        }

        public async Task InitializeDefaultSecuritySettingsAsync()
        {
            // Check if already initialized
            if (await _context.SecuritySettings.AnyAsync())
                return;

            var defaultSettings = new List<SecuritySettingsEntity>
            {
                new SecuritySettingsEntity
                {
                    SettingKey = "SessionTimeoutMinutes",
                    SettingValue = "30",
                    Description = "Session timeout in minutes",
                    Category = "Session",
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new SecuritySettingsEntity
                {
                    SettingKey = "PasswordMinLength",
                    SettingValue = "8",
                    Description = "Minimum password length",
                    Category = "Password",
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new SecuritySettingsEntity
                {
                    SettingKey = "RequireSpecialChar",
                    SettingValue = "true",
                    Description = "Require special characters in password",
                    Category = "Password",
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new SecuritySettingsEntity
                {
                    SettingKey = "RequireUppercase",
                    SettingValue = "true",
                    Description = "Require uppercase letters in password",
                    Category = "Password",
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new SecuritySettingsEntity
                {
                    SettingKey = "RequireNumber",
                    SettingValue = "true",
                    Description = "Require numbers in password",
                    Category = "Password",
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                },
                new SecuritySettingsEntity
                {
                    SettingKey = "PasswordExpiryDays",
                    SettingValue = "90",
                    Description = "Password expiry period in days (0 = never)",
                    Category = "Password",
                    LastModifiedDate = DateTime.Now,
                    LastModifiedByUserId = 1
                }
            };

            _context.SecuritySettings.AddRange(defaultSettings);
            await _context.SaveChangesAsync();
        }

        // ======================================================================
        // Envelope Notification Settings
        // ======================================================================

        public async Task<List<EnvelopeNotificationSettingViewModel>> GetEnvelopeNotificationSettingsAsync()
        {
            var settings = await _context.EnvelopeNotificationSettings
                .OrderBy(e => e.EnvelopeNotificationSettingsId)
                .ToListAsync();

            return settings.Select(s => new EnvelopeNotificationSettingViewModel
            {
                EnvelopeNotificationSettingsId = s.EnvelopeNotificationSettingsId,
                LocationCode = s.LocationCode,
                LocationName = s.LocationName,
                NotificationEmail = s.NotificationEmail,
                IsEnabled = s.IsEnabled
            }).ToList();
        }

        public async Task SaveEnvelopeNotificationSettingAsync(SaveEnvelopeNotificationSettingRequest request, int modifiedByUserId)
        {
            var setting = await _context.EnvelopeNotificationSettings.FindAsync(request.EnvelopeNotificationSettingsId);
            if (setting == null)
                throw new Exception("Envelope notification setting not found");

            setting.NotificationEmail = string.IsNullOrWhiteSpace(request.NotificationEmail)
                ? null
                : request.NotificationEmail.Trim();
            setting.IsEnabled = request.IsEnabled;
            setting.LastModifiedDate = DateTime.Now;
            setting.LastModifiedByUserId = modifiedByUserId;

            await _context.SaveChangesAsync();
        }

        public async Task InitializeDefaultEnvelopeNotificationSettingsAsync()
        {
            var defaultRows = new[]
            {
                new { LocationCode = (string?)null, LocationName = "Company (All Branches)" },
                new { LocationCode = (string?)"LAX",  LocationName = "Los Angeles" },
                new { LocationCode = (string?)"LSV",  LocationName = "Las Vegas" },
                new { LocationCode = (string?)"CHN",  LocationName = "Chino" },
                new { LocationCode = (string?)"PHX",  LocationName = "Phoenix" },
                new { LocationCode = (string?)"SND",  LocationName = "San Diego" }
            };

            foreach (var row in defaultRows)
            {
                bool exists = row.LocationCode == null
                    ? await _context.EnvelopeNotificationSettings.AnyAsync(e => e.LocationCode == null)
                    : await _context.EnvelopeNotificationSettings.AnyAsync(e => e.LocationCode == row.LocationCode);

                if (!exists)
                {
                    _context.EnvelopeNotificationSettings.Add(new EnvelopeNotificationSettingsEntity
                    {
                        LocationCode = row.LocationCode,
                        LocationName = row.LocationName,
                        NotificationEmail = null,
                        IsEnabled = true,
                        LastModifiedDate = DateTime.Now,
                        LastModifiedByUserId = 1
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        // ======================================================================
        // Branding Settings
        // ======================================================================

        // Canonical list of branding keys exposed in the UI, in display order.
        private static readonly (string Key, string DisplayName, string? Description, string InputType)[] BrandingKeys =
        {
            ("CompanyName",     "Company Name",        "Your company's display name used in emails and footers.",           "text"),
            ("LogoUrl",         "Company Logo URL",    "Absolute URL to the company logo shown in email headers.",          "url"),
            ("EnvelopeLogoUrl", "Envelope Logo URL",   "Absolute URL for the E-Sign logo shown inside envelope emails. Leave blank to hide.", "url"),
            ("Website",         "Website",             "Company website URL shown in the email footer.",                    "url"),
            ("Phone",           "Phone",               "Company phone number shown in the email footer.",                   "text"),
        };

        public async Task<List<BrandingSettingViewModel>> GetBrandingSettingsAsync()
        {
            var dbRows = await _context.SecuritySettings
                .Where(s => s.Category == "Branding")
                .ToListAsync();

            var result = new List<BrandingSettingViewModel>();
            foreach (var (key, displayName, description, inputType) in BrandingKeys)
            {
                var row = dbRows.FirstOrDefault(r => r.SettingKey == key);
                result.Add(new BrandingSettingViewModel
                {
                    SettingKey   = key,
                    DisplayName  = displayName,
                    Description  = description,
                    SettingValue = row?.SettingValue,
                    InputType    = inputType
                });
            }
            return result;
        }

        public async Task SaveBrandingSettingAsync(SaveBrandingSettingRequest request, int modifiedByUserId)
        {
            var existing = await _context.SecuritySettings
                .FirstOrDefaultAsync(s => s.Category == "Branding" && s.SettingKey == request.SettingKey);

            if (existing != null)
            {
                existing.SettingValue        = request.SettingValue?.Trim() ?? "";
                existing.LastModifiedDate    = DateTime.Now;
                existing.LastModifiedByUserId = modifiedByUserId;
            }
            else
            {
                var def = BrandingKeys.FirstOrDefault(b => b.Key == request.SettingKey);
                _context.SecuritySettings.Add(new SecuritySettingsEntity
                {
                    SettingKey            = request.SettingKey,
                    SettingValue          = request.SettingValue?.Trim() ?? "",
                    Description           = def.Description,
                    Category              = "Branding",
                    LastModifiedDate      = DateTime.Now,
                    LastModifiedByUserId  = modifiedByUserId
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task InitializeDefaultBrandingSettingsAsync()
        {
            foreach (var (key, _, description, _) in BrandingKeys)
            {
                bool exists = await _context.SecuritySettings
                    .AnyAsync(s => s.Category == "Branding" && s.SettingKey == key);
                if (!exists)
                {
                    _context.SecuritySettings.Add(new SecuritySettingsEntity
                    {
                        SettingKey           = key,
                        SettingValue         = "",
                        Description          = description,
                        Category             = "Branding",
                        LastModifiedDate     = DateTime.Now,
                        LastModifiedByUserId = 1
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        // ======================================================================
        // Private helper methods
        // ======================================================================

        private NotificationSettingsViewModel MapToNotificationSettingsViewModel(NotificationSettingsEntity entity)
        {
            return new NotificationSettingsViewModel
            {
                NotificationSettingsId = entity.NotificationSettingsId,
                CategoryName = entity.CategoryName,
                CategoryDisplayName = GetCategoryDisplayName(entity.CategoryName),
                CategoryDescription = GetCategoryDescription(entity.CategoryName),
                IsEnabled = entity.IsEnabled,
                NotifyAssignee = entity.NotifyAssignee,
                NotifyManager = entity.NotifyManager,
                NotifyTaskOwner = entity.NotifyTaskOwner,
                EnableInAppNotification = entity.EnableInAppNotification,
                EnableEmailNotification = entity.EnableEmailNotification,
                ReminderHoursBefore = entity.ReminderHoursBefore,
                SpecificStatusIds = !string.IsNullOrEmpty(entity.SpecificStatuses)
                    ? entity.SpecificStatuses.Split(',').Select(int.Parse).ToList()
                    : null
            };
        }

        private SecuritySettingViewModel MapToSecuritySettingViewModel(SecuritySettingsEntity entity)
        {
            var viewModel = new SecuritySettingViewModel
            {
                SecuritySettingsId = entity.SecuritySettingsId,
                SettingKey = entity.SettingKey,
                SettingValue = entity.SettingValue,
                Description = entity.Description,
                Category = entity.Category
            };

            // Determine input type based on setting key
            if (entity.SettingKey.StartsWith("Require") || entity.SettingValue.ToLower() == "true" || entity.SettingValue.ToLower() == "false")
            {
                viewModel.InputType = "boolean";
            }
            else if (int.TryParse(entity.SettingValue, out _))
            {
                viewModel.InputType = "number";
            }
            else
            {
                viewModel.InputType = "text";
            }

            return viewModel;
        }

        // ======================================================================
        // General Settings
        // ======================================================================

        // Key → (DisplayName, Description, DefaultValue, InputType, Options[])
        private static readonly (string Key, string Display, string Desc, string Default, string InputType, string[] Options)[] GeneralKeys =
        {
            ("AppTheme",          "App Theme",            "Visual theme applied across the application.",                             "light",             "select",  new[] { "light", "dark", "system" }),
            ("DefaultTimezone",   "Default Timezone",     "Timezone used when displaying dates and times across the app.",           "America/New_York",  "select",  new[] {
                "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles",
                "America/Phoenix",  "America/Anchorage", "Pacific/Honolulu", "UTC"
            }),
            ("DateFormat",        "Date Format",          "How dates are displayed throughout the app.",                             "MM/dd/yyyy",        "select",  new[] { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "MMMM d, yyyy" }),
            ("TimeFormat",        "Time Format",          "12-hour (AM/PM) or 24-hour clock display.",                              "12h",               "select",  new[] { "12h", "24h" }),
            ("DefaultLandingPage","Default Landing Page", "Page users land on after signing in (if they have access).",             "Home",              "select",  new[] { "Home", "GMDash", "Dashboard", "Tasks" }),
            ("ItemsPerPage",      "Items Per Page",       "Default number of rows shown in paginated lists.",                       "25",                "select",  new[] { "10", "25", "50", "100" }),
            ("MaintenanceMode",   "Maintenance Mode",     "When enabled, only Admins can log in. All other users see a hold page.", "false",             "boolean", Array.Empty<string>()),
        };

        public async Task<List<SecuritySettingViewModel>> GetGeneralSettingsAsync()
        {
            var dbRows = await _context.SecuritySettings
                .Where(s => s.Category == "General")
                .ToListAsync();

            var result = new List<SecuritySettingViewModel>();
            foreach (var (key, display, desc, defaultVal, inputType, options) in GeneralKeys)
            {
                var row = dbRows.FirstOrDefault(r => r.SettingKey == key);
                result.Add(new SecuritySettingViewModel
                {
                    SecuritySettingsId = row?.SecuritySettingsId ?? 0,
                    SettingKey         = key,
                    SettingValue       = row?.SettingValue ?? defaultVal,
                    Description        = desc,
                    Category           = "General",
                    InputType          = inputType,
                    AvailableOptions   = options.Length > 0 ? options.ToList() : null,
                });
            }
            return result;
        }

        public async Task InitializeDefaultGeneralSettingsAsync()
        {
            foreach (var (key, _, desc, defaultVal, _, _) in GeneralKeys)
            {
                bool exists = await _context.SecuritySettings
                    .AnyAsync(s => s.Category == "General" && s.SettingKey == key);
                if (!exists)
                {
                    _context.SecuritySettings.Add(new SecuritySettingsEntity
                    {
                        SettingKey           = key,
                        SettingValue         = defaultVal,
                        Description          = desc,
                        Category             = "General",
                        LastModifiedDate     = DateTime.Now,
                        LastModifiedByUserId = 1
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        private string GetCategoryDisplayName(string categoryName)
        {
            return categoryName switch
            {
                "TaskAssigned" => "Task Assigned",
                "StatusChanged" => "Status Changed",
                "NoteAdded" => "Note Added",
                "DueDateReminder" => "Due Date Reminder",
                "DocumentSigning" => "Document Signing",
                _ => categoryName
            };
        }

        private string GetCategoryDescription(string categoryName)
        {
            return categoryName switch
            {
                "TaskAssigned" => "Notify users when a task is assigned to them",
                "StatusChanged" => "Notify users when a task status changes",
                "NoteAdded" => "Notify users when a note is added to a task",
                "DueDateReminder" => "Remind users about upcoming task due dates",
                "DocumentSigning" => "Notify users about document signing requests",
                _ => "No description available"
            };
        }
    }
}
