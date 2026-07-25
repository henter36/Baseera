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

The migration uses restrict deletes, row versions, check constraints, filtered unique indexes, and operational indexes. Rollback drops the D.5 resource tables only.
