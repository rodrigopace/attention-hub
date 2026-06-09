# Backend

FastAPI MVP backend for Attention Hub.

Implemented endpoints:

- `GET /health`
- `POST /sync`

The backend currently stores device state, sync request audit records, and attention events in SQLite. Storage is isolated behind `EventStore`, so a future PostgreSQL or cloud database implementation can be added without changing route handlers.

Google Calendar sync is optional. If not configured, `/sync` still accepts and stores events with `calendar_sync: disabled`.

## Run Locally

```powershell
cd backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e .[test]
uvicorn app.main:app --reload
```

The default database is:

```text
backend/data/attention-hub.sqlite3
```

Override it with:

```powershell
$env:ATTENTION_HUB_DATABASE_URL = "sqlite:///./data/dev.sqlite3"
```

## Test

```powershell
cd backend
pytest
```

## P0 Boundaries

No real Teams, Slack, or WhatsApp integrations are implemented yet. `/sync` accepts the contracts, persists metadata-first events, and can optionally write masked busy blocks to Google Calendar.

## Google Calendar Sync

Set these environment variables to enable central calendar writes:

```powershell
$env:ATTENTION_HUB_GOOGLE_CALENDAR_ID = "primary"
$env:ATTENTION_HUB_GOOGLE_CALENDAR_ACCESS_TOKEN = "<oauth-access-token>"
```

Optional:

```powershell
$env:ATTENTION_HUB_GOOGLE_CALENDAR_BUSY_TITLE_TEMPLATE = "[{source_display_name}] Busy"
$env:ATTENTION_HUB_GOOGLE_CALENDAR_TIMEOUT_SECONDS = "10"
```

Behavior:

- only `calendar.busy` events are written;
- Google Calendar event titles remain masked, for example `[Empresa A] Busy`;
- event visibility is `private`;
- event body/location/attendees are not written;
- upsert is done by `attentionHubDedupeKey` in Google Calendar private extended properties;
- `calendar_sync` returns `ok`, `degraded`, `disabled`, or `reauth_required`.

This MVP expects a valid Google OAuth access token. A production version should add a proper OAuth consent/refresh-token flow.
