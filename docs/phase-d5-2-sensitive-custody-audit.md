# Sensitive Custody Audit

Audit events are emitted for weapon create/update, custody transaction create/transition, ammunition transaction, inventory changes, inspection, import confirmation, and reconciliation.

Safety rule: audit entries use module `SensitiveCustody`, action name, entity type, entity id, facility id, and safe reason metadata. Serial numbers, detailed armory locations, and raw import files are not written to audit values.
