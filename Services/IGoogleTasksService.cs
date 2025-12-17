using GoogleTaskModel = Google.Apis.Tasks.v1.Data.Task;
using GoogleTaskListModel = Google.Apis.Tasks.v1.Data.TaskList;

namespace SalesMetrics.Services
{
    public interface IGoogleTasksService
    {
        Task<string?> CreateTaskAsync(string accessToken, string refreshToken, string usersId, int taskId, string title, string notes, DateTime? dueDate = null);
        Task<string?> CreateTaskAsync(string accessToken, string refreshToken, string usersId, string taskId, string title, string notes, DateTime? dueDate = null);
        Task<List<GoogleTaskListModel>> GetTaskListsAsync(string accessToken, string refreshToken, string users_Id);
    }
}
