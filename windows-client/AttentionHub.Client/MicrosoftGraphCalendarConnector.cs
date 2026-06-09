using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AttentionHub.Client;

public sealed class MicrosoftGraphCalendarConnector
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public async Task<CalendarConnectorResult> ReadUpcomingBusyEventsAsync(
        ClientSettings settings,
        Func<string, Task> showDeviceCodeAsync,
        int daysAhead = 7)
    {
        if (string.IsNullOrWhiteSpace(settings.MicrosoftClientId))
        {
            return CalendarConnectorResult.Failed("Microsoft Client ID nao configurado. Configure na tela Status.");
        }

        var token = await GetAccessTokenAsync(settings, showDeviceCodeAsync);
        if (string.IsNullOrWhiteSpace(token))
        {
            return CalendarConnectorResult.Failed("Nao foi possivel obter token Microsoft Graph.");
        }

        var start = DateTimeOffset.Now;
        var end = start.AddDays(daysAhead);
        var url =
            "https://graph.microsoft.com/v1.0/me/calendarView" +
            $"?startDateTime={Uri.EscapeDataString(start.ToString("o"))}" +
            $"&endDateTime={Uri.EscapeDataString(end.ToString("o"))}" +
            "&$select=id,start,end,showAs,isCancelled,isOrganizer,type" +
            "&$orderby=start/dateTime";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Prefer", $"outlook.timezone=\"{TimeZoneInfo.Local.Id}\"");

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return CalendarConnectorResult.Failed($"Microsoft Graph Calendar falhou: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var payload = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GraphCalendarViewResponse>(payload, JsonOptions.Wire);
        var events = result?.Value?
            .Where(item => !item.IsCancelled)
            .Select(MapGraphEvent)
            .Where(item => item is not null)
            .Cast<MockAttentionEvent>()
            .OrderBy(item => item.OccurredAt)
            .ToList() ?? [];

        return new CalendarConnectorResult(
            Success: true,
            Events: events,
            Message: events.Count == 0
                ? "Microsoft Graph lido com sucesso, sem eventos nos proximos dias."
                : $"Microsoft Graph lido com sucesso: {events.Count} eventos de calendario.");
    }

    private async Task<string?> GetAccessTokenAsync(ClientSettings settings, Func<string, Task> showDeviceCodeAsync)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _accessToken;
        }

        var tenant = string.IsNullOrWhiteSpace(settings.MicrosoftTenantId) ? "common" : settings.MicrosoftTenantId.Trim();
        var deviceCodeUrl = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/devicecode";
        var tokenUrl = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";
        var scope = "https://graph.microsoft.com/Calendars.ReadBasic offline_access User.Read";

        using var deviceCodeResponse = await _httpClient.PostAsync(
            deviceCodeUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.MicrosoftClientId.Trim(),
                ["scope"] = scope
            }));

        if (!deviceCodeResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var deviceCodePayload = await deviceCodeResponse.Content.ReadAsStringAsync();
        var deviceCode = JsonSerializer.Deserialize<DeviceCodeResponse>(deviceCodePayload, JsonOptions.Wire);
        if (deviceCode is null)
        {
            return null;
        }

        await showDeviceCodeAsync(deviceCode.Message);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(deviceCode.ExpiresIn);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(deviceCode.Interval, 5)));

            using var tokenResponse = await _httpClient.PostAsync(
                tokenUrl,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = settings.MicrosoftClientId.Trim(),
                    ["device_code"] = deviceCode.DeviceCode
                }));

            var tokenPayload = await tokenResponse.Content.ReadAsStringAsync();
            if (tokenResponse.IsSuccessStatusCode)
            {
                var token = JsonSerializer.Deserialize<TokenResponse>(tokenPayload, JsonOptions.Wire);
                _accessToken = token?.AccessToken;
                _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token?.ExpiresIn ?? 0);
                return _accessToken;
            }

            var error = JsonSerializer.Deserialize<TokenErrorResponse>(tokenPayload, JsonOptions.Wire);
            if (error?.Error is "authorization_pending")
            {
                continue;
            }

            if (error?.Error is "slow_down")
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }

            return null;
        }

        return null;
    }

    private static MockAttentionEvent? MapGraphEvent(GraphCalendarEvent item)
    {
        if (!DateTimeOffset.TryParse(item.Start?.DateTime, out var start))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(item.End?.DateTime, out var end) || end <= start)
        {
            return null;
        }

        var availability = item.ShowAs switch
        {
            "free" => "free",
            "tentative" => "tentative",
            "oof" => "out_of_office",
            _ => "busy"
        };

        var sourceId = "microsoft-graph-calendar";
        var dedupe = $"{sourceId}:{item.Id}:{start:o}:{end:o}";

        return new MockAttentionEvent(
            eventId: $"evt_graph_calendar_{Math.Abs(dedupe.GetHashCode())}",
            eventType: "calendar.busy",
            sourceId: sourceId,
            sourceDisplayName: "Office 365 Calendar",
            sourceApp: "microsoft_graph",
            occurredAt: start,
            priority: "normal",
            summary: $"[Office 365] Busy {start:HH:mm}-{end:HH:mm}",
            dedupeKey: dedupe,
            localStatus: "Novo",
            calendar: new CalendarPayload(
                StartsAt: start,
                EndsAt: end,
                Availability: availability,
                MaskedTitle: "[Office 365] Busy",
                IsRecurring: item.Type == "seriesMaster" || item.Type == "occurrence"));
    }

    private sealed record DeviceCodeResponse(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_uri")] string VerificationUri,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int Interval,
        [property: JsonPropertyName("message")] string Message);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record TokenErrorResponse(
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);

    private sealed record GraphCalendarViewResponse(
        [property: JsonPropertyName("value")] IReadOnlyList<GraphCalendarEvent> Value);

    private sealed record GraphCalendarEvent(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("start")] GraphDateTime? Start,
        [property: JsonPropertyName("end")] GraphDateTime? End,
        [property: JsonPropertyName("showAs")] string? ShowAs,
        [property: JsonPropertyName("isCancelled")] bool IsCancelled,
        [property: JsonPropertyName("type")] string? Type);

    private sealed record GraphDateTime(
        [property: JsonPropertyName("dateTime")] string? DateTime,
        [property: JsonPropertyName("timeZone")] string? TimeZone);
}
