# Windows Client

Minimal WPF/.NET Windows client for the MVP sync loop.

Implemented in this first version:

- local desktop UI;
- mocked calendar, email, Teams, and Slack events;
- mock/real Windows notification loading with fallback;
- read-only Office 365 calendar loading through Microsoft Graph device code OAuth;
- read-only Outlook Calendar loading via local Windows COM;
- manual `POST /sync`;
- polling timer initiated by the client;
- backend response preview;
- Google Calendar central agenda loading from backend `/calendar/events`.
- no real Teams, Slack, or WhatsApp integrations.

## Run

Start the backend first:

```powershell
cd backend
.\.venv\Scripts\Activate.ps1
uvicorn app.main:app --reload
```

Then run the Windows client:

```powershell
cd windows-client\AttentionHub.Client
dotnet run
```

The default backend URL in the UI is:

```text
http://localhost:8000
```

Polling is disabled by default. Use `Sincronizar` for a manual sync or `Iniciar polling` to poll automatically.

Use `Agenda -> Carregar Google Calendar` to show events returned by the backend central calendar endpoint.

Use `Eventos -> Carregar notificacoes Windows` to request access to active Windows toast notifications and add them to the local inbox.

## Local Settings

The client persists basic settings as JSON in:

```text
%LocalAppData%\AttentionHub\client-settings.json
```

Persisted fields:

- backend URL;
- polling interval in seconds;
- device id;
- device display name.

Use the `Status` screen to edit and save device settings. Backend URL and polling interval are also saved when syncing or starting polling.

## Windows Notification Connector

The `Eventos` screen has a `Carregar notificacoes Windows` button. It uses the Windows `UserNotificationListener` API to read active toast notifications when the OS grants access.

Behavior:

- maps active notifications to local `message.direct` or `message.mention` events;
- ignores Codex and Attention Hub notifications by default;
- groups notifications by app and conversation, showing one local inbox row per conversation with a count in the summary;
- keeps the mock inbox as fallback if access is denied or unavailable;
- replaces the previous Windows notification snapshot when notifications are loaded again;
- does not persist notification content outside the existing local event list unless the user syncs.

Limitations:

- Windows may require explicit notification listener permission;
- some desktop/unpackaged app contexts can be denied by the OS;
- notification text is best-effort because each app structures toast content differently.

## Outlook Calendar Read-Only Connector

## Microsoft Graph Calendar Read-Only Connector

The `Agenda` screen has a `Carregar Office 365` button. It uses Microsoft Graph device code OAuth and reads `/me/calendarView` with `Calendars.ReadBasic`.

Setup:

1. Register a public client app in Microsoft Entra ID.
2. Enable public client/device code flow for the app.
3. Add delegated Microsoft Graph permissions:
   - `Calendars.ReadBasic`
   - `User.Read`
4. Copy the app/client id into the `Status` screen as `Microsoft Client ID`.
5. Use `common`, `organizations`, or your tenant id in `Tenant`.

The connector keeps the access token in memory only. It does not persist Microsoft tokens to disk in this MVP.

Privacy behavior:

- subject/body/location/attendees are not requested from Graph;
- requested fields are limited to id, start, end, showAs, cancellation state, organizer flag, and type;
- generated local event titles are masked as `[Office 365] Busy`.

## Outlook Calendar Read-Only Connector

The `Agenda` screen has a `Carregar Outlook` button. It attempts to read upcoming local Outlook Desktop calendar items through COM and maps them to `calendar.busy` events with masked titles.

Privacy behavior:

- meeting subject is not sent;
- attendee/body/location are not read into the payload;
- the generated title is `[Outlook] Busy`;
- if Outlook is unavailable or returns no events, the client keeps the mocked fallback events.

Important limitation:

- this connector only works with a running and configured classic Outlook COM instance;
- it does not start or configure classic Outlook;
- the new Outlook/Office 365 app does not expose the same COM calendar surface, so it needs a future Microsoft Graph connector.
