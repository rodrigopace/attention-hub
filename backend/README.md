# Backend

Placeholder for the MVP backend.

The first backend implementation should expose:

- `GET /health`
- `POST /sync`

The backend should own:

- device registration state;
- event ingestion;
- deduplication;
- rule evaluation;
- consolidated event state;
- Google Calendar write integration after the mock sync loop is proven;
- audit records showing exactly what the client sent.

No real external integrations are implemented in this initial repository shape.

