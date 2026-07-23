# Phase C.5: Form compliance dashboard and completion indicators

Closes #49
Related to #45
Depends on merged #47
Depends on merged #48
Depends on merged #86

## Summary

Adds a scoped form compliance dashboard over cycle/facility assignments, using `FormFacilityAssignment` as the denominator source and left-joining `FormResponse` so not-started assignments are included.

## Metrics

- Targeted: count of `(CycleId, FacilityId)` assignments.
- Distinct facilities: distinct `FacilityId`, displayed separately.
- Eligible: targeted minus unavailable.
- Completion: Submitted basis counts Submitted, UnderReview, Approved, Closed. Approved basis counts Approved and Closed.
- Overdue: overlapping flag for incomplete rows past effective due.
- Completion timestamp: Submitted basis uses `SubmittedAtUtc`; Approved basis uses `ApprovedAtUtc`; no `ClosedAtUtc` fallback.
- Average completion: average non-negative `CompletionAtUtc - Cycle.OpenAtUtc`, excluding missing and invalid timestamps.
- Status reconciliation: API returns `StatusBucketTotal` and `StatusReconciliationValid`.

## API

- `/api/v1/form-compliance/summary`
- `/api/v1/form-compliance/regions`
- `/api/v1/form-compliance/facilities`
- `/api/v1/form-compliance/cycles`
- `/api/v1/form-compliance/pending`
- `/api/v1/form-compliance/trend`
- `/api/v1/form-compliance/export.csv`

## UI

- `/form-compliance`
- `/form-compliance/regions/:regionId`
- `/form-compliance/facilities/:facilityId`
- `/form-compliance/cycles/:cycleId`

The UI is RTL, URL-filtered, shows zero denominator as `—`, labels unavailable assignments, and renders `غير محدد` for missing responsible user.

## CSV

CSV supports facilities, cycles, and pending views only. It uses UTF-8 BOM, Arabic headers, escaping, and formula-injection protection. Excel/PDF are out of scope for Issue #51.

## Permissions and Audit

Adds `Forms.ViewComplianceDashboard` and `Forms.ExportComplianceDashboard`. Export writes `FormComplianceExported` with scope summary, filter hash, view, row count, and timestamp.

## Out of Scope

Issue #50 reminders/escalation/notifications are not implemented.
Issue #51 advanced analytics and Excel/PDF export are not implemented.
