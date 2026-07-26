# Phase D.5.1 Workforce Source Of Truth

Authoritative operational person identity is `WorkforceMember` (`OrganizationId` + normalized `EmployeeNumber`). Login `User` is not the staffing record.

Presence / coverage signal precedence (`WorkforceSourceResolver`):

1. Published duty-roster assignment (strongest operational schedule signal).
2. Attendance-like availability from Import/ExternalSystem with `AvailabilityType.Available`.
3. Other availability events.
4. Active `WorkforceAssignment` (planning only — assignment ≠ present).
5. Unknown.

Denominator authority:

- Requirement baseline = active `StaffingRequirement` windows.
- Present counts come from published roster assignment statuses (`Present` / `Confirmed` / `Late`), not from assignment alone.
- Operational eligibility = `IsOperational` and employment `Active` or `SecondedIn` (`WorkforceReadinessPolicy`).
- Blocking availability removes the member from operational availability without treating assignment as presence.

Latest timestamp alone is not authority. Stale `LastVerifiedAtUtc` (default 30 days) and missing fields lower freshness/confidence and surface in Data Quality.
