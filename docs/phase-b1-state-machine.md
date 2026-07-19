# Phase B.1 — State Machine

Central implementation: `NoteStateMachine` + `INoteWorkflowService` / `INoteAssignmentService` / `INoteCommandService`.

`Overdue` is **not** a stored status. It is computed: `DueAtUtc < UtcNow` and status ∉ {Closed, Cancelled}.

## Allowed transitions

| From | To | Endpoint | Permission | Reason required | Actor notes | Audit event |
|------|-----|----------|------------|-----------------|-------------|-------------|
| Draft | Open | `POST .../submit` | `Notes.Update` | optional | reporter/editor in scope | `NoteSubmitted` |
| Draft | Cancelled | `POST .../cancel` | `Notes.Cancel` | **yes** | in scope | `NoteCancelled` |
| Open | Assigned | `POST .../assign` | `Notes.Assign` | **yes** | assignee must intersect note scope | `NoteAssigned` |
| Open | Cancelled | `POST .../cancel` | `Notes.Cancel` | **yes** | in scope | `NoteCancelled` |
| Assigned | InProgress | `POST .../start-work` | `Notes.StartWork` | optional | sets LastProcessedByUserId | `NoteWorkStarted` |
| Assigned | Assigned | `POST .../assign` (reassign) | `Notes.Assign` | **yes** | ends prior assignment; history kept | `NoteReassigned` |
| InProgress | PendingVerification | `POST .../submit-for-verification` | `Notes.SubmitForVerification` | optional | sets LastProcessedByUserId | `NoteSubmittedForVerification` |
| PendingVerification | Closed | `POST .../verify-closure` | `Notes.VerifyClosure` | **yes** + ClosureSummary | **Critical SoD** | `NoteClosed` |
| PendingVerification | InProgress | `POST .../return-for-rework` | `Notes.ReturnForRework` | **yes** | in scope | `NoteReturnedForRework` |
| Closed | Reopened | `POST .../reopen` | `Notes.Reopen` | **yes** | in scope | `NoteReopened` |
| Reopened | Assigned | `POST .../assign` | `Notes.Assign` | **yes** | new assignment | `NoteAssigned` / `NoteReassigned` |
| Reopened | InProgress | `POST .../start-work` | `Notes.StartWork` | optional | if current assignment still valid | `NoteWorkStarted` |

All transitions require matching `RowVersion` and write `NoteStatusHistory` + `AuditLog` in the same unit of work.

## Rejected examples

- Close from Open / Assigned / InProgress (must go through PendingVerification)
- Reopen anything other than Closed
- Mutate Cancelled/Closed via generic Update
- Cancel a Closed note
- Any From→To pair not in the table above → HTTP 409

## Critical SoD

For `Severity = Critical`:

- Any user who performed **actual processing** on the note cannot execute `verify-closure`
- Processing is identified from append-only `NoteStatusHistory` (not `LastProcessedByUserId` alone):
  - `Assigned → InProgress` / `Reopened → InProgress` (`start-work`)
  - `InProgress → PendingVerification` (`submit-for-verification`)
- `PendingVerification → InProgress` (`return-for-rework`) is **not** treated as processing
- Verifier needs `Notes.VerifyClosure` **and** organizational scope
- Out-of-scope verifier → 404 (anti-enumeration)
- **SystemAdministrator does not bypass SoD**
- SoD runs **before** any note/assignment/history/audit mutation

Non-critical notes: same transition path; SoD check does not block same-user closure.

## Archive / Restore (not status transitions)

| Action | Endpoint | Permission | Audit |
|--------|----------|------------|-------|
| Soft-delete | `POST .../archive` | `Notes.Archive` | `NoteArchived` |
| Restore | `POST .../restore` | `Notes.Restore` | `NoteRestored` |
