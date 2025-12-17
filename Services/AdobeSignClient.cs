using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SalesMetrics.Services;

public class AdobeSignClient : IAdobeSignClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public AdobeSignClient(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
        _http.BaseAddress = new Uri(_cfg["AdobeSign:BaseUrl"]!);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _cfg["AdobeSign:AccessToken"]);
    }

    public async Task<string> CreateAgreementFromLibraryAsync(string libraryDocumentId, string managerEmail, string placeholderTenantEmail, string message)
    {
        var payload = new
        {
            name = "Property Agreement",
            state = "IN_PROCESS",
            fileInfos = new[] { new { libraryDocumentId } },
            participantSetsInfo = new[]
            {
                new { memberInfos = new[] { new { email = managerEmail } }, order = 1, role = "SIGNER" },
                new { memberInfos = new[] { new { email = placeholderTenantEmail } }, order = 2, role = "SIGNER" }
            },
            signatureType = "ESIGN",
            message
        };

        var res = await _http.PostAsync("agreements", JsonContent(payload));
        await EnsureSuccess(res);
        //res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task ReplaceRecipientAsync(string agreementId, int recipientOrder, string newEmail)
    {
        var payload = new { email = newEmail };
        var res = await _http.PostAsync($"agreements/{agreementId}/members/participantSets/{recipientOrder}/replace",
                                        JsonContent(payload));
        await EnsureSuccess(res);
        //res.EnsureSuccessStatusCode();
    }

    public async Task<Stream> DownloadCombinedDocAsync(string agreementId)
    {
        var res = await _http.GetAsync($"agreements/{agreementId}/combinedDocument");
        await EnsureSuccess(res);
        //res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStreamAsync();
    }

    private static StringContent JsonContent(object o) =>
        new StringContent(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

    private static async Task EnsureSuccess(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
        {
            var url = res.RequestMessage?.RequestUri?.ToString();
            var body = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"HTTP {(int)res.StatusCode} {res.StatusCode} for {url}\n{body}");
        }
    }

    public async Task<string> GetAgreementStatusAsync(string agreementId)
    {
        var res = await _http.GetAsync($"agreements/{agreementId}");
        await EnsureSuccess(res);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("status").GetString()!;
    }

    public async Task<JsonElement> GetAgreementEventsAsync(string agreementId)
    {
        var res = await _http.GetAsync($"agreements/{agreementId}/events");
        await EnsureSuccess(res);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    public async Task<Dictionary<string, string>> GetAgreementFormDataAsync(string agreementId)
    {
        // Acrobat Sign returns CSV for formData; v6 supports this endpoint.
        var res = await _http.GetAsync($"agreements/{agreementId}/formData");
        await EnsureSuccess(res);
        var csv = await res.Content.ReadAsStringAsync();

        // naive CSV parse: header row + one data row
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2) return new();

        var headers = lines[0].Split(',', StringSplitOptions.TrimEntries);
        var values = lines[1].Split(',', StringSplitOptions.TrimEntries);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
            dict[headers[i]] = values[i].Trim('"');

        return dict;
    }

}
