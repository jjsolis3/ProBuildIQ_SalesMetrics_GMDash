using Google.Apis.Calendar.v3.Data;

namespace SalesMetrics.Services
{
    public interface IGoogleCalendarService
    {
        Task<string?> AddTaskEventAsync(int users_Id, string accessToken, string refreshToken, string taskId, string title, string description, DateTime dueDate);
        Task<string?> CreateEventAsync(string accessToken, string refreshToken, string usersId, string title, string description, DateTime? startDate, DateTime? endDate);
        Task<bool> UpdateTaskEventAsync(int userId, string accessToken, string refreshToken, string eventId, string title, string description, DateTime dueDate);
        Task<bool> DeleteTaskEventAsync(int userId, string accessToken, string refreshToken, string eventId);
    }
}
