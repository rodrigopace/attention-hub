using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AttentionHub.Client;

public sealed class SyncClient
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<SyncResponse> PostSyncAsync(string backendBaseUrl, SyncRequest request)
    {
        if (string.IsNullOrWhiteSpace(backendBaseUrl))
        {
            throw new ArgumentException("Backend URL is required.", nameof(backendBaseUrl));
        }

        var endpoint = new Uri(new Uri(EnsureTrailingSlash(backendBaseUrl)), "sync");
        var json = JsonSerializer.Serialize(request, JsonOptions.Wire);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(endpoint, body);

        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<SyncResponse>(responseText, JsonOptions.Wire)
               ?? throw new InvalidOperationException("Backend returned an empty or invalid response.");
    }

    public async Task<CentralCalendarEventsResponse> GetCalendarEventsAsync(string backendBaseUrl, int daysAhead = 14)
    {
        if (string.IsNullOrWhiteSpace(backendBaseUrl))
        {
            throw new ArgumentException("Backend URL is required.", nameof(backendBaseUrl));
        }

        var endpoint = new Uri(new Uri(EnsureTrailingSlash(backendBaseUrl)), $"calendar/events?days_ahead={daysAhead}");
        using var response = await _httpClient.GetAsync(endpoint);
        var responseText = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<CentralCalendarEventsResponse>(responseText, JsonOptions.Wire)
               ?? throw new InvalidOperationException("Backend returned an empty or invalid calendar response.");
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }
}
