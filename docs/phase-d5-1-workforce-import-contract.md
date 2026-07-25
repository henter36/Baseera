# Phase D.5.1 Workforce Import Contract

Bounded JSON import via preview/confirm. Idempotency key: `(FacilityId, SourceSystem, SourceReference, FileHash)` unique on `WorkforceImportBatches`.

Request (`WorkforceImportPreviewRequest`):

- `sourceSystem`, `sourceReference`, `fileHash`
- `rows[]` with `employeeNumber`, `displayName`, `jobTitle`, `primarySpecialty`, optional `externalPersonnelId` / unit / employment / `isOperational`
- Max **500** rows per request

Preview validates:

- required fields,
- unit membership in target facility,
- duplicate employee numbers inside the request or already in the organization (normalized uppercase),
- batch count invariants (`Valid + Rejected + Duplicate = Total`, `Applied ≤ Valid`),
- Previewed state: `AppliedRows = 0` and `ConfirmedAtUtc` null.

Confirm:

- Re-confirm of the same keys returns the prior confirmed result without re-inserting members.
- Applies valid rows as `WorkforceMember` with `SourceType.Import`, home/operational facility = target facility.
- Stores `WorkforceImportBatch` summary only (no raw file bytes in AuditLog).
- Race on unique batch index reloads the confirmed batch.

Raw biometric feeds, payroll files, and Region/HQ bulk sync are out of scope.
