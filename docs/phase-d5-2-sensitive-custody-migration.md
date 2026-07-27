# Sensitive Custody Migration

Migration:

- `20260726122846_PhaseD52SensitiveCustodyReadiness`

Schema additions:

- `WeaponAssets`, `WeaponTypeDefinitions`, `ArmoryLocations`
- `CustodyTransactions`
- `AmmunitionTypes`, `AmmunitionLots`, `AmmunitionTransactions`
- `SensitiveResourceRequirements`
- `InventorySessions`, `InventoryEntries`
- `WeaponInspections`
- `SensitiveCustodyImportBatches`
- `SensitiveCustodyReconciliationResolutions`

Constraints include unique serial hash per organization, unique internal asset code, filtered active custody index, rowversion columns, nonnegative ammunition quantities, period checks, and restrict deletes for append-only history.
