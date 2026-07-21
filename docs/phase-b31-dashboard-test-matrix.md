# Phase B.3.1 — Dashboard Test Matrix

## Unit (`OperationalDashboardQueryServiceTests`)

- KPI counts: open, assigned, overdue, unassigned, requiresRouting, dueSoon
- Period: from > to throws; range > 90 throws; 90-day boundary OK
- Riyadh: `StartOfSaudiDayUtc` boundary
- Scope mocks and region isolation via `NoteScopeService`
- Type access mock filtering
- Sensitive exclusion (not redaction)
- Soft-deleted notes excluded
- Null DueAt, non-current assignment, permission-gated sections

## Integration (`OperationalDashboardIntegrationTests`)

- Summary overdue = notes list overdueOnly totalCount
- Adjacent region isolation (summary + queue)
- FacilityUnit isolation (summary + queue)
- Sensitive notes excluded without ViewSensitive
- Note type deny override excluded
- Priority queue length ≤ 10
- 403 without dashboard permissions (summary/trends/breakdowns/queues)
- Readonly dashboard user shape

## Frontend

- `OperationalDashboardPage.test.tsx`: KPI render, filters, drill-down href, loading/error/empty
- `OperationalDashboardPage.permission.test.tsx`: denial alert
- `dashboardDrillDown.test.ts`: URL builder

## Gates

- `dotnet build` Release
- Unit + integration (SQL via `BASEERA_TEST_CONNECTION`)
- `npm run typecheck`, `lint`, `test`, `build`, `npm audit --audit-level=high`
- `git diff --check`
