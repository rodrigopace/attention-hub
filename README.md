# Attention Hub

Windows-first MVP for consolidating attention signals across multiple corporate environments without copying sensitive corporate content.

The MVP focuses on:

- a Windows client/agent that initiates outbound sync over HTTPS/443;
- a backend sync contract for calendar, email, and message notification events;
- a central personal calendar target, initially Google Calendar;
- metadata-first privacy defaults;
- no automatic writes to corporate calendars in the first MVP.

## Repository Layout

```text
attention-hub/
  backend/          Backend placeholder and API ownership notes.
  contracts/        JSON Schemas and example payloads for MVP P0.
  docs/             Product, architecture, and decision notes.
  windows-client/   Windows client/agent placeholder and local responsibilities.
```

## MVP P0 Scope

- Windows client only.
- Polling/sync initiated by the client through HTTPS/443.
- Consolidated personal calendar update.
- Conflict detection and local alerts.
- Email alert when the user is in the `To` field.
- Message/mention alerts from Windows notifications where official APIs are unavailable.
- Metadata-only payloads by default.

Out of scope for P0:

- Android client.
- macOS/Linux clients.
- Writing busy blocks back to corporate calendars.
- Reading full message or email bodies by default.
- Production connector implementations for Outlook, Teams, Slack, WhatsApp, or Google Calendar.

## Development Order

1. Stabilize contracts in `contracts/`.
2. Implement a mock backend around `POST /sync`.
3. Implement a Windows client with mock/local manual events.
4. Add Outlook/calendar collection.
5. Add Google Calendar writing.
6. Add notification collectors and hardening.

