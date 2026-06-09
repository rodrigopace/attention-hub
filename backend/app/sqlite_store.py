import json
import sqlite3
from datetime import datetime, timezone
from pathlib import Path

from app.models import AttentionEvent, Device
from app.storage import EventStore, StoreSyncResult


class SQLiteEventStore(EventStore):
    def __init__(self, db_path: Path):
        self.db_path = db_path

    def initialize(self) -> None:
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        with self._connect() as conn:
            conn.executescript(
                """
                PRAGMA journal_mode = WAL;

                CREATE TABLE IF NOT EXISTS devices (
                    device_id TEXT PRIMARY KEY,
                    display_name TEXT NOT NULL,
                    platform TEXT NOT NULL,
                    agent_version TEXT NOT NULL,
                    timezone TEXT,
                    first_seen_at TEXT NOT NULL,
                    last_seen_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS sync_requests (
                    request_id TEXT PRIMARY KEY,
                    device_id TEXT NOT NULL,
                    sync_cursor TEXT,
                    received_at TEXT NOT NULL,
                    event_count INTEGER NOT NULL,
                    FOREIGN KEY(device_id) REFERENCES devices(device_id)
                );

                CREATE TABLE IF NOT EXISTS attention_events (
                    event_id TEXT PRIMARY KEY,
                    dedupe_key TEXT NOT NULL UNIQUE,
                    event_type TEXT NOT NULL,
                    source_id TEXT NOT NULL,
                    source_display_name TEXT NOT NULL,
                    source_app TEXT,
                    priority TEXT NOT NULL,
                    privacy_level TEXT NOT NULL,
                    occurred_at TEXT NOT NULL,
                    received_at TEXT,
                    device_id TEXT NOT NULL,
                    request_id TEXT NOT NULL,
                    payload_json TEXT NOT NULL,
                    stored_at TEXT NOT NULL,
                    FOREIGN KEY(device_id) REFERENCES devices(device_id),
                    FOREIGN KEY(request_id) REFERENCES sync_requests(request_id)
                );

                CREATE INDEX IF NOT EXISTS idx_attention_events_occurred_at
                    ON attention_events(occurred_at);

                CREATE INDEX IF NOT EXISTS idx_attention_events_source
                    ON attention_events(source_id, event_type);
                """
            )

    def health(self) -> str:
        with self._connect() as conn:
            conn.execute("SELECT 1").fetchone()
        return "sqlite"

    def store_sync(
        self,
        *,
        request_id: str,
        device: Device,
        sync_cursor: str | None,
        received_at: datetime,
        events: list[AttentionEvent],
    ) -> StoreSyncResult:
        received_text = _format_dt(received_at)
        accepted: list[str] = []
        duplicates: list[str] = []

        with self._connect() as conn:
            conn.execute("BEGIN")
            self._upsert_device(conn, device, received_text)
            conn.execute(
                """
                INSERT OR IGNORE INTO sync_requests (
                    request_id, device_id, sync_cursor, received_at, event_count
                ) VALUES (?, ?, ?, ?, ?)
                """,
                (request_id, device.device_id, sync_cursor, received_text, len(events)),
            )

            for event in events:
                inserted = self._insert_event(conn, request_id, device.device_id, event, received_text)
                if inserted:
                    accepted.append(event.event_id)
                else:
                    duplicates.append(event.event_id)

            conn.commit()

        return StoreSyncResult(
            accepted_event_ids=accepted,
            duplicate_event_ids=duplicates,
            next_sync_cursor=f"cursor_{int(received_at.timestamp())}_{len(accepted)}",
        )

    def _connect(self) -> sqlite3.Connection:
        conn = sqlite3.connect(self.db_path)
        conn.row_factory = sqlite3.Row
        return conn

    def _upsert_device(self, conn: sqlite3.Connection, device: Device, seen_at: str) -> None:
        conn.execute(
            """
            INSERT INTO devices (
                device_id, display_name, platform, agent_version, timezone, first_seen_at, last_seen_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(device_id) DO UPDATE SET
                display_name = excluded.display_name,
                platform = excluded.platform,
                agent_version = excluded.agent_version,
                timezone = excluded.timezone,
                last_seen_at = excluded.last_seen_at
            """,
            (
                device.device_id,
                device.display_name,
                device.platform,
                device.agent_version,
                device.timezone,
                seen_at,
                seen_at,
            ),
        )

    def _insert_event(
        self,
        conn: sqlite3.Connection,
        request_id: str,
        device_id: str,
        event: AttentionEvent,
        stored_at: str,
    ) -> bool:
        payload = event.model_dump(mode="json")
        cursor = conn.execute(
            """
            INSERT OR IGNORE INTO attention_events (
                event_id,
                dedupe_key,
                event_type,
                source_id,
                source_display_name,
                source_app,
                priority,
                privacy_level,
                occurred_at,
                received_at,
                device_id,
                request_id,
                payload_json,
                stored_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                event.event_id,
                event.dedupe_key,
                event.event_type,
                event.source.source_id,
                event.source.display_name,
                event.source.app,
                event.priority,
                event.privacy_level,
                _format_dt(event.occurred_at),
                _format_dt(event.received_at) if event.received_at else None,
                device_id,
                request_id,
                json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
                stored_at,
            ),
        )
        return cursor.rowcount == 1


def _format_dt(value: datetime) -> str:
    if value.tzinfo is None:
        value = value.replace(tzinfo=timezone.utc)
    return value.isoformat()

