from datetime import datetime, timezone
from uuid import uuid4

from fastapi import Depends, FastAPI

from app.calendar_sync import CalendarSyncService, CalendarSyncResult
from app.config import Settings, get_settings
from app.models import Action, HealthResponse, Rule, SyncRequest, SyncResponse, SyncStatus
from app.storage import EventStore
from app.dependencies import get_calendar_sync_service, get_event_store


def create_app() -> FastAPI:
    app = FastAPI(
        title="Attention Hub Backend",
        version="0.1.0",
        description="MVP backend for client-initiated attention event sync.",
    )

    @app.get("/health", response_model=HealthResponse)
    def health(store: EventStore = Depends(get_event_store)) -> HealthResponse:
        return HealthResponse(status="ok", storage=store.health())

    @app.post("/sync", response_model=SyncResponse)
    def sync(
        payload: SyncRequest,
        store: EventStore = Depends(get_event_store),
        calendar_sync: CalendarSyncService = Depends(get_calendar_sync_service),
        settings: Settings = Depends(get_settings),
    ) -> SyncResponse:
        now = datetime.now(timezone.utc)
        result = store.store_sync(
            request_id=payload.request_id,
            device=payload.device,
            sync_cursor=payload.sync_cursor,
            received_at=now,
            events=payload.events,
        )
        calendar_result = calendar_sync.sync_busy_events(payload.events)

        return SyncResponse(
            request_id=payload.request_id,
            accepted_event_ids=result.accepted_event_ids,
            duplicate_event_ids=result.duplicate_event_ids,
            rejected_events=[],
            next_sync_cursor=result.next_sync_cursor,
            server_time=now,
            effective_rules=_default_rules(),
            actions=_actions_for(payload, calendar_result),
            status=SyncStatus(
                calendar_sync=calendar_result.status,
                recommended_poll_interval_seconds=settings.default_poll_interval_seconds,
            ),
        )

    return app


def _default_rules() -> list[Rule]:
    return [
        Rule(rule_id="rule_metadata_only", enabled=True, rule_type="metadata_only"),
        Rule(
            rule_id="rule_email_to_me",
            enabled=True,
            rule_type="alert_email_to_me",
            config={"priority": "high"},
        ),
        Rule(
            rule_id="rule_mask_calendar",
            enabled=True,
            rule_type="mask_calendar_titles",
            config={"title_template": "[{source_display_name}] Busy"},
        ),
    ]


def _actions_for(payload: SyncRequest, calendar_result: CalendarSyncResult) -> list[Action]:
    actions: list[Action] = []
    for event in payload.events:
        if event.event_type == "email.direct" and event.priority in {"high", "urgent"}:
            actions.append(
                Action(
                    action_id=f"action_{uuid4().hex}",
                    action_type="show_local_notification",
                    message=f"Novo e-mail direto em {event.source.display_name}",
                )
            )
        if event.event_type in {"message.direct", "message.mention"} and event.priority == "urgent":
            actions.append(
                Action(
                    action_id=f"action_{uuid4().hex}",
                    action_type="show_local_notification",
                    message=f"Mensagem urgente em {event.source.display_name}",
                )
            )
    if calendar_result.auth_required:
        actions.append(
            Action(
                action_id=f"action_{uuid4().hex}",
                action_type="reauthenticate_calendar",
                message="Google Calendar precisa de reautenticacao ou novo access token.",
            )
        )
    return actions


app = create_app()
