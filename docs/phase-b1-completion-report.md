# Phase B.1 — Completion Report

**Branch:** `phase-b1-notes-core`  
**Base merge:** `5dc0b5b8efab8214c8a38cad839f8964f9f59f08` (PR #2 → `main`)  
**Migration:** `20260719103156_PhaseB1NotesCore`  
**PR:** https://github.com/henter36/Baseera/pull/3  
**Decision (proposed):** Phase B.1 Conditionally Accepted — functional slice + CI/Sonar/Qlty green on PR #3; awaiting explicit human acceptance. **Do not merge until approved.**

## Delivered

- Domain: `OperationalNote`, `NoteAssignment`, `NoteStatusHistory`, enums, Arabic display helpers
- Application: scope / query / command / workflow / assignment services + FluentValidation DTOs
- Infrastructure: EF configs, sequence, check constraints, seed permissions, attachment resolver
- API: `/api/v1/notes` (+ attachments list, facility-units, departments lookups)
- Frontend RTL: `/notes`, `/notes/new`, `/notes/:id`, `/notes/:id/edit`
- Authorization + organizational scope + Critical SoD + Audit + soft delete + RowVersion
- EF Role join query filters (no EF 10622 leakage of soft-deleted roles)
- Cryptography.Xml `10.0.10` pin retained (removing it reintroduces High advisories via Identity.Web)

## Explicit exclusions (unchanged)

Corrective multi-actions, auto-escalation, notifications, background jobs, executive dashboard, reports/export, form builder, vehicles/armament/workforce, AI, external integrations, maps/GPS, Phase B.2/B.3.

## Local test results (implementation tip)

| Suite | Before | After | Skipped |
|-------|--------|-------|---------|
| Unit | 71 | 201 | 0 |
| Integration | 21 | 49 | 0 |
| Frontend | 16 | 76 | 0 |

## PR #3 CI (tip `7a270a4`)

| Check | Result |
|-------|--------|
| backend | pass |
| frontend | pass |
| secret-scan | pass |
| SonarCloud Code Analysis | **pass** (Reliability remediated) |
| qlty check | pass (no blocking issues) |
| CodeRabbit | pass (review completed; rate-limit note on later push) |
| NuGet High/Critical | clean (Cryptography.Xml 10.0.10 pin retained; NU1510 warning remains) |

## Residual risks

1. Malware scanner remains stubbed (PendingScan still blocks download — intentional for B.1).
2. NU1510 warning remains while the High/Critical pin is required.
3. Restore UX requires knowing `rowVersion` for archived notes (no “include deleted” list API yet).
4. CodeRabbit findings addressed in follow-up: confidential attachment list metadata redaction, Update audit field coverage, async list-scope expansion, NoteEditPage conflict reload state clear.

## Final Three Sonar Findings

**Pre-fix SHA:** `0ae33d5eb6ceec666aca30ef69a5b6eea97fdbb6`

| Finding | Before | After |
|---------|--------|-------|
| `GET /notes` list handler parameters | **22** (individual query args) | **3** (`[AsParameters] NoteListQueryParams` → `NoteListQuery`, `INoteQueryService`, `CancellationToken`). Application `NoteListQuery` alone returned HTTP 400 under Minimal API binding; API-layer params class preserves the same query names/defaults. |
| `IntersectsNoteAsync` Cognitive Complexity | **29** | **≤15** — extracted to `NoteAssigneeScopeIntersection` coordinator + helpers (`IntersectsRegionAsync`, `IntersectsFacilityAsync`, `IntersectsFacilityUnitAsync`, `HasGlobalScope`, `HasHeadquartersScope`, `GetFacilityRegionIdAsync`) |
| Nested ternary in `NoteDetailPage` attachment download UI | nested `?:` | `AttachmentAction` early-return component |

### Tests added

- Unit: `NoteAssigneeScopeIntersectionTests` (Global/HQ/Region/Facility/FacilityUnit/MultipleRegions/MultipleFacilities + null-id / missing facility cases).
- Integration: `List_notes_binds_AsParameters_filters_and_defaults` (defaults + page/pageSize/status/severity/facilityId/overdueOnly/dates/sort).
- Frontend: sensitive-redacted attachment message; quarantined/rejected hide download; Clean download invokes `downloadAttachment`.

### Local verification (this round)

| Suite | Count | Skipped |
|-------|-------|---------|
| Unit | **213** | 0 |
| Integration | **51** expected (local SQL unreachable; CI validates) | 0 |
| Frontend | **78** | 0 |

NuGet High/Critical gate: clean. Frontend typecheck/lint/test/build (Entra placeholders): OK.

**Post-fix tip SHA:** `0406606d500767f6be2efc6065b1471b25bb6985` (code fix); branch HEAD docs tip `fed16a1`.

## Rollback

1. Revert the B.1 PR / migration `PhaseB1NotesCore` Down in a **test** environment only first.
2. Do not edit prior merged migrations.
3. Frontend routes can be removed independently; API is additive.

## Docs

- [phase-b1-baseline.md](./phase-b1-baseline.md)
- [phase-b1-scope.md](./phase-b1-scope.md)
- [phase-b1-domain-model.md](./phase-b1-domain-model.md)
- [phase-b1-state-machine.md](./phase-b1-state-machine.md)
- [phase-b1-permissions-and-scope.md](./phase-b1-permissions-and-scope.md)
- [phase-b1-api-contract.md](./phase-b1-api-contract.md)
- [phase-b1-test-matrix.md](./phase-b1-test-matrix.md)
- [permissions-matrix.md](./permissions-matrix.md)
