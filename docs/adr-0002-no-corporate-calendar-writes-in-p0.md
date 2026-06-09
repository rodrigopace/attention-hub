# ADR 0002: No Corporate Calendar Writes in P0

## Status

Accepted for MVP P0.

## Context

Writing busy blocks back to corporate calendars could reduce meeting overlap, but it also introduces compliance, permission, audit, and user-trust risks.

## Decision

P0 will read calendar signals, publish masked busy blocks to a personal calendar, detect conflicts, and alert the user. P0 will not automatically create or modify events in corporate calendars.

## Consequences

- The MVP validates value with lower organizational risk.
- Conflict resolution remains user-assisted.
- A later phase may add opt-in corporate busy-block creation if policy and permissions allow it.

