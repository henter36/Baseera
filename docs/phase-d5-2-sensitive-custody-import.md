# Sensitive Custody Import

Supported import kinds:

- weapon master;
- armory locations;
- current custody;
- ammunition lots;
- ammunition balances;
- requirements.

The current implementation supports preview/confirm batches with file hash, row limits, validation counts, idempotency key, and safe audit summary. It does not persist raw uploaded files or raw serial values into audit.
