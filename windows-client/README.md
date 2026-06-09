# Windows Client

Placeholder for the Windows desktop client/agent.

The client should own:

- local UI for `Now`, `Events`, `Agenda`, `Rules`, and `Status`;
- polling outbound to the backend through HTTPS/443;
- local Windows notifications;
- collection of local calendar/email/message signals;
- local masking before data leaves the machine;
- local audit preview of outbound payloads;
- Windows proxy and credential-store support.

The first implementation should use mocked or manually created local events before any real Outlook, Teams, Slack, WhatsApp, or Google Calendar integration.

