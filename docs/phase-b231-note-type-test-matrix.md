# Phase B.2.3.1 Test Matrix

Baseline:

- Backend build: Passed.
- Unit: 284 passed, 0 skipped.
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
- FacilityUnit eligibility isolation blocks sibling unit leakage while allowing facility-wide notes.
- Eligible assignee resolution uses batched scope and note type access lookups.
- Role grant and user override replace requests reject duplicate note type IDs.
- Existing intake profile updates require RowVersion.
- Note create form waits for intake context before loading facilities and formats default due dates as local `datetime-local` values.
- B.2.3.1 migration duplication was reduced by consolidating generated table creation SQL in the current migration only.

Skipped tests: 0.
