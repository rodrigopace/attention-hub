using System.Security.Cryptography;
using System.Text;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace AttentionHub.Client;

public sealed record NotificationConnectorResult(
    bool Success,
    IReadOnlyList<MockAttentionEvent> Events,
    string Message)
{
    public static NotificationConnectorResult Failed(string message)
    {
        return new NotificationConnectorResult(false, [], message);
    }
}

public sealed class WindowsNotificationConnector
{
    public async Task<NotificationConnectorResult> ReadCurrentNotificationsAsync()
    {
        try
        {
            var listener = UserNotificationListener.Current;
            var accessStatus = await listener.RequestAccessAsync();

            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                return NotificationConnectorResult.Failed($"Acesso a notificacoes Windows nao permitido: {accessStatus}.");
            }

            var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
            var events = notifications
                .Select(TryMapNotification)
                .Where(item => item is not null)
                .Cast<MockAttentionEvent>()
                .OrderByDescending(item => item.OccurredAt)
                .ToList();

            return new NotificationConnectorResult(
                Success: true,
                Events: events,
                Message: events.Count == 0
                    ? "Notificacoes Windows lidas com sucesso, sem notificacoes ativas."
                    : $"Notificacoes Windows lidas com sucesso: {events.Count} notificacoes ativas.");
        }
        catch (Exception ex)
        {
            return NotificationConnectorResult.Failed($"Falha ao ler notificacoes Windows: {ex.Message}");
        }
    }

    private static MockAttentionEvent? TryMapNotification(UserNotification notification)
    {
        try
        {
            var appName = notification.AppInfo.DisplayInfo.DisplayName;
            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = notification.AppInfo.AppUserModelId;
            }

            var text = ExtractText(notification.Notification);
            var summary = string.IsNullOrWhiteSpace(text)
                ? $"Notificacao de {appName}"
                : text;

            var occurredAt = notification.CreationTime;
            var hash = Hash($"{notification.Id}:{appName}:{occurredAt:o}:{summary}");
            var normalizedApp = NormalizeAppName(appName);
            var eventType = GuessEventType(appName, summary);
            var priority = GuessPriority(summary);

            return new MockAttentionEvent(
                eventId: $"evt_win_notification_{hash[..16]}",
                eventType: eventType,
                sourceId: $"windows-notification-{normalizedApp}",
                sourceDisplayName: appName,
                sourceApp: "windows",
                occurredAt: occurredAt,
                priority: priority,
                summary: summary,
                dedupeKey: $"windows:notification:{hash}",
                localStatus: "Novo",
                message: new MessagePayload(
                    ConversationType: eventType == "message.direct" ? "direct" : "unknown",
                    SenderHash: $"sha256:{hash[..16]}"));
        }
        catch
        {
            return null;
        }
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

    private static string GuessEventType(string appName, string summary)
    {
        var combined = $"{appName} {summary}".ToLowerInvariant();
        if (combined.Contains("mentioned") || combined.Contains("mencao") || combined.Contains("menção") || combined.Contains("@"))
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
}
