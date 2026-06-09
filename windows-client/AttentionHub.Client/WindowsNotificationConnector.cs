using System.Security.Cryptography;
using System.Text;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace AttentionHub.Client;

public sealed record NotificationConnectorResult(
    bool Success,
    IReadOnlyList<MockAttentionEvent> Events,
    string Message,
    string Diagnostics = "")
{
    public static NotificationConnectorResult Failed(string message)
    {
        return new NotificationConnectorResult(false, [], message);
    }
}

public sealed class WindowsNotificationConnector
{
    private static readonly string[] IgnoredAppNames =
    [
        "codex",
        "attention hub",
        "attention-hub",
        "attentionhub"
    ];

    public async Task<NotificationConnectorResult> ReadCurrentNotificationsAsync()
    {
        try
        {
            var listener = UserNotificationListener.Current;
            var accessStatus = await listener.RequestAccessAsync();

            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                return new NotificationConnectorResult(
                    Success: false,
                    Events: [],
                    Message: $"Acesso a notificacoes Windows nao permitido: {accessStatus}.",
                    Diagnostics: $"access_status={accessStatus}");
            }

            var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            var mappedNotifications = new List<MappedNotification>();
            var diagnostics = new List<string>
            {
                $"access_status={accessStatus}",
                $"raw_notifications={notifications.Count}"
            };

            foreach (var notification in notifications)
            {
                var mapped = TryMapNotification(notification, out var detail);
                diagnostics.Add(detail);
                if (mapped is not null)
                {
                    mappedNotifications.Add(mapped);
                }
            }

            var groupedEvents = mappedNotifications
                .GroupBy(item => item.GroupKey, StringComparer.OrdinalIgnoreCase)
                .Select(ToGroupedEvent)
                .OrderByDescending(item => item.OccurredAt)
                .ToList();
            diagnostics.Add($"grouped_conversations={groupedEvents.Count}");

            return new NotificationConnectorResult(
                Success: true,
                Events: groupedEvents,
                Message: groupedEvents.Count == 0
                    ? "Notificacoes Windows lidas com sucesso, sem notificacoes ativas."
                    : $"Notificacoes Windows lidas com sucesso: {groupedEvents.Count} conversas agrupadas.",
                Diagnostics: string.Join(Environment.NewLine, diagnostics));
        }
        catch (Exception ex)
        {
            return NotificationConnectorResult.Failed($"Falha ao ler notificacoes Windows: {ex.Message}");
        }
    }

    private static MappedNotification? TryMapNotification(UserNotification notification, out string diagnostics)
    {
        try
        {
            var appName = notification.AppInfo.DisplayInfo.DisplayName;
            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = notification.AppInfo.AppUserModelId;
            }

            if (IsIgnoredApp(appName))
            {
                diagnostics = $"ignored id={notification.Id}; app={appName}; reason=ignored_app";
                return null;
            }

            var text = ExtractText(notification.Notification);
            var summary = string.IsNullOrWhiteSpace(text)
                ? $"Notificacao de {appName}"
                : text;
            var conversation = ExtractConversation(appName, summary);
            diagnostics = $"mapped id={notification.Id}; app={appName}; conversation={conversation}; created={notification.CreationTime:o}; text_length={text.Length}";

            return new MappedNotification(
                NotificationId: notification.Id,
                AppName: appName,
                NormalizedAppName: NormalizeAppName(appName),
                Conversation: conversation,
                EventType: GuessEventType(appName, summary),
                Priority: GuessPriority(summary),
                Summary: summary,
                OccurredAt: notification.CreationTime);
        }
        catch (Exception ex)
        {
            diagnostics = $"discarded id={notification.Id}; reason={ex.Message}";
            return null;
        }
    }

    private static MockAttentionEvent ToGroupedEvent(IGrouping<string, MappedNotification> group)
    {
        var ordered = group.OrderByDescending(item => item.OccurredAt).ToList();
        var latest = ordered[0];
        var count = ordered.Count;
        var hash = Hash(group.Key);
        var eventType = ordered.Any(item => item.EventType == "message.mention")
            ? "message.mention"
            : latest.EventType;
        var priority = ordered.Any(item => item.Priority == "urgent")
            ? "urgent"
            : latest.Priority;
        var summary = count == 1
            ? latest.Summary
            : $"{latest.Conversation} | {count} notificacoes agrupadas | ultima: {latest.Summary}";

        return new MockAttentionEvent(
            eventId: $"evt_win_notification_group_{hash[..16]}",
            eventType: eventType,
            sourceId: $"windows-notification-{latest.NormalizedAppName}",
            sourceDisplayName: latest.AppName,
            sourceApp: "windows",
            occurredAt: latest.OccurredAt,
            priority: priority,
            summary: summary,
            dedupeKey: $"windows:notification-group:{hash}",
            localStatus: "Novo",
            message: new MessagePayload(
                ConversationType: eventType == "message.direct" ? "direct" : "unknown",
                SenderHash: $"sha256:{hash[..16]}"));
    }

    private static string ExtractText(Notification notification)
    {
        var binding = notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
        if (binding is null)
        {
            return "";
        }

        var textElements = binding.GetTextElements();
        return string.Join(" | ", textElements.Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string ExtractConversation(string appName, string summary)
    {
        var parts = summary.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : appName;
    }

    private static string GuessEventType(string appName, string summary)
    {
        var combined = $"{appName} {summary}".ToLowerInvariant();
        if (combined.Contains("mentioned") || combined.Contains("mencao") || combined.Contains("men\u00e7\u00e3o") || combined.Contains("@"))
        {
            return "message.mention";
        }

        return "message.direct";
    }

    private static string GuessPriority(string summary)
    {
        var lower = summary.ToLowerInvariant();
        if (lower.Contains("urgent") || lower.Contains("urgente") || lower.Contains("critical") || lower.Contains("critico"))
        {
            return "urgent";
        }

        return "normal";
    }

    private static bool IsIgnoredApp(string appName)
    {
        var normalized = appName.ToLowerInvariant();
        return IgnoredAppNames.Any(ignored => normalized.Contains(ignored, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAppName(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        return new string(chars).Trim('-');
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record MappedNotification(
        uint NotificationId,
        string AppName,
        string NormalizedAppName,
        string Conversation,
        string EventType,
        string Priority,
        string Summary,
        DateTimeOffset OccurredAt)
    {
        public string GroupKey => $"{NormalizedAppName}:{Conversation}";
    }
}
