using SalesMetrics.Models;

namespace SalesMetrics.Services.Announcements
{
    public interface IAnnouncementService
    {
        // Dashboard
        Task<AnnouncementDashboardViewModel> GetDashboardDataAsync();

        // Get announcements
        Task<List<AnnouncementViewModel>> GetAllAnnouncementsAsync();
        Task<List<AnnouncementViewModel>> GetActiveAnnouncementsAsync();
        Task<List<AnnouncementViewModel>> GetScheduledAnnouncementsAsync();
        Task<List<AnnouncementViewModel>> GetExpiredAnnouncementsAsync();
        Task<List<AnnouncementViewModel>> GetTemplatesAsync();
        Task<AnnouncementViewModel?> GetAnnouncementByIdAsync(int id);

        // Create/Update/Delete
        Task<int> CreateAnnouncementAsync(AnnouncementCreateEditViewModel model, int createdByUserId);
        Task UpdateAnnouncementAsync(int id, AnnouncementCreateEditViewModel model, int modifiedByUserId);
        Task DeleteAnnouncementAsync(int id);

        // Send/Schedule
        Task SendAnnouncementAsync(int id, int sentByUserId);
        Task<bool> ProcessScheduledAnnouncementsAsync(); // Background job to send scheduled announcements

        // Templates
        Task<int> CreateTemplateAsync(AnnouncementCreateEditViewModel model, int createdByUserId);
        Task<AnnouncementViewModel?> GetTemplateByIdAsync(int id);

        // Analytics
        Task UpdateReadCountAsync(int broadcastMessageId);
    }
}
