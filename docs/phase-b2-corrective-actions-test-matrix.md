# Phase B.2.1 — Test Matrix

## Baseline

- Backend build: Passed.
- Unit baseline: 236 passed, 0 skipped.
- Integration baseline: 54 passed, 0 skipped.
- Frontend baseline: 78 passed, 0 skipped.
- Production frontend build: Passed with non-secret Entra validation values.
- npm audit: 0 High/Critical vulnerabilities.

## Added Coverage

- State machine allowed transitions through `MemberData` and representative rejected transitions.
- Reference number formatting.
- Overdue and due-soon calculation.
- Validators for create, assign, transition, completion, reopen.
- Assignment XOR and append-only status history protection.
- Note closure guard with active corrective actions.
- Note cancellation guard with active corrective actions and no mutation/history/audit side effects.
- SQL-backed create/list/detail scope isolation.
- Rejection of create under Draft note.
- Note closure succeeds after corrective actions are Completed or Cancelled.
- Critical corrective action SoD blocks all processing participants, including SystemAdministrator, and allows an independent verifier.
- Out-of-scope verifier receives 404 without completion mutation/history/audit.
- Invalid assignment targets reject disabled, soft-deleted, pending-provisioning, out-of-scope users, and archived departments.
- Concurrent corrective action reference generation produces unique `CA-########` values.
- Concurrent first assignment returns one success and one conflict with a single current assignment.
- Archive hides corrective actions and authorized restore returns them to detail/list access.
- Sensitive corrective action list redaction and authorized sensitive view audit.
- Corrective action attachment scan-state enforcement and cross-scope 404.
- Unit hardening for Critical SoD before mutation, assignment validation before ending current assignment, and non-unique `DbUpdateException` classification.
- RTL corrective actions list: loading, empty, API failure/retry, filters sent to API, pagination, sorting, row badges, linked note.
- RTL corrective action detail: permission-based buttons, transition validation, 403/404/409 handling, success transition, attachment scan states.
- RTL corrective action create/edit: validation, successful submit, and 409 edit handling.
- Note detail corrective actions section with count and add/view links.

## Current Counts

- Unit: 268 passed, 0 skipped.
- Integration: 67 passed, 0 skipped.
- Frontend: 91 passed, 0 skipped.
- SonarCloud Quality Gate: Passed on hardening commit `cf7bce28a601c19f28dc298a40d9eff7f012a235`.
- Eight targeted Sonar findings: 0 open after the hardening fixes.
- Final implementation SHA before documentation finalization: `cf7bce28a601c19f28dc298a40d9eff7f012a235`.
