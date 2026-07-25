# Phase D.5 Resource Domain Model

The resource model uses a shared `ResourceAsset` plus typed profile tables:

- `VehicleProfile`
- `CommunicationDeviceProfile`
- `EquipmentProfile`
- `FacilityAssetProfile`

History and operational state are separated:

- `ResourceStatusEvent` records approved status changes.
- `ResourcePlacement` records active and historical operational placement. Ownership can differ from operational facility/unit.
- `MaintenanceWorkOrder` records maintenance and inspection work.
- `ResourceRequirement` records approved requirement baselines.
- `ResourceImportBatch` records bounded import summaries without raw file payloads.

Resource types in this phase are `Vehicle`, `CommunicationDevice`, `OperationalEquipment`, `SecurityEquipment`, and `FacilityAsset`. Weapons and personnel are explicitly excluded.
