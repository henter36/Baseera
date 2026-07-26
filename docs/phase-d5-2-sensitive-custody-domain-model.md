# Sensitive Custody Domain Model

Core entities:

- `WeaponAsset`, `WeaponTypeDefinition`, `ArmoryLocation`
- `CustodyTransaction`
- `AmmunitionType`, `AmmunitionLot`, `AmmunitionTransaction`
- `SensitiveResourceRequirement`
- `InventorySession`, `InventoryEntry`
- `WeaponInspection`
- `SensitiveCustodyImportBatch`
- `SensitiveCustodyReconciliationResolution`

The model lives in `Baseera.Domain.SensitiveCustody`. Serial numbers are stored as protected text plus a separate SHA-256 hash for matching. Lists and workspace payloads use masked serial values and omit detailed armory locations unless a dedicated permission exists.
