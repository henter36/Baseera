# Phase D.4 Occupancy Current State Analysis

Branch: `phase-d4-facility-occupancy-inmate-movement`

## Existing State

| Area | Current state |
| --- | --- |
| Facility | Exists in `Baseera.Domain.Organization.Facility` with region scope, soft delete, row version, and active flag. |
| FacilityUnit | Exists with hierarchy and facility scope, but has no capacity or current population fields. |
| Inmate | No inmate entity exists. |
| Capacity | No approved capacity table exists for facility or unit. |
| Census snapshots | No authoritative or reconciled inmate count snapshot exists. |
| Movement records | No admission, release, transfer, or internal movement table exists. |
| External integration | No inmate-system integration contract exists. |
| Workspace | Phase D.3 shows occupancy as an explicit data-quality gap and exposes facility units with note/action counts only. |
| Permissions | Workspace and organization permissions exist; no occupancy-specific permission exists. |
| Audit | Append-only `AuditLog` exists and is suitable for capacity/snapshot/import/reconciliation command auditing. |
| Attachments | Generic attachment service exists but is not required for this MVP. |

## Missing Capabilities

- Approved operational capacity per facility and per unit.
- Statistical census snapshot with source, quality, and authoritative flag.
- Movement events with idempotent external event identifiers and non-display inmate reference hash.
- Source-of-truth resolver that prevents double counting.
- Server-side occupancy summary, unit breakdown, movement summary, intervention reasons, and data-quality classification.
- Workspace widget that replaces the D.3 occupancy gap when the user has occupancy permissions.
- Management entry points for capacity, snapshots, imports, and reconciliation.

## Potential Sources of Truth

1. Authoritative external census snapshot.
2. Reconciled internal snapshot.
3. Derived movement projection when explicitly enabled and traceable.
4. Unknown when no trustworthy source exists.

Phase D.4 implements these source contracts inside Baseera. It does not integrate with a real external inmate system in this PR.

## Legal and Privacy Risks

- Workspace-level summaries must not expose inmate names, national identifiers, or raw external inmate IDs.
- `InmateReferenceHash` is stored only for deduplication/reconciliation and is never returned by facility workspace payloads.
- Movement detail endpoints must require `Occupancy.ViewMovements`; sensitive movement identifiers require `Occupancy.ViewSensitiveMovements`.
- PII must not appear in URLs, frontend cache keys, logs, telemetry, or AuditLog payloads.

## Double Counting Risks

- Summing all movements on every request can double count corrections, reversals, temporary leave, or external duplicates.
- Combining snapshots and movements without a documented rule can inflate counts.
- Latest timestamp alone is not reliable; `IsAuthoritative` and quality status matter.
- Phase D.4 uses the latest authoritative snapshot as the primary count and reports movement deltas separately.

## Migration Plan

- Add one EF Core migration with `FacilityCapacityBaselines`, `InmateCensusSnapshots`, and `InmateMovementEvents`.
- Add foreign keys to Organization, Facility, FacilityUnit, and reversal event references with restrict behavior.
- Add row versions, soft-delete columns where historical administrative correction may be needed, check constraints, and indexes for latest-per-facility/unit queries.
- Add unique filtered index for `(SourceType, SourceReference, ExternalEventId)` when `ExternalEventId` is present.

## Backfill Plan

- No production backfill is performed automatically.
- Existing facilities start with `Unknown` occupancy status until a capacity baseline and census snapshot are recorded or imported.
- Development/demo seed may create non-production sample occupancy records only when existing demo seed is enabled.

## Phase Boundaries

- Includes capacity, census snapshots, movement events, source resolver, workspace occupancy integration, and basic management views.
- Does not implement inmate identity management, personal inmate profiles, Region/HQ workspaces, vehicles, weapons, workforce, full resource center, AI, prediction, or simulation.

## Expected File Map

- Domain: `Baseera.Domain/Occupancy`.
- Application: occupancy DTOs, policies, query service, command/import service, workspace integration.
- Infrastructure: EF configuration, DbContext DbSets, migration, optional demo seed.
- API: `/api/v1/facilities/{facilityId}/occupancy/*`.
- Frontend: Facility Workspace occupancy section and protected occupancy management pages.
- Docs: Phase D.4 scope, model, source-of-truth, API, permissions, security, import, reconciliation, performance, migration, RTL, tests, and completion report.
