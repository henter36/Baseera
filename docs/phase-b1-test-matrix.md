# Phase B.1 — Test Matrix

## Baseline (pre-B.1 @ `5dc0b5b`)

| Suite | Passed | Skipped |
|-------|--------|---------|
| Unit | 71 | 0 |
| Integration | 21 | 0 |
| Frontend | 16 | 0 |

## After B.1 merge (`bda6bdd`) + Final Acceptance SoD hardening

| Suite | Passed | Skipped |
|-------|--------|---------|
| Unit | 236 | 0 |
| Integration | 54 | 0 |
| Frontend | 78 | 0 |

(Pre-hardening tip on merge: Unit 228 / Integration 52 / Frontend 78.)

## Unit coverage (representative)

| Area | Coverage |
|------|----------|
| State machine | Allowed + rejected transitions |
| Due date / overdue | Validation + computed overdue |
| Scope shape | Global/HQ/Region/Facility/Unit |
| Reference format | `OBS-########` |
| Assignment XOR + reassignment | Target rules, history retained |
| Critical SoD (all processors) | start-work / submit / earlier processor / reopen-start / SystemAdmin / multi-user A→B→C |
| Critical SoD mutation order | Rejected before note/assignment/history/audit mutation |
| Non-critical policy | Processor may verify when severity ≠ Critical |
| Sensitive redaction | List redacts, does not hide |
| Validators | FluentValidation rules |
| Soft-deleted Role | UserRoles / RolePermissions / PrivilegeGuard |

## Integration coverage (representative)

| Scenario | Expected |
|----------|----------|
| Global / HQ isolation | HQ ≠ Global |
| Region / Facility / Unit isolation | No cross-scope list/detail leakage |
| Cross-scope update / create | 404 |
| Assign out-of-scope / disabled / deleted / PendingProvisioning | Rejected |
| Concurrent reference generation | Unique OBS numbers |
| Concurrent update / concurrent first assign | 409 |
| Invalid transition | 409 |
| Critical SoD multi-processor | A and B blocked; independent C closes |
| Critical SoD out-of-scope verifier | 404 |
| Audit + status history atomic | Same transaction |
| Status history append-only | No update/delete path |
| Soft-delete + restore | Hidden / restored with permission |
| Sensitive list + audit | Redacted + sensitive view audited |
| Attachment nonexistent / cross-scope | 404 |
| PendingScan download blocked; Clean allowed | Enforced |
| Pagination/sort scoped | Stable within scope |

## Frontend coverage (representative)

Empty / loading / error+retry, filters to API (incl. classification), pagination, create Zod validation, cascading scope fields, permission buttons, transition confirm, 409 reload, 404 out-of-scope, sensitive `[محجوب]`, StrictMode double-fetch safety, production TestAuth guard unchanged.

## CI gates (required)

- `dotnet build` Release
- Unit + Integration (with `BASEERA_TEST_CONNECTION`)
- `npm ci --ignore-scripts` + typecheck + lint + test + build
- `npm audit --audit-level=high`
- NuGet vulnerability gate (fail-closed self-test)
- Gitleaks full history
- SonarCloud Quality Gate / Qlty
