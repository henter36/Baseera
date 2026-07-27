# Sensitive Custody State Machine

Weapon terminal statuses: `Retired`, `Destroyed`.

Restricted statuses: `Missing`, `UnderInvestigation`, `Quarantined`, `UnderMaintenance`.

Custody transaction lifecycle:

`Draft` -> `PendingApproval` -> `Approved` -> `HandedOver` -> `Received` -> `Completed`

`Rejected`, `Cancelled`, and `Reversed` are non-happy-path statuses. Sensitive approval enforces four-eyes: creator and approver cannot be the same actor.

Completion updates current custody and weapon status. Draft and pending states do not change current custody.
