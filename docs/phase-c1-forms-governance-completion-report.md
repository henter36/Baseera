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
| Tip SHA | `b3cffd2e6ea7bdb5bd084a8e1ebfb17a0de4384c` |

## CI failure root cause (run 29834609029)

All **113** integration tests failed because `MapDelete` for grant revoke inferred a body parameter, which ASP.NET Core rejects at host startup:

`Body was inferred but the method does not allow inferred body parameters`

Fixed by replacing with `POST /api/v1/forms/{id}/access-grants/{grantId}/revoke`.

## Local gates (post-fix)

| Gate | Result |
|------|--------|
| `dotnet build -c Release` | Pass (pre-existing NU1510 / CS8619 warnings only) |
| Unit tests | **437** passed, 0 skipped |
| Integration tests | **116** passed, Failed=0, Skipped=0 on CI run `29888273188` |
| Frontend typecheck / lint / test / build / audit | Pass (156 frontend tests; 0 high/critical audit) |
| `git diff --check` | Pass |
| RTL walkthrough | **Not completed** — blocked until API/SQL available for live walkthrough |
| Sonar QG | **Pass** — Security Rating A; new duplication ≈ 0.38% |

## Merge gate (all required)

- [x] Backend CI green (unit + integration Failed=0, Skipped=0)
- [x] Frontend CI green
- [x] Migration apply green (CI SQL Server)
- [x] Sonar Quality Gate green (Security Rating A, new duplication ≈ 0.38%)
- [x] All review threads resolved (0 open)
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

**Do not merge** until RTL walkthrough and product acceptance are complete. CI/Sonar are green; RTL remains outstanding.
