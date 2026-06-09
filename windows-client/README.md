# Windows Client

Minimal WPF/.NET Windows client for the MVP sync loop.

Implemented in this first version:

- local desktop UI;
- mocked calendar, email, Teams, and Slack events;
- read-only Outlook Calendar loading via local Windows COM;
- manual `POST /sync`;
- polling timer initiated by the client;
- backend response preview;
- no real Teams, Slack, WhatsApp, or Google Calendar integrations.

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

## Outlook Calendar Read-Only Connector

The `Agenda` screen has a `Carregar Outlook` button. It attempts to read upcoming local Outlook Desktop calendar items through COM and maps them to `calendar.busy` events with masked titles.

Privacy behavior:

- meeting subject is not sent;
- attendee/body/location are not read into the payload;
- the generated title is `[Outlook] Busy`;
- if Outlook is unavailable or returns no events, the client keeps the mocked fallback events.
