# Backend

FastAPI MVP backend for Attention Hub.

Implemented endpoints:

- `GET /health`
- `POST /sync`

The backend currently stores device state, sync request audit records, and attention events in SQLite. Storage is isolated behind `EventStore`, so a future PostgreSQL or cloud database implementation can be added without changing route handlers.

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

No real Outlook, Teams, Slack, WhatsApp, or Google Calendar integrations are implemented yet. `/sync` accepts the contracts and persists metadata-first events for the Windows client loop.
