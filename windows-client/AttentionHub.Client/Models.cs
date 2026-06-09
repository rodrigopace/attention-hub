using System.Text.Json;
using System.Text.Json.Serialization;

namespace AttentionHub.Client;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Pretty = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}

public sealed record MockAttentionEvent(
    string EventId,
    string EventType,
    string SourceId,
    string SourceDisplayName,
    string SourceApp,
    DateTimeOffset OccurredAt,
    string Priority,
    string Summary,
    string DedupeKey,
    string LocalStatus = "Novo",
    CalendarPayload? Calendar = null,
    EmailPayload? Email = null,
    MessagePayload? Message = null)
{
    public string DisplayTime => OccurredAt.ToString("HH:mm");
}

public sealed record AgendaItem(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string SourceDisplayName,
    string MaskedTitle,
    string Availability)
{
    public string StartsAtDisplay => StartsAt.ToString("dd/MM HH:mm");
    public string EndsAtDisplay => EndsAt.ToString("dd/MM HH:mm");
}

public sealed record RuleSetting(
    string Name,
    string Status,
    string Description);

public sealed record SourceStatus(
    string SourceDisplayName,
    string App,
    string Status,
    DateTimeOffset LastEventAt,
    string Notes)
{
    public string LastEventDisplay => LastEventAt.ToString("dd/MM HH:mm");
}

public sealed record SyncRequest(
    string RequestId,
    Device Device,
    string? SyncCursor,
    DateTimeOffset SentAt,
    IReadOnlyList<string> ClientCapabilities,
    IReadOnlyList<AttentionEvent> Events);

public sealed record Device(
    string DeviceId,
    string DisplayName,
    string Platform,
    string AgentVersion,
    string Timezone);

public sealed record AttentionEvent(
    string EventId,
    string EventType,
    Source Source,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ReceivedAt,
    string PrivacyLevel,
    string Priority,
    string DedupeKey,
    CalendarPayload? Calendar,
    EmailPayload? Email,
    MessagePayload? Message);

public sealed record Source(
    string SourceId,
    string DisplayName,
    string EnvironmentType,
    string App);

public sealed record CalendarPayload(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Availability,
    string MaskedTitle,
    bool IsRecurring);

public sealed record EmailPayload(
    string Direction,
    string? SenderHash,
    string RecipientMatch,
    bool? HasAttachments);

public sealed record MessagePayload(
    string ConversationType,
    string? SenderHash = null,
    string? ChannelHash = null);

public sealed record SyncResponse(
    string RequestId,
    IReadOnlyList<string> AcceptedEventIds,
    IReadOnlyList<string> DuplicateEventIds,
    IReadOnlyList<RejectedEvent> RejectedEvents,
    string NextSyncCursor,
    DateTimeOffset ServerTime,
    IReadOnlyList<Rule> EffectiveRules,
    IReadOnlyList<BackendAction> Actions,
    SyncStatus Status);

public sealed record RejectedEvent(string EventId, string Reason);

public sealed record Rule(
    string RuleId,
    bool Enabled,
    string RuleType,
    Dictionary<string, JsonElement>? Config);

public sealed record BackendAction(
    string ActionId,
    string ActionType,
    string? Message);

public sealed record SyncStatus(
    string CalendarSync,
    int RecommendedPollIntervalSeconds);

public static class SyncRequestFactory
{
    public static SyncRequest Create(IEnumerable<MockAttentionEvent> mockEvents, ClientSettings settings)
    {
        return new SyncRequest(
            RequestId: $"req_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            Device: new Device(
                DeviceId: settings.DeviceId,
                DisplayName: settings.DeviceDisplayName,
                Platform: "windows",
                AgentVersion: "0.1.0",
                Timezone: TimeZoneInfo.Local.Id),
            SyncCursor: null,
            SentAt: DateTimeOffset.Now,
            ClientCapabilities:
            [
                "windows_notifications",
                "outlook_calendar_read",
                "outlook_email_metadata",
                "local_audit_preview",
                "system_proxy"
            ],
            Events: mockEvents.Select(ToAttentionEvent).ToList());
    }

    private static AttentionEvent ToAttentionEvent(MockAttentionEvent item)
    {
        return new AttentionEvent(
            EventId: item.EventId,
            EventType: item.EventType,
            Source: new Source(
                SourceId: item.SourceId,
                DisplayName: item.SourceDisplayName,
                EnvironmentType: "corporate",
                App: item.SourceApp),
            OccurredAt: item.OccurredAt,
            ReceivedAt: DateTimeOffset.Now,
            PrivacyLevel: "metadata_only",
            Priority: item.Priority,
            DedupeKey: item.DedupeKey,
            Calendar: item.Calendar,
            Email: item.Email,
            Message: item.Message);
    }
}
