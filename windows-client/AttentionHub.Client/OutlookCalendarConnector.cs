using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AttentionHub.Client;

public sealed record CalendarConnectorResult(
    bool Success,
    IReadOnlyList<MockAttentionEvent> Events,
    string Message)
{
    public static CalendarConnectorResult Failed(string message)
    {
        return new CalendarConnectorResult(false, [], message);
    }
}

public sealed class OutlookCalendarConnector
{
    private const int OlFolderCalendar = 9;

    public Task<CalendarConnectorResult> ReadUpcomingBusyEventsAsync(int daysAhead = 7)
    {
        return Task.Run(() => ReadUpcomingBusyEvents(daysAhead));
    }

    private static CalendarConnectorResult ReadUpcomingBusyEvents(int daysAhead)
    {
        object? outlookApp = null;
        object? outlookNamespace = null;
        object? calendarFolder = null;
        object? items = null;
        object? restrictedItems = null;

        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null)
            {
                return CalendarConnectorResult.Failed("Outlook Desktop nao encontrado via COM.");
            }

            outlookApp = Activator.CreateInstance(outlookType);
            if (outlookApp is null)
            {
                return CalendarConnectorResult.Failed("Nao foi possivel iniciar o Outlook via COM.");
            }

            dynamic app = outlookApp;
            outlookNamespace = app.GetNamespace("MAPI");
            dynamic session = outlookNamespace;
            calendarFolder = session.GetDefaultFolder(OlFolderCalendar);
            dynamic folder = calendarFolder;

            items = folder.Items;
            dynamic calendarItems = items;
            calendarItems.IncludeRecurrences = true;
            calendarItems.Sort("[Start]");

            var start = DateTime.Today;
            var end = start.AddDays(daysAhead);
            var filter = $"[Start] >= '{start:MM/dd/yyyy hh:mm tt}' AND [Start] < '{end:MM/dd/yyyy hh:mm tt}'";
            restrictedItems = calendarItems.Restrict(filter);

            var events = new List<MockAttentionEvent>();
            foreach (dynamic item in (System.Collections.IEnumerable)restrictedItems)
            {
                if (!TryMapAppointment(item, out MockAttentionEvent mapped))
                {
                    continue;
                }

                events.Add(mapped);
            }

            return new CalendarConnectorResult(
                Success: true,
                Events: events.OrderBy(item => item.OccurredAt).ToList(),
                Message: events.Count == 0
                    ? "Outlook lido com sucesso, sem eventos nos proximos dias."
                    : $"Outlook lido com sucesso: {events.Count} eventos de calendario.");
        }
        catch (COMException ex)
        {
            return CalendarConnectorResult.Failed($"Falha COM ao ler Outlook: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CalendarConnectorResult.Failed($"Falha ao ler Outlook Calendar: {ex.Message}");
        }
        finally
        {
            ReleaseComObject(restrictedItems);
            ReleaseComObject(items);
            ReleaseComObject(calendarFolder);
            ReleaseComObject(outlookNamespace);
            ReleaseComObject(outlookApp);
        }
    }

    private static bool TryMapAppointment(dynamic item, out MockAttentionEvent mapped)
    {
        mapped = null!;

        try
        {
            DateTime start = item.Start;
            DateTime end = item.End;
            if (end <= start)
            {
                return false;
            }

            var entryId = SafeString(() => item.EntryID) ?? $"{start:o}:{end:o}";
            var sourceId = "outlook-calendar";
            var hash = Hash($"{entryId}:{start:o}:{end:o}");
            var availability = MapAvailability(SafeInt(() => item.BusyStatus));
            var isRecurring = SafeBool(() => item.IsRecurring);

            mapped = new MockAttentionEvent(
                eventId: $"evt_outlook_calendar_{hash[..16]}",
                eventType: "calendar.busy",
                sourceId: sourceId,
                sourceDisplayName: "Outlook Calendar",
                sourceApp: "outlook",
                occurredAt: new DateTimeOffset(start),
                priority: "normal",
                summary: $"[Outlook] Busy {start:HH:mm}-{end:HH:mm}",
                dedupeKey: $"{sourceId}:calendar:{hash}",
                localStatus: "Novo",
                calendar: new CalendarPayload(
                    StartsAt: new DateTimeOffset(start),
                    EndsAt: new DateTimeOffset(end),
                    Availability: availability,
                    MaskedTitle: "[Outlook] Busy",
                    IsRecurring: isRecurring));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MapAvailability(int? busyStatus)
    {
        return busyStatus switch
        {
            0 => "free",
            1 => "tentative",
            3 => "out_of_office",
            _ => "busy"
        };
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? SafeString(Func<dynamic> getter)
    {
        try
        {
            return getter()?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static int? SafeInt(Func<dynamic> getter)
    {
        try
        {
            return Convert.ToInt32(getter());
        }
        catch
        {
            return null;
        }
    }

    private static bool SafeBool(Func<dynamic> getter)
    {
        try
        {
            return Convert.ToBoolean(getter());
        }
        catch
        {
            return false;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        try
        {
            if (value is not null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
        catch
        {
            // Best-effort COM cleanup only.
        }
    }
}
