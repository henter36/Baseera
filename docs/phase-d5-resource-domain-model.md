# Phase D.5 Resource Domain Model

The resource model uses a shared `ResourceAsset` plus typed profile tables:

- `VehicleProfile`
- `CommunicationDeviceProfile`
- `EquipmentProfile`
- `FacilityAssetProfile`

History and operational state are separated:

- `ResourceStatusEvent` records approved status changes (append-only).
- `ResourcePlacement` records active and historical operational placement. Ownership can differ from operational facility/unit.
- `MaintenanceWorkOrder` records maintenance and inspection work (numbers from `MaintenanceWorkOrderNumberSequence`).
- `ResourceRequirement` records approved requirement baselines (no overlapping active open windows for the same facility/unit/type/category).
- `ResourceImportBatch` records bounded import summaries without raw file payloads; requires Facility navigation and optional SubmittedByUser.

Resource types in this phase are `Vehicle`, `CommunicationDevice`, `OperationalEquipment`, `SecurityEquipment`, and `FacilityAsset`. Weapons and personnel are explicitly excluded.

Facility-unit integrity uses composite FKs to `FacilityUnit (FacilityId, Id)` for placements, requirements, and assets when a unit is set. Communication/facility profiles keep simple unit FKs because they have no FacilityId column; application services validate membership.
