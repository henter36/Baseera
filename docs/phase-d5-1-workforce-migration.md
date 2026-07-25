# Phase D.5.1 Workforce Migration

Migration: `20260725180933_PhaseD51FacilityWorkforceReadiness` (`PhaseD51FacilityWorkforceReadiness`).

Creates:

- `ShiftDefinitions`
- `WorkforceImportBatches`
- `WorkforceMembers`
- `WorkforceRoleDefinitions`
- `DutyRosters`
- `WorkforceAvailabilityEvents`
- `CriticalPositionRequirements`
- `StaffingRequirements`
- `WorkforceAssignments`
- `WorkforceQualifications`
- `WorkforceReadinessSnapshots`
- `DutyRosterAssignments`

Integrity highlights:

- Soft-delete + `RowVersion` on mutable tables; restrict FK deletes.
- Check constraints: midnight-crossing shifts, import batch counts/state/status, roster publish state, availability/assignment/requirement effective ranges, staffing quantity (`MinimumSafe ≤ Required`), no self-supervision, unit requires facility.
- Filtered unique indexes for employee numbers, external personnel ids, role codes, shift codes, roster day uniqueness, import idempotency.
- Operational indexes for facility coverage and snapshot lookup.

`Down` drops these workforce tables only. Does not create weapons, payroll, or Region/HQ tables.
