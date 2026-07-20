# Phase B.2.3.1 Domain Model

New tables:

- `NoteTypes`
- `RoleNoteTypeGrants`
- `UserNoteTypeOverrides`
- `UserNoteIntakeProfiles`

`OperationalNotes.Category` is migrated to `OperationalNotes.NoteTypeId`.

Constraints:

- `NoteTypes.Code` is unique and immutable through application services.
- `RoleId + NoteTypeId` is unique.
- `UserId + NoteTypeId` is unique.
- One active intake profile row per user.
- Intake lock constraints enforce `None`, `Region`, and `Facility` shapes.
- Foreign keys use restrict delete.
- Mutable records use `RowVersion`.

Backfill:

- `Security` -> `SECURITY`
- `Technical` -> `TECHNICAL`
- `Operational` -> `OPERATIONAL`
- `HealthAndSafety` -> `HEALTH_SAFETY`
- `Administrative` -> `ADMINISTRATIVE`
- `Other` -> `OTHER`

