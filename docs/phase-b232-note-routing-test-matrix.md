# Phase B.2.3.2 — Test Matrix

Baseline:
- Backend build: passed.
- Unit baseline: 284 passed, 0 skipped.
- Integration baseline: 73 passed, 0 skipped.
- Frontend baseline: 98 passed, 0 skipped.

Added targeted unit tests:
- Submit without matching rule records a decision and opens the note.
- Department rule creates a department assignment linked to the routing decision.
- Facility-specific rule wins over a region rule even when priority number is higher.
- Routing settings page empty state, validation, and create request.
- Notes list `requiresRouting=true` tab filter.

Final local counts:
- Unit: 287 passed, 0 skipped.
- Integration: 73 passed, 0 skipped.
- Frontend: 102 passed, 0 skipped.
- Migration apply on a new temporary SQL Server database: passed.
- Migration rollback to B.2.3.1: passed.
- Migration reapply: passed.

Required remaining hardening:
- Integration coverage for concurrency, idempotency, reviewer role, notifications, migration rollback/reapply, and N+1 query counts.
- Frontend tests for the routing rule management and effectiveness screens.
