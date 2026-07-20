# Phase B.2.1 — Test Matrix

## Baseline

- Backend build: Passed.
- Unit baseline: 236 passed, 0 skipped.
- Integration baseline: 54 passed, 0 skipped.
- Frontend baseline: 78 passed, 0 skipped.
- Production frontend build: Passed with non-secret Entra validation values.
- npm audit: 0 High/Critical vulnerabilities.

## Added Coverage

- State machine allowed and rejected transitions.
- Reference number formatting.
- Overdue and due-soon calculation.
- Validators for create, assign, transition, completion, reopen.
- Assignment XOR and append-only status history protection.
- Note closure guard with active corrective actions.
- SQL-backed create/list/detail scope isolation.
- Rejection of create under Draft note.
- Note closure succeeds after corrective actions are Completed or Cancelled.
- RTL corrective actions list: loading, empty, API failure/retry, filters sent to API, pagination, sorting, row badges, linked note.
- Note detail corrective actions section with count and add/view links.

## Remaining Coverage to Expand

The initial vertical slice includes representative backend and frontend paths. Additional PR hardening should broaden integration coverage for all listed out-of-scope assignment cases, concurrent assignment/reference generation, full attachment download matrix, and Critical multi-user SoD scenarios.
