using SalesMetrics.Models;

namespace SalesMetrics.Services.Settings
{
    public interface ISettingsService
    {
        // Dashboard
        Task<SettingsDashboardViewModel> GetDashboardDataAsync();

        // Notification Settings
        Task<List<NotificationSettingsViewModel>> GetNotificationSettingsAsync();
        Task<NotificationSettingsViewModel?> GetNotificationSettingByIdAsync(int id);
        Task<NotificationSettingsViewModel?> GetNotificationSettingByCategoryAsync(string categoryName);
        Task UpdateNotificationSettingAsync(UpdateNotificationSettingRequest request, int modifiedByUserId);
        Task InitializeDefaultNotificationSettingsAsync();

        // Security Settings
        Task<List<SecuritySettingViewModel>> GetSecuritySettingsAsync();
        Task<List<SecuritySettingViewModel>> GetSecuritySettingsByCategoryAsync(string category);
        Task<SecuritySettingViewModel?> GetSecuritySettingByKeyAsync(string settingKey);
        Task UpdateSecuritySettingAsync(UpdateSecuritySettingRequest request, int modifiedByUserId);
        Task<string?> GetSecuritySettingValueAsync(string settingKey);
        Task InitializeDefaultSecuritySettingsAsync();

        // Envelope Notification Settings
        Task<List<EnvelopeNotificationSettingViewModel>> GetEnvelopeNotificationSettingsAsync();
        Task SaveEnvelopeNotificationSettingAsync(SaveEnvelopeNotificationSettingRequest request, int modifiedByUserId);
        Task InitializeDefaultEnvelopeNotificationSettingsAsync();

        // Form Notification Settings
        Task<List<FormNotificationSettingViewModel>> GetFormNotificationSettingsAsync();
        Task<string?> GetFormNotificationEmailAsync(string? locationCode);
        Task SaveFormNotificationSettingAsync(SaveFormNotificationSettingRequest request, int modifiedByUserId);
        Task InitializeDefaultFormNotificationSettingsAsync();

        // Branding Settings
        Task<List<BrandingSettingViewModel>> GetBrandingSettingsAsync();
        Task SaveBrandingSettingAsync(SaveBrandingSettingRequest request, int modifiedByUserId);
        Task InitializeDefaultBrandingSettingsAsync();

        // General Settings
        Task<List<SecuritySettingViewModel>> GetGeneralSettingsAsync();
        Task InitializeDefaultGeneralSettingsAsync();
    }
}
