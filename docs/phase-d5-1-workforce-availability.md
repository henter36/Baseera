# Phase D.5.1 Workforce Availability

Availability is recorded as append-style soft-deletable `WorkforceAvailabilityEvent` rows on a member.

Fields:

- `AvailabilityType` — Available, leave types, Training, External/InternalAssignment, Suspended, RestrictedDuty, EmergencyLeave, UnexcusedAbsence, Other.
- Effective window `StartsAtUtc` / optional `EndsAtUtc` (`Ends > Starts` when set).
- `AffectsOperationalAvailability` (default true).
- `ReasonCode` (operational code, not free-text diagnosis).
- `RestrictionCodesCsv` — comma-separated `OperationalRestrictionCode` names when RestrictedDuty (`CannotDrive`, `CannotCarryWeapon`, `CannotWorkNightShift`, `CannotPerformEscort`, `AdministrativeDutyOnly`).
- `SourceType` / `SourceReference`, `RecordedAtUtc` / `RecordedBy`.

API: `POST .../workforce/availability` requires `Workforce.RecordAvailability`. Member detail returns availability; `RestrictionCodes` are omitted unless the caller has `Workforce.ViewSensitiveRestrictions`.

Medical diagnosis text, raw biometric attendance streams, and payroll leave balances are out of scope.
