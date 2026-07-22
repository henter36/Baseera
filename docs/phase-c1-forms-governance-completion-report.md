# Phase C.1 — Forms Governance Completion Report

Status: **In review — not merge-ready until all gates below are green.**

## Branch and SHAs

| Item | Value |
|------|-------|
| Branch | `phase-c1-forms-governance-foundation` |
| Base (`main` at branch start) | `66f35f3f0916e398df6a7318d7bff13b0cc85433` (PR #60) |
| Initial implementation | `c673e2f731ba7f07fff0e62b81d010651b5f5008` |
| Migration | `20260722024228_PhaseC1FormsGovernanceCore` |
| Epic / Issue | Epic #45, Issue #52 |
| PR | #61 |

## CI failure root cause (run 29834609029)

All **113** integration tests failed because `MapDelete` for grant revoke inferred a body parameter, which ASP.NET Core rejects at host startup:

`Body was inferred but the method does not allow inferred body parameters`

Fixed by replacing with `POST /api/v1/forms/{id}/access-grants/{grantId}/revoke`.

## Local gates (post-fix)

| Gate | Result |
|------|--------|
| `dotnet build -c Release` | Pass (pre-existing NU1510 / CS8619 warnings only) |
| Unit tests | **437** passed, 0 skipped |
| Integration tests | Requires reachable SQL Server (`127.0.0.1:1434` unavailable locally); **run in CI** — suite expanded with deny/restore/revoke/rowversion cases |
| Frontend typecheck / lint / test / build / audit | Pass (156 frontend tests; 0 high/critical audit) |
| `git diff --check` | Pass |
| RTL walkthrough | **Not completed** — blocked until API/SQL available for live walkthrough |
| Sonar QG | Pending re-analysis after push (S6444 regex timeout fixed; duplication reduced via shared helpers) |

## Merge gate (all required)

- [ ] Backend CI green (unit + integration Failed=0, Skipped=0)
- [ ] Frontend CI green
- [ ] Migration apply green
- [ ] Sonar Quality Gate green (Security Rating A, new duplication ≤ 3%)
- [ ] All review threads resolved
- [ ] RTL walkthrough completed
- [ ] Product acceptance

## Key fixes in follow-up commits

- Restore prior status from latest Archive decision; soft-delete clear on restore
- Mixed Facility + FacilityUnit scope (`FullFacilityIds` vs `UnitIds`)
- Form-specific Deny on write paths via `IFormEffectiveAccessService`
- Grant revoke POST route; IncludingDeleted lookup; historical principal names
- RowVersion Base64 validation (400 vs 409)
- Unique grant index with `ScopeKey`; retention CHECK constraints + cross-field validation
- SoD unit test exercises RequestChanges rule correctly

## Explicit non-starts

Form field designer / publish / respond / export (Issues #46–#51).

## Decision

**Do not merge** until CI integration, Sonar, RTL walkthrough, and product acceptance are complete.
