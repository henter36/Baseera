# Phase C.1 — Forms Governance Test Matrix

## Unit tests (`Baseera.UnitTests/Forms/`)

| Area | File | Coverage |
|------|------|----------|
| State machine | `FormDefinitionStateMachineTests` | All valid/invalid transitions, editable/terminal flags |
| SoD | `FormSeparationOfDutiesServiceTests` | Creator/reviewer/approver rules, policy overrides, admin override |
| Retention | `FormRetentionPolicyServiceTests` | Expiry computation, archive eligibility, hard-delete rejection |
| Grants | `FormGrantResolverTests` | Deny precedence, expired grants, capability resolution |
| Scope | `FormScopeServiceTests` | Global/region/facility/unit reachability, filter behavior |

Baseline unit count: 348 → **427** (+79 Forms tests).

## Integration tests (`FormsCoreIntegrationTests.cs`)

Requires `BASEERA_TEST_CONNECTION` + reachable SQL Server. **21 tests**, 0 skipped when connection available.

| Scenario | Expected |
|----------|----------|
| Region/facility/classification isolation | 404 for cross-scope detail; list excludes foreign rows |
| Draft CRUD | Create, update, get, list |
| Full review workflow | submit → request-changes → resubmit → approve |
| Reject rework path | Rejected → update → resubmit |
| Archive / restore | Soft archive, restore to Approved |
| 400 validation | Missing required fields |
| 403 forbidden | Missing permission in scope |
| 404 scope miss | IDOR attempts |
| 409 conflict | RowVersion mismatch, invalid transition |
| Grants | Allow/deny create, revoke (soft delete), expiry |
| SoD enforcement | Self-review/self-approve blocked |
| Audit | Events created; review decisions append-only |
| Migration | `PhaseC1FormsGovernanceCore` applies |
| Filtered unique code | Reuse code after soft delete |
| Retention status | GET retention-status returns computed fields |
| No hard delete | No purge API |

Baseline integration count: 92 → **113** (+21 Forms tests).

## Frontend tests

| File | Coverage |
|------|----------|
| `FormsListPage.test.tsx` | List states, filters |
| `FormsListPage.permission.test.tsx` | Permission-gated UI |
| `formsListSearchParams.test.ts` | URL filter sync |
| `FormCreatePage.test.tsx` | Create flow |
| `FormEditPage.test.tsx` | Edit/conflict |
| `FormDetailPage.test.tsx` | Detail/actions |
| `FormReviewPage.test.tsx` | Review workflow UI |
| `FormAccessPage.test.tsx` | Grants UI |
| `FormsGovernanceSettingsPage.test.tsx` | Policy settings |

Baseline frontend count: 121 → **156** (+35).

## Gates

| Gate | Command |
|------|---------|
| Backend build | `dotnet build src/backend/Baseera.slnx -c Release` |
| Unit tests | `dotnet test src/backend/tests/Baseera.UnitTests -c Release` |
| Integration | `dotnet test src/backend/tests/Baseera.IntegrationTests -c Release` |
| Frontend | `npm ci`, `typecheck`, `lint`, `test`, `build` (Entra env for build), `audit --audit-level=high` |
| Whitespace | `git diff --check` |
