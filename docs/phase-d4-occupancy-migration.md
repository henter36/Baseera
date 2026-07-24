# Phase D.4 Occupancy Migration

Migration: `PhaseD4OccupancyInmateMovement`

Creates:
- `FacilityCapacityBaselines`
- `InmateCensusSnapshots`
- `InmateMovementEvents`

Includes:
- RowVersion concurrency.
- Soft delete columns and query filters.
- Restrict delete behavior.
- Non-negative count constraints.
- Positive capacity constraint.
- Effective date constraint.
- Movement required source/target constraints.
- Idempotent external event unique index.

Backfill:
- No production backfill is assumed.
- Development demo seed creates non-production example occupancy data only when demo seed is enabled.
