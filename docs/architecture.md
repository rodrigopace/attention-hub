# Architecture

## High-Level Flow

```text
Windows Client / Agent
  -> collects local attention events
  -> masks sensitive fields
  -> POST /sync over HTTPS/443
  -> receives rules, acknowledgements, and server status
  -> updates local UI and Windows notifications

Backend
  -> validates contracts
  -> deduplicates events
  -> applies rules
  -> persists audit and consolidated state
  -> updates personal calendar target
```

## Why Polling

The client initiates all network communication to avoid dependency on inbound connectivity to corporate machines. The backend can still return rule updates, acknowledgements, errors, and action hints in the response to the same `POST /sync` call.

## Initial Polling Targets

| Flow | Initial target |
| --- | --- |
| Email/message alerts | 1-3 minutes |
| Calendar updates | 5-15 minutes |
| Rules/config | Included in `/sync` response |
| Heartbeat | 5 minutes |

## Privacy Boundary

The Windows client is the privacy boundary. It should mask or drop sensitive fields before sending events to the backend. The backend must treat received data as the maximum allowed detail and should not request full corporate content in P0.

