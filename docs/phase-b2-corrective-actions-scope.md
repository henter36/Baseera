# Phase B.2.1 — Corrective Actions Scope

## Included

- `CorrectiveActions` module inside the existing modular monolith.
- Multiple independent corrective actions per operational note.
- Server-side Permission + Scope checks for every endpoint.
- Scope is derived from the parent `OperationalNote`; clients never submit action scope ids.
- SQL Server persistence through EF Core, one migration: `PhaseB2CorrectiveActionsCore`.
- AuditLog events for create, update, workflow, archive/restore, sensitive view, attachments, and note guards.
- RTL React routes backed by the real API:
  - `/corrective-actions`
  - `/notes/:noteId/corrective-actions/new`
  - `/corrective-actions/:id`
  - `/corrective-actions/:id/edit`

## Explicit Exclusions

No automatic escalation, notifications, background jobs, email/SMS, executive dashboard, reports/export, institutional KPIs, recurring/scheduled corrective actions, form builder, vehicles, armament, workforce, AI, external integrations, Phase B.2.2, or Phase B.3.

## Acceptance Boundary

Phase B.2.1 is accepted only when baseline remains green, skipped tests are zero, prior migrations are unchanged, and corrective actions are persisted, scoped, audited, versioned, and exposed through API and RTL frontend without mock operational paths.
