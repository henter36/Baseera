# Phase B.2.1 — State Machine

All transitions are centralized in `CorrectiveActionStateMachine` and use `RowVersion`. Invalid transitions return HTTP 409.

| From | To | Permission | Reason | Handler | Audit event | SoD |
|------|----|------------|--------|---------|-------------|-----|
| Draft | Open | `CorrectiveActions.Create` | Required | Command submit | `CorrectiveActionSubmitted` | No |
| Draft | Cancelled | `CorrectiveActions.Cancel` | Required | Workflow cancel | `CorrectiveActionCancelled` | No |
| Open | Assigned | `CorrectiveActions.Assign` | Required | Assignment service | `CorrectiveActionAssigned` | No |
| Open | Cancelled | `CorrectiveActions.Cancel` | Required | Workflow cancel | `CorrectiveActionCancelled` | No |
| Assigned | InProgress | `CorrectiveActions.StartWork` | Required | Workflow start | `CorrectiveActionWorkStarted` | Processing participant |
| Assigned | Assigned | `CorrectiveActions.Assign` | Required | Assignment service | `CorrectiveActionReassigned` | No |
| Assigned | Cancelled | `CorrectiveActions.Cancel` | Required | Workflow cancel | `CorrectiveActionCancelled` | No |
| InProgress | PendingVerification | `CorrectiveActions.SubmitForVerification` | Required | Workflow submit for verification | `CorrectiveActionSubmittedForVerification` | Processing participant |
| InProgress | Cancelled | `CorrectiveActions.Cancel` | Required | Workflow cancel | `CorrectiveActionCancelled` | No |
| PendingVerification | Completed | `CorrectiveActions.VerifyCompletion` | Required | Workflow verify | `CorrectiveActionCompleted` | Critical verifier must be independent |
| PendingVerification | InProgress | `CorrectiveActions.ReturnForRework` | Required | Workflow return | `CorrectiveActionReturnedForRework` | No |
| PendingVerification | Cancelled | `CorrectiveActions.Cancel` | Required | Workflow cancel | `CorrectiveActionCancelled` | No |
| Completed | Reopened | `CorrectiveActions.Reopen` | Required | Workflow reopen | `CorrectiveActionReopened` | No |
| Reopened | Assigned | `CorrectiveActions.Assign` | Required | Assignment service | `CorrectiveActionAssigned` | No |
| Reopened | InProgress | `CorrectiveActions.StartWork` | Required | Workflow start | `CorrectiveActionWorkStarted` | Processing participant |
| Reopened | Cancelled | `CorrectiveActions.Cancel` | Required | Workflow cancel | `CorrectiveActionCancelled` | No |

Rejected examples: Open/Assigned/InProgress directly to Completed, reopening non-Completed actions, mutating Completed/Cancelled through the general update endpoint, and any transition not listed above.
