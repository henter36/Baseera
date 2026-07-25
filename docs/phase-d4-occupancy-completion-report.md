# Phase D.4 Occupancy Completion Report

Status: implementation and local verification complete on branch `phase-d4-facility-occupancy-inmate-movement`; remote CI, SonarCloud, CodeRabbit review, and PR acceptance remain pending after push.

Implemented:
- Real occupancy domain entities.
- EF configuration and migration.
- Occupancy permissions and role grants.
- Query and command services.
- Occupancy workspace widget.
- Facility Workspace occupancy section and unit context panel.
- Occupancy management route.
- Documentation set and current-state analysis.
- Development-only demo occupancy seed under the existing safe demo seeding path; production paths do not use mock data.

Deferred:
- Full reconciliation workflow.
- External inmate-system connector.
- Sensitive movement-detail screens.
- Region/HQ rollups.
- Non-capacity resource center.
- Full Issue #11 closure; remaining operational domains continue through the already documented follow-up issues.

Issue status:
- Implements #124.
- Continues #11.
- Touches #15 only for capacity.

Local verification:
- Backend build: passed in Release.
- Unit tests: 706 passed, 0 failed, 0 skipped; targeted occupancy tests: 5 passed, 0 failed, 0 skipped.
- Integration tests: 156 passed, 0 failed, 0 skipped with `BASEERA_TEST_CONNECTION`.
- Frontend typecheck: passed.
- Frontend lint: passed with existing warnings outside Phase D.4.
- Frontend tests: 252 passed, 0 failed.
- Frontend build: passed with production-safe placeholder Entra values.
- `npm audit --audit-level=high`: 0 vulnerabilities.
- NuGet vulnerability gate: no High/Critical vulnerabilities.
- `git diff --check`: passed before final commit.

Migration:
- One migration was added: `20260724202429_PhaseD4OccupancyInmateMovement`.
- The migration creates capacity baselines, census snapshots, and inmate movement events with scoped indexes, constraints, rowversion, and soft-delete filtering.

Privacy review:
- Workspace summaries, pulse, intervention queue, and timeline do not expose inmate identity.
- Movement imports store only a masked/hash reference and external idempotency key.
- Sensitive movement detail permissions are defined but not surfaced in the general Facility Workspace.

Screenshots:
- Actual PNG screenshots are stored in `docs/screenshots/phase-d4/`.
- Captured states include desktop occupancy, unit panel, movement/admin view, data quality states, tablet, and mobile views.

Known residual gaps:
- Local screenshot capture depended on the available development database state; over-capacity data is supported by the domain and demo seed, while existing local DB state may still display explicit missing-data quality states until reseeded.
- Gitleaks was not available locally in this environment; the GitHub workflow remains the gate for the repository secret scan.
