# Phase B.1 — Scope

**Branch:** `phase-b1-notes-core`  
**Base:** `main` @ `5dc0b5b8efab8214c8a38cad839f8964f9f59f08`

## In scope

- Operational Notes (`OperationalNote`) CRUD + workflow
- Assignments (`NoteAssignment`) with history
- Visible timeline (`NoteStatusHistory`, append-only)
- Permission + organizational scope on every endpoint
- Critical SoD for verify-closure
- Attachments on `OperationalNote`
- AuditLog for all mutating/sensitive events
- RTL React pages: list / new / detail / edit
- Unit, Integration, Frontend tests + CI gates

## Explicit exclusions (not in B.1)

- Multi-step corrective actions, escalation, notifications
- Background jobs, executive dashboard, reports/export
- Form builder, vehicles, armament, workforce
- AI, external integrations, maps/GPS
- Phase B.2 / B.3

## Security invariants retained from A.1

- No production TestAuth/DemoSeed
- Soft delete only for operational records
- Out-of-scope → NotFound (anti-enumeration)
- PendingScan attachments not downloadable
- No Sonar/Qlty rule suppressions
