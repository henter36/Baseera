# Phase B.1 — Completion Report

**Branch:** `phase-b1-notes-core`  
**Base merge:** `5dc0b5b8efab8214c8a38cad839f8964f9f59f08` (PR #2 → `main`)  
**Migration:** `20260719103156_PhaseB1NotesCore`  
**Decision (proposed):** Phase B.1 Conditionally Accepted — pending PR review, Sonar/Qlty CI on the PR, and explicit human acceptance. **Do not merge until approved.**

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

## Residual risks

1. Malware scanner remains stubbed (PendingScan still blocks download — intentional for B.1).
2. NU1510 warning remains while the High/Critical pin is required.
3. SonarCloud / Qlty / CodeRabbit results must be confirmed on the PR CI run.
4. Restore UX requires knowing `rowVersion` for archived notes (no “include deleted” list API yet).

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
