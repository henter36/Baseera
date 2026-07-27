# Sensitive Custody Inventory

Inventory sessions support scheduled, surprise, shift-handover, annual, incident-triggered, transfer-triggered, and other inventory types.

Rules:

- `InventoryEntry` records counted status and discrepancy type;
- completed inventory sessions are moved to `Completed`;
- approval uses rowversion and four-eyes;
- critical discrepancies feed intervention/data-quality projections;
- serials and detailed armory names are not emitted to the general workspace.
