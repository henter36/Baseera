# Phase D.4 Occupancy Domain Model

The occupancy domain uses explicit entities, not JSON payloads:

- `FacilityCapacityBaseline`: approved operational or special-purpose capacity, with effective dates and source reference.
- `InmateCensusSnapshot`: statistical count at facility or unit level, with quality status and authority flag.
- `InmateMovementEvent`: non-identifying movement record with hashed inmate reference and idempotent external event ID.

Primary enums:
- `CapacityType`: `ApprovedOperational`, `Emergency`, `Temporary`, `MedicalIsolation`, `SecurityIsolation`, `Other`.
- `OccupancySourceType`: `Manual`, `ExternalSystem`, `Import`, `Reconciliation`.
- `CensusQualityStatus`: `Complete`, `Partial`, `Stale`, `Missing`, `Conflicting`.
- `MovementType`: admission, release, transfer, leave, return, death, correction, and other.

The main occupancy rate uses only `ApprovedOperational` capacity. Other capacity types are stored for future policy but are not mixed into the main rate.
