# Phase C.4 — Scope

## In scope
- Form response domain (one response per assignment)
- Campaign response policy (completion basis, review mode, levels, late/resubmit/SoD)
- Draft autosave with mutation idempotency + RowVersion/DraftVersion conflicts
- Server-side validation against pinned FormSchemaSnapshot
- Immutable submissions + review decisions/comments/history
- Respondent workspace + reviewer inbox (RTL)
- Attachment entity type FormResponse
- Sensitive projection + audit events
- EF migration PhaseC4FormResponseWorkflow with policy backfill

## Out of scope
- #49 compliance dashboard / KPI aggregation
- #50 reminders/escalations/notifications automation
- #51 analytics/exports
- Anonymous/public forms, advanced e-sign, workflow designer, AI

Closes #48. Depends on merged #63 (and related #47/#45).
