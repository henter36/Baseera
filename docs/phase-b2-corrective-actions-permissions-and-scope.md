# Phase B.2.1 — Permissions and Scope

## Permissions

- `CorrectiveActions.View`
- `CorrectiveActions.ViewSensitive`
- `CorrectiveActions.Create`
- `CorrectiveActions.Update`
- `CorrectiveActions.Assign`
- `CorrectiveActions.StartWork`
- `CorrectiveActions.SubmitForVerification`
- `CorrectiveActions.VerifyCompletion`
- `CorrectiveActions.ReturnForRework`
- `CorrectiveActions.Reopen`
- `CorrectiveActions.Cancel`
- `CorrectiveActions.Archive`
- `CorrectiveActions.Restore`

Policies are centralized in `AuthPolicies`; application services check permission codes and never branch on role names.

## Scope Resolution

`ICorrectiveActionScopeService` resolves access through the parent note:

1. Load the corrective action.
2. Load its non-deleted parent `OperationalNote`.
3. Delegate organizational access to note scope rules.
4. Return 404 for missing or out-of-scope records.
5. Return 403 only when the record is in scope but the user lacks the required permission.

Clients never submit action `RegionId`, `FacilityId`, or `FacilityUnitId`.

## Sensitive Content

Classification uses the existing `ClassificationLevel`. Sensitive fields are redacted from list/search/counters for users without `CorrectiveActions.ViewSensitive`. Detail access to sensitive content is audited with `CorrectiveActionSensitiveViewed`.

## Critical SoD

For Critical corrective actions, any user who performed real processing transitions cannot verify completion, including `SystemAdministrator`. Processing is inferred from `CorrectiveActionStatusHistory` for:

- `Assigned -> InProgress`
- `Reopened -> InProgress`
- `InProgress -> PendingVerification`

Creation, submission, assignment, reassignment, return for rework, cancellation, reopening, and view do not count as processing.
