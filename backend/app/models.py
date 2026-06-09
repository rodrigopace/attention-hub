from datetime import datetime
from typing import Any, Literal
from uuid import uuid4

from pydantic import BaseModel, ConfigDict, Field


Platform = Literal["windows"]
PrivacyLevel = Literal["metadata_only", "sanitized_subject", "sanitized_preview"]
Priority = Literal["low", "normal", "high", "urgent"]
EventType = Literal[
    "calendar.busy",
    "calendar.changed",
    "calendar.conflict_detected",
    "email.direct",
    "email.mention",
    "message.direct",
    "message.mention",
    "system.heartbeat",
]


class StrictModel(BaseModel):
    model_config = ConfigDict(extra="forbid")


class Device(StrictModel):
    device_id: str
    display_name: str
    platform: Platform
    agent_version: str
    timezone: str | None = None


class Source(StrictModel):
    source_id: str
    display_name: str
    environment_type: Literal["corporate", "personal", "unknown"]
    app: Literal["outlook", "microsoft_graph", "teams", "slack", "whatsapp", "google_calendar", "windows", "other"] | None = None


class CalendarPayload(StrictModel):
    starts_at: datetime
    ends_at: datetime
    availability: Literal["busy", "tentative", "free", "out_of_office"]
    masked_title: str | None = None
    is_recurring: bool = False


class EmailPayload(StrictModel):
    direction: Literal["inbound", "outbound"]
    sender_hash: str | None = None
    recipient_match: Literal["to", "cc", "mention", "none"]
    sanitized_subject: str | None = None
    has_attachments: bool | None = None


class MessagePayload(StrictModel):
    conversation_type: Literal["direct", "group", "channel", "unknown"]
    sender_hash: str | None = None
    channel_hash: str | None = None
    sanitized_preview: str | None = None


class SystemPayload(StrictModel):
    agent_version: str | None = None
    os: str | None = None
    timezone: str | None = None


class AttentionEvent(StrictModel):
    event_id: str
    event_type: EventType
    source: Source
    occurred_at: datetime
    received_at: datetime | None = None
    privacy_level: PrivacyLevel = "metadata_only"
    priority: Priority = "normal"
    dedupe_key: str
    calendar: CalendarPayload | None = None
    email: EmailPayload | None = None
    message: MessagePayload | None = None
    system: SystemPayload | None = None


class SyncRequest(StrictModel):
    request_id: str = Field(default_factory=lambda: f"req_{uuid4().hex}")
    device: Device
    sync_cursor: str | None = None
    sent_at: datetime | None = None
    events: list[AttentionEvent]
    client_capabilities: list[
        Literal[
            "windows_notifications",
            "outlook_calendar_read",
            "outlook_email_metadata",
            "local_audit_preview",
            "system_proxy",
        ]
    ] = []


class RejectedEvent(StrictModel):
    event_id: str
    reason: str


class Rule(StrictModel):
    rule_id: str
    enabled: bool
    rule_type: Literal[
        "alert_email_to_me",
        "alert_mentions",
        "alert_direct_messages",
        "quiet_hours",
        "metadata_only",
        "mask_calendar_titles",
    ]
    config: dict[str, Any] = Field(default_factory=dict)


class Action(StrictModel):
    action_id: str
    action_type: Literal[
        "show_local_notification",
        "refresh_rules",
        "slow_down_polling",
        "reauthenticate_calendar",
        "none",
    ]
    message: str | None = None


class SyncStatus(StrictModel):
    calendar_sync: Literal["ok", "degraded", "disabled", "reauth_required"] = "disabled"
    recommended_poll_interval_seconds: int = Field(default=120, ge=30)


class SyncResponse(StrictModel):
    request_id: str
    accepted_event_ids: list[str]
    duplicate_event_ids: list[str] = Field(default_factory=list)
    rejected_events: list[RejectedEvent]
    next_sync_cursor: str
    server_time: datetime
    effective_rules: list[Rule] = Field(default_factory=list)
    actions: list[Action] = Field(default_factory=list)
    status: SyncStatus


class HealthResponse(StrictModel):
    status: Literal["ok"]
    storage: str


class CentralCalendarEvent(StrictModel):
    event_id: str
    source: str
    starts_at: datetime
    ends_at: datetime
    title: str
    availability: Literal["busy", "tentative", "free", "out_of_office"] = "busy"


class CentralCalendarEventsResponse(StrictModel):
    status: Literal["ok", "disabled", "degraded", "reauth_required"]
    events: list[CentralCalendarEvent] = Field(default_factory=list)
    errors: list[str] = Field(default_factory=list)
