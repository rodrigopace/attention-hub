from functools import lru_cache

from app.config import get_settings
from app.sqlite_store import SQLiteEventStore
from app.storage import EventStore


@lru_cache
def get_event_store() -> EventStore:
    settings = get_settings()
    store = SQLiteEventStore(settings.sqlite_path)
    store.initialize()
    return store

