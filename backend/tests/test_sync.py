from datetime import datetime

from fastapi.testclient import TestClient

from app.calendar_sync import CalendarSyncResult, CalendarSyncService
from app.dependencies import get_calendar_sync_service, get_event_store
from app.main import create_app
from app.models import CentralCalendarEvent
from app.sqlite_store import SQLiteEventStore


class FakeCalendarSyncService(CalendarSyncService):
    def __init__(self, result: CalendarSyncResult | None = None):
        self.result = result or CalendarSyncResult(status="ok")
        self.synced_event_ids: list[str] = []

    def sync_busy_events(self, events):
        self.synced_event_ids = [
            event.event_id
            for event in events
            if event.event_type == "calendar.busy" and event.calendar is not None
        ]
        return self.result

    def list_events(self, days_ahead: int = 14):
        return "ok", [
            CentralCalendarEvent(
                event_id="google_evt_001",
                source="google-calendar",
                starts_at=datetime.fromisoformat("2026-06-09T09:00:00-03:00"),
                ends_at=datetime.fromisoformat("2026-06-09T10:00:00-03:00"),
                title="[Empresa A] Busy",
                availability="busy",
            )
        ], []


def build_client(tmp_path, calendar_sync_service: CalendarSyncService | None = None):
    app = create_app()
    store = SQLiteEventStore(tmp_path / "test.sqlite3")
    store.initialize()

    app.dependency_overrides[get_event_store] = lambda: store
    if calendar_sync_service is not None:
        app.dependency_overrides[get_calendar_sync_service] = lambda: calendar_sync_service
    return TestClient(app)


def build_payload(events):
    return {
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
        "events": events,
        "client_capabilities": ["outlook_email_metadata"],
    }


def test_health_returns_sqlite(tmp_path):
    client = build_client(tmp_path)

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "storage": "sqlite"}


def test_sync_accepts_events_and_deduplicates(tmp_path):
    client = build_client(tmp_path)
    payload = build_payload(
        [
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
        ]
    )

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


def test_sync_defaults_calendar_sync_to_disabled(tmp_path):
    client = build_client(tmp_path)
    payload = build_payload([])

    response = client.post("/sync", json=payload)

    assert response.status_code == 200
    assert response.json()["status"]["calendar_sync"] == "disabled"


def test_sync_forwards_calendar_busy_events_to_calendar_service(tmp_path):
    fake_calendar = FakeCalendarSyncService()
    client = build_client(tmp_path, fake_calendar)
    payload = build_payload(
        [
            {
                "event_id": "evt_cal_001",
                "event_type": "calendar.busy",
                "source": {
                    "source_id": "empresa-a",
                    "display_name": "Empresa A",
                    "environment_type": "corporate",
                    "app": "outlook",
                },
                "occurred_at": "2026-06-09T09:00:00-03:00",
                "privacy_level": "metadata_only",
                "priority": "normal",
                "dedupe_key": "empresa-a:calendar:busy:001",
                "calendar": {
                    "starts_at": "2026-06-09T09:30:00-03:00",
                    "ends_at": "2026-06-09T10:00:00-03:00",
                    "availability": "busy",
                    "masked_title": "[Empresa A] Busy",
                    "is_recurring": False,
                },
            }
        ]
    )

    response = client.post("/sync", json=payload)

    assert response.status_code == 200
    assert response.json()["status"]["calendar_sync"] == "ok"
    assert fake_calendar.synced_event_ids == ["evt_cal_001"]


def test_calendar_events_lists_central_calendar_items(tmp_path):
    client = build_client(tmp_path, FakeCalendarSyncService())

    response = client.get("/calendar/events")

    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "ok"
    assert body["events"][0]["title"] == "[Empresa A] Busy"
