# ADR 0001: Windows-First Client With Outbound Polling

## Status

Accepted for MVP P0.

## Context

Corporate environments may block inbound traffic, unsolicited callbacks, browser extensions, or unofficial integrations. A Windows-first MVP also matches the expected first user environment and gives better access to Outlook Desktop, Windows notifications, local proxy settings, Windows Credential Manager, tray behavior, and local notifications.

## Decision

Build the initial client as a Windows desktop client/agent. The client will initiate sync with the backend using outbound HTTPS/443 polling. The backend will not push directly to the client in P0.

## Consequences

- The system is more likely to work on locked-down networks.
- Alerts are near-real-time, not instant.
- Backend commands/configuration must be returned as part of polling responses.
- Future clients can reuse the same `POST /sync` contract.

