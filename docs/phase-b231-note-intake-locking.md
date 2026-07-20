# Phase B.2.3.1 Note Intake Locking

`UserNoteIntakeProfile` controls where a user can enter new notes. It does not grant viewing scope and does not replace `UserScope`.

Lock behavior:

- `None`: user selects a region from scoped regions, then a facility in that region.
- `Region`: region is fixed; user selects a facility in that region.
- `Facility`: region is derived server-side from the fixed facility; region and facility are locked.

Server rules:

- The selected facility must belong to the selected region.
- The selected location must be inside the user scope.
- Invalid intake profile blocks creation with a clear validation error.
- New notes from the UI path are created as `ScopeType = Facility`.

