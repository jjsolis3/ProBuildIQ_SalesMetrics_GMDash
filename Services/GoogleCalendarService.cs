using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Net;

public class GoogleCalendarService
{
    private readonly IConfiguration _configuration;
    private readonly string _applicationName = "SalesMetrics";

    public GoogleCalendarService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private async Task<string?> EnsureValidAccessTokenAsync(int users_Id, string refreshToken)
    {
        var tokenHelper = new GoogleTokenHelper(_configuration);
        var newToken = await tokenHelper.RefreshAccessTokenAsync(refreshToken);

        if (!string.IsNullOrEmpty(newToken))
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("SalesMetrics"));
            await conn.OpenAsync();

            var cmd = new SqlCommand("UPDATE Users SET GoogleAccessToken = @AccessToken WHERE Users_ID = @Users_ID", conn);
            cmd.Parameters.AddWithValue("@AccessToken", newToken);
            cmd.Parameters.AddWithValue("@Users_ID", users_Id);
            await cmd.ExecuteNonQueryAsync();

            return newToken;
        }

        return null;
    }

    private CalendarService GetService(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _applicationName
        });
    }

    public async Task<string?> AddTaskEventAsync(int users_Id, string accessToken, string refreshToken, string taskId, string title, string description, DateTime dueDate)
    {
        try
        {
            var calendarService = GetService(accessToken);

            var newEvent = new Event
            {
                Summary = title,
                Description = $"[Task ID: {taskId}]\n\n{description}",
                Start = new EventDateTime
                {
                    DateTime = dueDate,
                    TimeZone = "America/Los_Angeles"
                },
                End = new EventDateTime
                {
                    DateTime = dueDate.AddHours(1),
                    TimeZone = "America/Los_Angeles"
                }
            };

            var createdEvent = await calendarService.Events.Insert(newEvent, "primary").ExecuteAsync();
            return createdEvent.Id;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshedToken = await EnsureValidAccessTokenAsync(users_Id, refreshToken);
            if (!string.IsNullOrEmpty(refreshedToken))
            {
                var newService = GetService(refreshedToken);

                var newEvent = new Event
                {
                    Summary = title,
                    Description = $"[Task ID: {taskId}]\n\n{description}",
                    Start = new EventDateTime
                    {
                        DateTime = dueDate,
                        TimeZone = "America/Los_Angeles"
                    },
                    End = new EventDateTime
                    {
                        DateTime = dueDate.AddHours(1),
                        TimeZone = "America/Los_Angeles"
                    }
                };

                var createdEvent = await newService.Events.Insert(newEvent, "primary").ExecuteAsync();
                return createdEvent.Id;
            }

            Console.WriteLine("[Google Calendar] Failed to refresh token.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Google Calendar Error] {ex.Message}");
            return null;
        }
    }


    public async Task<bool> UpdateTaskEventAsync(int userId, string accessToken, string refreshToken, string eventId, string title, string description, DateTime dueDate)
    {
        var calendarService = GetService(accessToken);

        try
        {
            var existingEvent = await calendarService.Events.Get("primary", eventId).ExecuteAsync();
            existingEvent.Summary = title;
            existingEvent.Description = description;
            existingEvent.Start = new EventDateTime
            {
                DateTimeRaw = dueDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = "America/Los_Angeles"
            };
            existingEvent.End = new EventDateTime
            {
                DateTimeRaw = dueDate.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = "America/Los_Angeles"
            };

            await calendarService.Events.Update(existingEvent, "primary", eventId).ExecuteAsync();
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await EnsureValidAccessTokenAsync(userId, refreshToken);
            if (!string.IsNullOrEmpty(refreshed))
            {
                var newService = GetService(refreshed);
                var existingEvent = await newService.Events.Get("primary", eventId).ExecuteAsync();
                existingEvent.Summary = title;
                existingEvent.Description = description;
                existingEvent.Start = new EventDateTime
                {
                    DateTimeRaw = dueDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "America/Los_Angeles"
                };
                existingEvent.End = new EventDateTime
                {
                    DateTimeRaw = dueDate.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "America/Los_Angeles"
                };

                await newService.Events.Update(existingEvent, "primary", eventId).ExecuteAsync();
                return true;
            }

            Console.WriteLine("[Google Calendar] Failed to refresh token on update.");
            return false;
        }
    }

    public async Task<bool> DeleteTaskEventAsync(int userId, string accessToken, string refreshToken, string eventId)
    {
        try
        {
            var service = GetService(accessToken);
            await service.Events.Delete("primary", eventId).ExecuteAsync();
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await EnsureValidAccessTokenAsync(userId, refreshToken);
            if (!string.IsNullOrEmpty(refreshed))
            {
                var newService = GetService(refreshed);
                await newService.Events.Delete("primary", eventId).ExecuteAsync();
                return true;
            }

            Console.WriteLine("[Google Calendar] Failed to refresh token on delete.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Google Calendar Delete Error] {ex.Message}");
            return false;
        }
    }

}
