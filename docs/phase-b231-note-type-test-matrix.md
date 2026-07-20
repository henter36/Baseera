# Phase B.2.3.1 Test Matrix

Baseline:

- Backend build: Passed.
- Unit: 278 passed, 0 skipped.
- Integration: 73 passed, 0 skipped.
- Frontend: 98 passed, 0 skipped.
- Production frontend build: Passed with non-secret Entra test values.
- npm audit: 0 high/critical vulnerabilities.

Coverage added or updated:

- Dynamic note type validation.
- Create note region/facility/type ordering.
- Facility-only create path.
- Corrective action access through parent note type.
- Requires-my-action list filter with `CanProcess`.
- NoteCategory to NoteType migration backfill.
- Integration helpers updated to use the B.2.3.1 intake shape.

Skipped tests: 0.

