from fastapi.testclient import TestClient

from app.dependencies import get_event_store
from app.main import create_app
from app.sqlite_store import SQLiteEventStore


def build_client(tmp_path):
    app = create_app()
    store = SQLiteEventStore(tmp_path / "test.sqlite3")
    store.initialize()

    app.dependency_overrides[get_event_store] = lambda: store
    return TestClient(app)


def test_health_returns_sqlite(tmp_path):
    client = build_client(tmp_path)

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "storage": "sqlite"}


def test_sync_accepts_events_and_deduplicates(tmp_path):
    client = build_client(tmp_path)
    payload = {
        "request_id": "req_test_001",
        "device": {
            "device_id": "win-notebook-empresa-a",
            "display_name": "Notebook Empresa A",
            "platform": "windows",
            "agent_version": "0.1.0",
            "timezone": "America/Sao_Paulo",
        },
        "sync_cursor": None,
        "sent_at": "2026-06-09T09:00:00-03:00",
        "events": [
            {
                "event_id": "evt_email_001",
                "event_type": "email.direct",
                "source": {
                    "source_id": "empresa-a",
                    "display_name": "Empresa A",
                    "environment_type": "corporate",
                    "app": "outlook",
                },
                "occurred_at": "2026-06-09T08:58:00-03:00",
                "received_at": "2026-06-09T08:58:05-03:00",
                "privacy_level": "metadata_only",
                "priority": "high",
                "dedupe_key": "empresa-a:email:msg_opaque_123",
                "email": {
                    "direction": "inbound",
                    "sender_hash": "sha256:sender-token",
                    "recipient_match": "to",
                    "has_attachments": False,
                },
            }
        ],
        "client_capabilities": ["outlook_email_metadata"],
    }

    first = client.post("/sync", json=payload)
    second = client.post("/sync", json={**payload, "request_id": "req_test_002"})

    assert first.status_code == 200
    assert first.json()["accepted_event_ids"] == ["evt_email_001"]
    assert first.json()["duplicate_event_ids"] == []
    assert first.json()["actions"][0]["action_type"] == "show_local_notification"

    assert second.status_code == 200
    assert second.json()["accepted_event_ids"] == []
    assert second.json()["duplicate_event_ids"] == ["evt_email_001"]
    assert second.json()["rejected_events"] == []
