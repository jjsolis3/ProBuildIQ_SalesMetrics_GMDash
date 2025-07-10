using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using System.Threading.Tasks;
using Google.Apis.Tasks.v1;
using Google.Apis.Tasks.v1.Data;
using Microsoft.Data.SqlClient;

using GoogleTaskModel = Google.Apis.Tasks.v1.Data.Task;
using GoogleTaskListModel = Google.Apis.Tasks.v1.Data.TaskList;


namespace SalesMetrics.Services
{
    public class GoogleTasksService
    {
        private readonly IConfiguration _configuration;

        public GoogleTasksService(IConfiguration config)
        {
            _configuration = config;
        }

        private async Task<TasksService> GetServiceAsync(string accessToken, string refreshToken, string users_Id)
        {
            var token = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var initializer = new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _configuration["Authentication:Google:ClientId"],
                    ClientSecret = _configuration["Authentication:Google:ClientSecret"]
                }
            };

            var flow = new GoogleAuthorizationCodeFlow(initializer);
            var credential = new UserCredential(flow, users_Id, token);

            // Attempt to refresh token if expired
            if (await credential.RefreshTokenAsync(CancellationToken.None))
            {
                await SaveNewAccessTokenAsync(users_Id, credential.Token.AccessToken);
            }

            return new TasksService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "SalesMetrics"
            });
        }

        private async System.Threading.Tasks.Task SaveNewAccessTokenAsync(string users_Id, string newAccessToken)
        {
            var connStr = _configuration.GetConnectionString("SalesMetrics");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                UPDATE Users 
                SET GoogleAccessToken = @AccessToken, 
                    GoogleTokenLastUpdated = GETDATE()
                WHERE Users_ID = @Users_Id", conn);

            cmd.Parameters.AddWithValue("@AccessToken", newAccessToken);
            cmd.Parameters.AddWithValue("@Users_Id", users_Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<string?> CreateTaskAsync(string accessToken, string refreshToken, string users_Id, string taskId, string title, string notes, DateTime? dueDate = null)
        {
            var service = await GetServiceAsync(accessToken, refreshToken, users_Id);

            // Step 1: Get task lists
            var taskLists = await service.Tasklists.List().ExecuteAsync();
            var defaultList = taskLists.Items?.FirstOrDefault();

            if (defaultList == null)
            {
                Console.WriteLine("[Google Tasks] No task list found for user.");
                return null;
            }

            // Step 2: Build the task
            var task = new GoogleTaskModel
            {
                Title = title,
                Notes = $"[Task ID: {taskId}]\n{notes}",
                Due = dueDate?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            // Step 3: Insert task
            try
            {
                var result = await service.Tasks.Insert(task, defaultList.Id).ExecuteAsync();
                return result.Id;
            }
            catch (Google.GoogleApiException ex)
            {
                Console.WriteLine("[Google Tasks API ERROR]");
                Console.WriteLine(ex.ToString());

                if (ex.Error?.Errors != null)
                {
                    foreach (var e in ex.Error.Errors)
                        Console.WriteLine($"[Google Error] Reason: {e.Reason}, Message: {e.Message}");
                }

                throw;
            }
        }
        public async Task<List<GoogleTaskListModel>> GetTaskListsAsync(string accessToken, string refreshToken, string users_Id)
        {
            var service = await GetServiceAsync(accessToken, refreshToken, users_Id);
            var result = await service.Tasklists.List().ExecuteAsync();
            return result.Items?.ToList() ?? new List<TaskList>();
        }
    }

}