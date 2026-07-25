# Phase D.4 Occupancy Permissions

Added permissions:

- `Occupancy.ViewSummary`
- `Occupancy.ViewUnitBreakdown`
- `Occupancy.ViewMovements`
- `Occupancy.ViewSensitiveMovements`
- `Occupancy.ManageCapacity`
- `Occupancy.RecordSnapshot`
- `Occupancy.Import`
- `Occupancy.Export`
- `Occupancy.Reconcile`

Workspace rules:
- `Workspaces.ViewFacility` alone does not grant occupancy access.
- If occupancy permissions are missing, the occupancy widget is not returned by the Workspace Framework.
- Direct endpoints return `403` for missing permission and `404` for out-of-scope facility.
- Sensitive movement identity is not projected into Facility Workspace.
