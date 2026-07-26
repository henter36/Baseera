# Ammunition Ledger

`AmmunitionTransaction` is append-only. `AmmunitionLot.CurrentQuantity` is adjusted through service commands only.

Rules:

- issue/consumption/transfer/damage/expiry/destruction decrease quantity;
- receipt/return/transfer-in/release increase quantity;
- resulting quantity must not be negative;
- quarantined and reserved quantities do not count as available readiness;
- lot/type/facility filters are bounded.

Evidence: `AmmunitionLedgerPolicy` and `IAmmunitionLedgerService`.
