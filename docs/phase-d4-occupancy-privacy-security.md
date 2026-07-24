# Phase D.4 Occupancy Privacy And Security

Privacy rules:
- Facility Workspace shows statistical counts only.
- No inmate name, civil ID, case number, or personal identifier is shown.
- `InmateReferenceHash` is stored for deduplication and source traceability but is not returned in workspace DTOs.
- Import payloads are not copied into AuditLog.
- Audit entries record operation metadata only.

Security rules:
- Server-side RBAC and facility scope are mandatory.
- Out-of-scope facilities return 404.
- Missing permissions return 403.
- Import is bounded to 100 rows per request.
- External event IDs are uniquely constrained to prevent duplicate import.
- URLs and React Query keys do not contain PII.
