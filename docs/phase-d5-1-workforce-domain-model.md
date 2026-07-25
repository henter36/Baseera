# Phase D.5.1 Workforce Domain Model

Operational staffing is modeled separately from login identity. `User` remains auth/RBAC only; the operational person is `WorkforceMember` with optional `UserId?`.

Core entities:

- `WorkforceMember` — facility/org-scoped staff profile (`EmployeeNumber` unique per organization, soft-delete).
- `WorkforceRoleDefinition` — operational roles (not RBAC), org-scoped by `Code`.
- `WorkforceQualification` — certifications/skills/licenses/clearances tied to a member (optional role link).
- `WorkforceAssignment` — facility/unit/role placement with effective window and primary conflict rules.
- `StaffingRequirement` — `RequiredHeadcount` + `MinimumSafeHeadcount` baseline per facility/unit/role/shift.
- `ShiftDefinition` — local start/end, `CrossesMidnight`, timezone (default `Asia/Riyadh`).
- `DutyRoster` / `DutyRosterAssignment` — draft/published duty day coverage with assignment status.
- `WorkforceAvailabilityEvent` — leave/training/restricted-duty codes (`RestrictionCodesCsv`); never medical diagnosis text.
- `CriticalPositionRequirement` — primary/alternate counts for mission-critical roles.
- `WorkforceReadinessSnapshot` — facility (and optional unit/shift/role) coverage snapshot.
- `WorkforceImportBatch` — preview/confirm summary only (no raw file payload).

Enums cover employment, role category/criticality, qualification type/status, assignment type, availability type, operational restriction codes, roster/assignment status, source type, and coverage status (`Ready` / `Attention` / `Critical` / `Unsafe` / `Unknown`).

Weapons, ammunition, payroll, Region/HQ workspaces, and biometric attendance stores are not part of this model.
