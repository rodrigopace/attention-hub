# MVP P0 Specification

## Goal

Validate whether a Windows-first attention hub can reduce missed meetings, missed direct emails, and missed direct messages for a user working across multiple corporate environments.

The MVP consolidates attention signals. It does not attempt to centralize full corporate content.

## Product Principles

- Client-initiated communication only.
- HTTPS/443 for network compatibility.
- Metadata-only by default.
- Local masking before backend sync.
- User-visible audit of outbound payloads.
- No automatic writes to corporate calendars in P0.

## P0 Features

| Area | Feature | Priority |
| --- | --- | --- |
| Calendar | Publish masked busy blocks to a personal calendar | P0 |
| Calendar | Detect conflicts across sources | P0 |
| Email | Alert when the user is in the `To` field | P0 |
| Messages | Capture direct/mention notifications from Windows notifications | P0 |
| Sync | Client polls `POST /sync` through HTTPS/443 | P0 |
| Rules | Silence sources, types, and quiet hours | P0 |
| Status | Show last sync per device/source | P0 |
| Privacy | Show outbound payload preview/audit | P0 |

## P0 Non-Goals

- Android support.
- macOS/Linux support.
- Bidirectional sync.
- Automatic creation of busy blocks in corporate calendars.
- Full-text ingestion of email/message bodies.
- Admin-level corporate integrations.

## Success Criteria

- The user sees all relevant busy windows in one personal calendar.
- Calendar conflicts are detected before the meeting window.
- Direct email alerts arrive within the configured polling target.
- Message notifications appear in the unified event inbox.
- The user can verify exactly what data left the local machine.
- The system works with outbound HTTPS/443 only.

