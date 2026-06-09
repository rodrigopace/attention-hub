from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime, timedelta

import httpx

from app.config import Settings
from app.models import AttentionEvent
from app.models import CentralCalendarEvent


@dataclass(frozen=True)
class CalendarSyncResult:
    status: str
    synced_event_ids: list[str] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    auth_required: bool = False


class CalendarSyncService(ABC):
    @abstractmethod
    def sync_busy_events(self, events: list[AttentionEvent]) -> CalendarSyncResult:
        """Create or update central calendar busy blocks."""

    @abstractmethod
    def list_events(self, days_ahead: int = 14) -> tuple[str, list[CentralCalendarEvent], list[str]]:
        """List central calendar events."""


class DisabledCalendarSyncService(CalendarSyncService):
    def sync_busy_events(self, events: list[AttentionEvent]) -> CalendarSyncResult:
        return CalendarSyncResult(status="disabled")

    def list_events(self, days_ahead: int = 14) -> tuple[str, list[CentralCalendarEvent], list[str]]:
        return "disabled", [], []


class GoogleCalendarSyncService(CalendarSyncService):
    def __init__(self, settings: Settings):
        self.calendar_id = settings.google_calendar_id
        self.access_token = settings.google_calendar_access_token
        self.timeout_seconds = settings.google_calendar_timeout_seconds
        self.title_template = settings.google_calendar_busy_title_template

    def sync_busy_events(self, events: list[AttentionEvent]) -> CalendarSyncResult:
        busy_events = [event for event in events if event.event_type == "calendar.busy" and event.calendar is not None]
        if not busy_events:
            return CalendarSyncResult(status="ok")

        synced: list[str] = []
        errors: list[str] = []

        with httpx.Client(timeout=self.timeout_seconds) as client:
            for event in busy_events:
                try:
                    self._upsert_busy_event(client, event)
                    synced.append(event.event_id)
                except GoogleCalendarAuthError as exc:
                    return CalendarSyncResult(
                        status="reauth_required",
                        synced_event_ids=synced,
                        errors=[str(exc)],
                        auth_required=True,
                    )
                except Exception as exc:
                    errors.append(f"{event.event_id}: {exc}")

        return CalendarSyncResult(
            status="ok" if not errors else "degraded",
            synced_event_ids=synced,
            errors=errors,
        )

    def list_events(self, days_ahead: int = 14) -> tuple[str, list[CentralCalendarEvent], list[str]]:
        now = datetime.now().astimezone()
        end = now.replace(microsecond=0) + timedelta(days=days_ahead)

        try:
            with httpx.Client(timeout=self.timeout_seconds) as client:
                response = client.get(
                    self._events_url(),
                    headers=self._headers(),
                    params={
                        "timeMin": now.isoformat(),
                        "timeMax": end.isoformat(),
                        "singleEvents": "true",
                        "orderBy": "startTime",
                        "maxResults": "250",
                    },
                )
                self._raise_for_google_error(response)
                items = response.json().get("items", [])
                mapped = []
                for item in items:
                    event = _map_google_event(item)
                    if event is not None:
                        mapped.append(event)
                return "ok", mapped, []
        except GoogleCalendarAuthError as exc:
            return "reauth_required", [], [str(exc)]
        except Exception as exc:
            return "degraded", [], [str(exc)]

    def _upsert_busy_event(self, client: httpx.Client, event: AttentionEvent) -> None:
        existing_event_id = self._find_existing_google_event_id(client, event)
        body = self._build_google_event_body(event)

        if existing_event_id:
            response = client.patch(
                self._event_url(existing_event_id),
                headers=self._headers(),
                json=body,
            )
        else:
            response = client.post(
                self._events_url(),
                headers=self._headers(),
                json=body,
            )

        self._raise_for_google_error(response)

    def _find_existing_google_event_id(self, client: httpx.Client, event: AttentionEvent) -> str | None:
        response = client.get(
            self._events_url(),
            headers=self._headers(),
            params={
                "privateExtendedProperty": f"attentionHubDedupeKey={event.dedupe_key}",
                "singleEvents": "true",
                "maxResults": "1",
            },
        )
        self._raise_for_google_error(response)

        items = response.json().get("items", [])
        if not items:
            return None
        return items[0].get("id")

    def _build_google_event_body(self, event: AttentionEvent) -> dict:
        assert event.calendar is not None
        summary = self._masked_title(event)

        return {
            "summary": summary,
            "start": {"dateTime": _format_dt(event.calendar.starts_at)},
            "end": {"dateTime": _format_dt(event.calendar.ends_at)},
            "transparency": "opaque" if event.calendar.availability != "free" else "transparent",
            "visibility": "private",
            "extendedProperties": {
                "private": {
                    "attentionHubEventId": event.event_id,
                    "attentionHubDedupeKey": event.dedupe_key,
                    "attentionHubSourceId": event.source.source_id,
                }
            },
        }

    def _masked_title(self, event: AttentionEvent) -> str:
        assert event.calendar is not None
        if event.calendar.masked_title:
            return event.calendar.masked_title
        return self.title_template.format(source_display_name=event.source.display_name)

    def _headers(self) -> dict[str, str]:
        return {
            "Authorization": f"Bearer {self.access_token}",
            "Accept": "application/json",
            "Content-Type": "application/json",
        }

    def _events_url(self) -> str:
        return f"https://www.googleapis.com/calendar/v3/calendars/{self.calendar_id}/events"

    def _event_url(self, event_id: str) -> str:
        return f"{self._events_url()}/{event_id}"

    @staticmethod
    def _raise_for_google_error(response: httpx.Response) -> None:
        if response.status_code in {401, 403}:
            raise GoogleCalendarAuthError(f"Google Calendar authorization failed: {response.status_code}")
        response.raise_for_status()


class GoogleCalendarAuthError(Exception):
    pass


def _format_dt(value: datetime) -> str:
    return value.isoformat()


def _map_google_event(item: dict) -> CentralCalendarEvent | None:
    start = item.get("start", {}).get("dateTime") or item.get("start", {}).get("date")
    end = item.get("end", {}).get("dateTime") or item.get("end", {}).get("date")
    if not start or not end:
        return None

    try:
        starts_at = datetime.fromisoformat(start.replace("Z", "+00:00"))
        ends_at = datetime.fromisoformat(end.replace("Z", "+00:00"))
    except ValueError:
        return None

    private_props = item.get("extendedProperties", {}).get("private", {})
    source = private_props.get("attentionHubSourceId") or "google-calendar"
    transparency = item.get("transparency")

    return CentralCalendarEvent(
        event_id=item.get("id", ""),
        source=source,
        starts_at=starts_at,
        ends_at=ends_at,
        title=item.get("summary") or "Busy",
        availability="free" if transparency == "transparent" else "busy",
    )
