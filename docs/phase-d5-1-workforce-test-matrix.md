# Phase D.5.1 Workforce Test Matrix

## Unit (`Baseera.UnitTests/Workforce`)

- Coverage calculation, unknown baseline, status thresholds.
- Employment eligibility; availability blocking.
- Qualification validity (role mismatch, expiry, status).
- Midnight-crossing shift window.
- Freshness / confidence resolution.
- Access policy: summary/members/sensitive permission checks.
- Import batch state and count invariants.
- Employee-number normalize; unique-violation detection for members and import batches.
- Fatigue indicators (overtime, consecutive shifts, single point of failure, expiring quals).
- Assignment primary-period overlap conflicts.
- Source resolver precedence (published roster > import attendance > availability > assignment).

## Integration (`WorkforceReadinessIntegrationTests`, requires `BASEERA_TEST_CONNECTION`)

- Missing permission → 403 on summary.
- Out-of-scope facility → 404.
- Summary redacts member names (aggregate only).
- Facility workspace includes `facility.workforce` widget when domain permission exists.
- Member create is facility-scoped and audited (`WorkforceMemberCreated`).
- Import confirm idempotency (single member + single batch for same keys).
- Query-count budget for the Facility Workspace workforce slice.
- Payload-size budget for the Facility Workspace workforce slice.
- Bounded large-workforce member list with `pageSize` clamp.
- Reconciliation list/resolve and critical-position endpoints.
- Data-quality endpoint returns the stable catalog codes.

## Frontend

- `FacilityWorkspacePage.test.tsx` — section `workforce` / label `القوى البشرية والتغطية`, widget permission gating.
- `FacilityWorkforcePage.test.tsx` — admin page permission gates and section rendering.
- Action Center API-backed roster publish path with success/error states.

## Manual

- Manual review is limited to interaction semantics that are not already covered by DOM tests. Screenshots and PNG comparison are not acceptance evidence for Phase D.5.1 hardening.
