# Phase B.2.3.1 Scope

Phase B.2.3.1 makes operational note types data-driven and adds effective type access and intake locking.

Included:

- `NoteType` replaces runtime use of fixed `NoteCategory`.
- Role-level note type grants.
- User-level direct allow/deny overrides.
- Effective access calculation for note type capabilities.
- User note intake profile with optional region/facility lock.
- Create-note flow starts with region, facility, then note type.
- Eligible assignee/reviewer endpoints.
- Corrective action access derives note type access from the parent note.

Excluded:

- Auto routing.
- Dashboard.
- Reports/export.
- Phase B.2.3.2 implementation.
- Phase C.

