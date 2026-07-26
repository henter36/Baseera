# Sensitive Custody Permissions

Permissions:

- `SensitiveCustody.ViewSummary`
- `SensitiveCustody.ViewWeapons`
- `SensitiveCustody.ViewSerialNumbers`
- `SensitiveCustody.ViewArmoryLocations`
- `SensitiveCustody.ViewAmmunition`
- `SensitiveCustody.ViewCustodyTransactions`
- `SensitiveCustody.ManageWeapons`
- `SensitiveCustody.IssueWeapons`
- `SensitiveCustody.ReceiveWeapons`
- `SensitiveCustody.ApproveTransactions`
- `SensitiveCustody.ManageAmmunition`
- `SensitiveCustody.ConductInventory`
- `SensitiveCustody.ApproveInventory`
- `SensitiveCustody.ManageInspections`
- `SensitiveCustody.ManageMaintenance`
- `SensitiveCustody.ViewDiscrepancies`
- `SensitiveCustody.Export`
- `SensitiveCustody.Import`
- `SensitiveCustody.Reconcile`

`Workspaces.ViewFacility` alone never grants sensitive custody counts. Out-of-scope facility access returns 404; missing permission returns 403.
