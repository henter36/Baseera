# Phase D.5 Resource Test Matrix

Implemented automated coverage:

- Readiness denominator and gap calculations.
- Zero requirement does not imply 100% readiness.
- Retired and transferred exclusion.
- Maintenance transition guard.
- Resource access policy (type permissions, asset-code normalize, period overlap, import counts, unique violation detection).
- Workspace resource drill-down permissions (`ViewSummary` priority / `ViewMaintenance` activity).
- Backend build with EF model and migration.
- Frontend typecheck for resource contracts and pages.

Integration coverage (requires `BASEERA_TEST_CONNECTION`):

- Facility scope and 403/404 endpoints.
- Type-permission filtering on list/get.
- Organization-scoped duplicate asset code → 409.
- Import confirm idempotency.
- Concurrent maintenance work-order number uniqueness.
- Soft-delete hides dependents; status events append-only.
- Cross-facility unit rejection; overlapping requirements → 409.
- Workspace widget visibility.
