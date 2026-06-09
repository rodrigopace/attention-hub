# Windows Client

Minimal WPF/.NET Windows client for the MVP sync loop.

Implemented in this first version:

- local desktop UI;
- mocked calendar, email, Teams, and Slack events;
- manual `POST /sync`;
- polling timer initiated by the client;
- backend response preview;
- no real Outlook, Teams, Slack, WhatsApp, or Google Calendar integrations.

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
