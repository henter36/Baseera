# Phase D.5.1 Workforce Duty Roster

Shifts are `ShiftDefinition` per facility (`Code` unique when not deleted). Local `StartLocalTime` / `EndLocalTime` with `CrossesMidnight` enforced by check constraint. Midnight-crossing membership uses `WorkforceReadinessPolicy.IsWithinShiftWindow` (`localTime >= start || localTime < end`).

Rosters:

- `DutyRoster` binds facility (optional unit) + shift + `DutyDate`.
- Status `Draft` or `Published` (`DutyRosterStatuses`). Published requires `PublishedAtUtc`; draft forbids publish metadata.
- Unique filtered indexes: one roster per facility/shift/date (facility-level and unit-level variants).
- Published rosters cannot receive new assignments; publish is idempotent and refreshes readiness summary.

Assignments:

- `DutyRosterAssignment` links member + role + `RosterAssignmentStatus` (Planned → Completed/Cancelled, including Present/Late/Absent/Replaced).
- Optional `ReplacementForAssignmentId` for relief coverage.
- List endpoint returns latest 50 rosters for the facility.

Permissions: view via `Workforce.ViewCoverage`; create/assign/publish via `Workforce.ManageRosters`.
