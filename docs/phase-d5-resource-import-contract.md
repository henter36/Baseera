# Phase D.5 Resource Import Contract

Import is bounded and idempotency-aware at asset-code level within the facility.

Preview validates:

- required asset code and display name,
- duplicate rows inside the request,
- duplicates already in the facility,
- row limit.

Confirm applies valid rows and stores `ResourceImportBatch` summary only. Raw file contents are not written to AuditLog.
