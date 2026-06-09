from functools import lru_cache

from app.config import get_settings
from app.calendar_sync import CalendarSyncService, DisabledCalendarSyncService, GoogleCalendarSyncService
from app.sqlite_store import SQLiteEventStore
from app.storage import EventStore


@lru_cache
def get_event_store() -> EventStore:
    settings = get_settings()
    store = SQLiteEventStore(settings.sqlite_path)
    store.initialize()
    return store


@lru_cache
def get_calendar_sync_service() -> CalendarSyncService:
    settings = get_settings()
    if not settings.google_calendar_enabled:
        return DisabledCalendarSyncService()
    return GoogleCalendarSyncService(settings)
