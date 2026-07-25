# Phase D.5 Resource Permissions

Permissions:

- `Resources.ViewSummary`
- `Resources.ViewAssets`
- `Resources.ViewVehicles`
- `Resources.ViewCommunicationDevices`
- `Resources.ViewEquipment`
- `Resources.ViewFacilityAssets`
- `Resources.ManageAssets`
- `Resources.ManagePlacements`
- `Resources.ManageStatus`
- `Resources.ViewMaintenance`
- `Resources.ManageMaintenance`
- `Resources.ViewRequirements`
- `Resources.ManageRequirements`
- `Resources.Import`
- `Resources.Export`
- `Resources.Reconcile`

Workspace permission does not grant resource access. Summary-only access must not expose sensitive identifiers such as vehicle plates where detailed asset access is absent.

Asset list/detail require `Resources.ViewAssets` plus the matching type permission (`ViewVehicles`, `ViewCommunicationDevices`, `ViewEquipment`, or `ViewFacilityAssets`). Missing type permission returns 403 after facility-scope checks; missing/out-of-scope assets return 404.

Facility workspace resource priority drill-downs require `Resources.ViewSummary`; resource activity drill-downs require `Resources.ViewMaintenance`.
