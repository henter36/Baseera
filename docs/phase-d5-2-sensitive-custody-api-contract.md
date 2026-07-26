# Sensitive Custody API Contract

Facility-scoped routes are mounted under:

`/api/v1/facilities/{facilityId}/sensitive-custody`

Implemented groups:

- summary, weapons list/detail/create/update;
- custody transactions list/create/approve/handover/receive/reverse;
- ammunition lots and ledger transactions;
- inventory sessions, entries, complete, approve;
- inspections;
- data quality;
- import preview/confirm;
- reconciliation.

All routes require server-side authorization and facility scope checks. Read paths use bounded limits and `AsNoTracking`; write paths require rowversion where updating existing records.
