using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class GoogleTokenHelper
{
    private readonly IConfiguration _configuration;

    public GoogleTokenHelper(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string?> RefreshAccessTokenAsync(string refreshToken)
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        var clientSecret = _configuration["Authentication:Google:ClientSecret"];

        using var httpClient = new HttpClient();

        var parameters = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(parameters)
        };

        var response = await httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[GoogleTokenHelper] Failed to refresh token: {responseBody}");
            return null;
        }

        var json = JsonDocument.Parse(responseBody);
        return json.RootElement.GetProperty("access_token").GetString();
    }
}
