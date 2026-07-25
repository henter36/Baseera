# Phase D.5 Resource Migration

Migration: `PhaseD5ResourceReadinessCore`.

It creates:

- `ResourceAssets`
- `VehicleProfiles`
- `CommunicationDeviceProfiles`
- `EquipmentProfiles`
- `FacilityAssetProfiles`
- `ResourceStatusEvents`
- `ResourcePlacements`
- `MaintenanceWorkOrders`
- `ResourceRequirements`
- `ResourceImportBatches`
- sequence `MaintenanceWorkOrderNumberSequence`
- `FacilityUnits` alternate key `(FacilityId, Id)` for composite facility-unit FKs

Integrity highlights: organization-scoped unique asset codes, composite facility-unit FKs (where facility id is available), filtered unique open requirements (facility-level and unit-level), import batch count/state checks, append-only status events, and soft-delete cascading query filters on dependents.

The migration uses restrict deletes, row versions, check constraints, filtered unique indexes, and operational indexes. Rollback drops the D.5 resource tables/sequence only.
