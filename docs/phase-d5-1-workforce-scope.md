# Phase D.5.1 Workforce Readiness Scope

Phase D.5.1 partially implements Issue #15, continues Issue #11, and closes Issue #133.

It introduces the Facility Workforce Readiness & Duty Coverage Center: operational staffing profiles, role definitions, qualifications, assignments, staffing requirements, shifts, duty rosters, availability events, critical positions, readiness/coverage calculations, bounded import, and Facility Workspace integration.

## Included

- `WorkforceMember` independent of login `User`
- Operational roles independent of RBAC roles
- Qualifications and certifications (operational)
- Assignments (facility / unit / role) with conflict rules
- Staffing requirements (`RequiredHeadcount` + `MinimumSafeHeadcount`)
- Shift definitions and duty rosters (including midnight-crossing)
- Availability events (leave, training, restricted duty codes — no medical diagnosis)
- Critical position requirements and alternates
- Readiness / coverage projections and deterministic fatigue/coverage-risk indicators
- Import preview/confirm with idempotency
- Facility Workspace section `القوى البشرية والتغطية`
- Context Panel, Intervention Queue, Action Center, Timeline, Data Quality
- Admin page `/facilities/:facilityId/workforce`
- Permissions, audit, migration, tests, and docs

## Excluded

- Payroll, allowances, promotions, full performance reviews
- Disciplinary records, detailed medical files, raw biometric attendance
- Recruitment, retirement, full institutional training management
- Weapons and ammunition
- Region Workspace / Headquarters Workspace
- AI, prediction, automated shift optimization

## Permission rule

`Workspaces.ViewFacility` is not sufficient for workforce data. Each workforce widget, section, and endpoint requires `Workforce.*` domain permissions.

## Issue links

- Partially implements #15 (Integrated Resource Center — workforce slice; weapons remain open)
- Continues #11 (Facility Workspace)
- Closes #133 (this slice)
