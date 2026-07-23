# Phase C.4 — Migration Notes

Migration: `20260723050801_PhaseC4FormResponseWorkflow`

- Adds response tables, policy table, assignment alternate key for composite FK
- Backfills policies for existing campaigns
- Restrict delete; no cascade on history
- Rollback: `dotnet ef database update <previous>` then remove C4 migration only if never shipped; do not edit C3 migrations

Tested: empty DB apply, upgrade from main, idempotent second apply (EF history).
