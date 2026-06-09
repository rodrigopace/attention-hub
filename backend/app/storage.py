from abc import ABC, abstractmethod
from dataclasses import dataclass
from datetime import datetime

from app.models import AttentionEvent, Device


@dataclass(frozen=True)
class StoreSyncResult:
    accepted_event_ids: list[str]
    duplicate_event_ids: list[str]
    next_sync_cursor: str


class EventStore(ABC):
    @abstractmethod
    def initialize(self) -> None:
        """Create or migrate storage structures."""

    @abstractmethod
    def health(self) -> str:
        """Return a short storage health label."""

    @abstractmethod
    def store_sync(
        self,
        *,
        request_id: str,
        device: Device,
        sync_cursor: str | None,
        received_at: datetime,
        events: list[AttentionEvent],
    ) -> StoreSyncResult:
        """Persist a sync request and its new events."""

