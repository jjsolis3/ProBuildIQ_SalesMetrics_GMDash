using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;

namespace SalesMetrics.Services
{
    public class GoogleGmailService
    {
        private readonly IConfiguration _configuration;
        private readonly string _appName = "SalesMetrics";

        public GoogleGmailService(IConfiguration config)
        {
            _configuration = config;
        }

        private GmailService GetService(string accessToken)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);
            return new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _appName
            });
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

        private string CreateRawEmail(string to, string from, string subject, string body)
        {
            var mime = new StringBuilder();
            mime.AppendLine($"To: {to}");
            mime.AppendLine($"From: {from}");
            mime.AppendLine($"Subject: {subject}");
            mime.AppendLine("Content-Type: text/plain; charset=utf-8");
            mime.AppendLine();
            mime.AppendLine(body);

            var bytes = Encoding.UTF8.GetBytes(mime.ToString());
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", ""); // base64url encoding
        }

        public async Task<bool> SendEmailAsync(int users_Id, string accessToken, string refreshToken, string to, string subject, string body, string fromEmail)
        {
            var service = GetService(accessToken);
            var rawMessage = CreateRawEmail(to, fromEmail, subject, body);

            var message = new Message { Raw = rawMessage };

            try
            {
                await service.Users.Messages.Send(message, "me").ExecuteAsync();
                return true;
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
            {
                var refreshed = await EnsureValidAccessTokenAsync(users_Id, refreshToken);
                if (!string.IsNullOrEmpty(refreshed))
                {
                    var newService = GetService(refreshed);
                    await newService.Users.Messages.Send(message, "me").ExecuteAsync();
                    return true;
                }

                Console.WriteLine("[Google Gmail] Failed to refresh token on send.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Google Gmail Error] {ex.Message}");
                return false;
            }
        }
    }
}
