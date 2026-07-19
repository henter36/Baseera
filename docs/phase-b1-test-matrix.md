# Phase B.1 — Test Matrix

## Baseline (pre-B.1 @ `5dc0b5b`)

| Suite | Passed | Skipped |
|-------|--------|---------|
| Unit | 71 | 0 |
| Integration | 21 | 0 |
| Frontend | 16 | 0 |

## After B.1 (local validation)

| Suite | Passed | Skipped |
|-------|--------|---------|
| Unit | 201 | 0 |
| Integration | 49 | 0 |
| Frontend | 76 | 0 |

## Unit coverage (representative)

| Area | Coverage |
|------|----------|
| State machine | Allowed + rejected transitions |
| Due date / overdue | Validation + computed overdue |
| Scope shape | Global/HQ/Region/Facility/Unit |
| Reference format | `OBS-########` |
| Assignment XOR + reassignment | Target rules, history retained |
| Critical SoD | Processor cannot verify; other verifier ok |
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
| Concurrent update | 409 |
| Invalid transition | 409 |
| Critical SoD | Processor blocked; other verifier ok |
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
