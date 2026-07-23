# Phase C.5 — Metric Catalog

Counting unit: `(CycleId, FacilityId)`.

`TargetedAssignmentCount`: count of targeted cycle/facility assignment rows from the assignment snapshot.

`DistinctFacilityCount`: distinct `FacilityId` count across the filtered scope and period. It is displayed separately and is not used as the denominator for multi-cycle compliance.

`UnavailableAssignmentCount`: targeted assignments where `FormFacilityAssignment.IsAvailable = false`.

`EligibleAssignmentCount`: `TargetedAssignmentCount - UnavailableAssignmentCount`. Compliance percentages use this denominator.

`CompletedCount`: eligible assignments whose response status satisfies the campaign completion basis. Submitted basis counts `Submitted`, `UnderReview`, `Approved`, `Closed`. Approved basis counts `Approved`, `Closed`.

`RemainingCount`: `EligibleAssignmentCount - CompletedCount`.

`CompletionRate`: `CompletedCount / EligibleAssignmentCount * 100`. If the denominator is zero, the API returns `null` and the UI shows `—`.

Status buckets are mutually exclusive and reconcile to `EligibleAssignmentCount`: `NotStarted`, `Draft`, `Submitted`, `UnderReview`, `Returned`, `Approved`, `Rejected`, `Closed`.

`OverdueCount`: overlapping flag, not a status bucket. A row is overdue when it is not completed and `nowUtc > (Response.DueAtUtcOverride ?? Cycle.DueAtUtc)`.

`CompletionAtUtc`: `SubmittedAtUtc` for Submitted basis, `ApprovedAtUtc` for Approved basis. `ClosedAtUtc` is never used as a fallback.

`CompletedOnTimeCount`: completed rows with known completion timestamp at or before effective due.

`CompletedLateCount`: completed rows with known completion timestamp after effective due.

`UnknownCompletionTimestampCount`: completed rows missing the basis-specific completion timestamp.

`AverageCompletionMinutes`: average of `CompletionAtUtc - Cycle.OpenAtUtc` for completed rows with known timestamp and non-negative duration. Missing and invalid timestamps are excluded.

`InvalidCompletionDurationCount`: completed rows whose completion timestamp is earlier than `Cycle.OpenAtUtc`.
