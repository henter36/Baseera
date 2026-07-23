# Phase C.4 — Migration Notes

Migrations:

- `20260723050801_PhaseC4FormResponseWorkflow`
- `20260723061641_PhaseC4ResponseWorkflowHardening` (approve-level unique index + SoD backfill correction)

## What C.4 adds

- Response tables, campaign response policy table, assignment alternate key for composite FK
- Backfills policies for existing campaigns (`CreatedBy = migration:PhaseC4`)
- Restrict delete; no cascade on history

## Hardening migration

- Drops unique index that included `ReviewedByUserId`
- Adds filtered unique index `IX_FormResponseReviewDecisions_ApproveLevel` on `(ResponseId, SubmissionId, ReviewLevel)` where `[Decision] = 2` (Approve)
- Corrects Phase C.4 campaign policy backfill: `RequireSeparationOfDuties = 1` only for rows with `CreatedBy = migration:PhaseC4` **and** `UpdatedAtUtc IS NULL` (preserves admin edits)

## Rollback (shell-safe)

List migrations, then target the migration **immediately before** C.4:

```bash
dotnet ef migrations list \
  --project src/backend/Baseera.Infrastructure \
  --startup-project src/backend/Baseera.Api
```

Previous migration before C.4 (as of this branch): `20260723021630_PhaseC3CompositeAssignmentCycleFk`

```bash
dotnet ef database update 20260723021630_PhaseC3CompositeAssignmentCycleFk \
  --project src/backend/Baseera.Infrastructure \
  --startup-project src/backend/Baseera.Api
```

To roll back only the hardening migration while keeping C.4 tables:

```bash
dotnet ef database update 20260723050801_PhaseC4FormResponseWorkflow \
  --project src/backend/Baseera.Infrastructure \
  --startup-project src/backend/Baseera.Api
```

Do not edit C.3 migrations. Remove a C.4 migration from source only if it was never shipped.

## Verification

Tested: empty DB apply, upgrade from `main`, idempotent second apply (EF history), filtered approve unique index, SoD backfill for `migration:PhaseC4` rows only.
