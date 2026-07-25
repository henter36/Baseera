# Phase D.5 Resource Import Contract

Import is bounded and idempotency-aware at organization-scoped asset-code level (normalized uppercase). Confirm is idempotent on `(FacilityId, SourceSystem, SourceReference, FileHash)`.

Preview validates:

- required asset code and display name,
- duplicate rows inside the request,
- duplicates already in the organization,
- optional unit membership in the target facility,
- row limit,
- batch count invariants (`Valid + Rejected + Duplicate = Total`, `Applied <= Valid`).

Confirm applies valid rows and stores `ResourceImportBatch` summary only. Raw file contents are not written to AuditLog. Re-confirm of the same batch keys returns the prior result without re-inserting assets.
