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

## Frontend

- `FacilityWorkspacePage.test.tsx` — section `workforce` / label `القوى البشرية والتغطية`, widget permission gating.
- `FacilityWorkforcePage.test.tsx` — admin page permission gates and section rendering.

## Manual / screenshots

- Desktop workspace workforce section, admin overview/members/import, partial gap strip, mobile overview — see `docs/screenshots/phase-d5-1/README.md`.
