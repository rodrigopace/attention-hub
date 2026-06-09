# Contracts

This directory contains the initial wire contracts for MVP P0.

The core endpoint is:

```text
POST /sync
```

Request schema:

- `schemas/sync-request.schema.json`

Response schema:

- `schemas/sync-response.schema.json`

Event schema:

- `schemas/attention-event.schema.json`

Example payloads:

- `examples/sync-request.example.json`
- `examples/sync-response.example.json`

Contracts are intentionally metadata-first. Sensitive content fields are omitted by default.

