# Phase C.5 — Security

Permissions:

- `Forms.ViewComplianceDashboard` is required for all dashboard reads.
- `Forms.ExportComplianceDashboard` is required for CSV export.
- `FormRespondent` is not granted the general compliance dashboard permission.

Scope:

- Aggregations use assignment snapshot fields for historical grouping.
- Authorization uses the current organizational scope over current `FacilityId`.
- A moved facility is visible only if the current user can still access that facility.
- Direct out-of-scope facility or region filters return not found behavior.
- Summary counts are scoped; no national denominator is mixed with scoped numerators.

Data minimization:

- Raw answers, canonical answers, review comments, attachments, and sensitive answer content are not read or exported.
- CSV export records an audit event with user, scope summary, filter hash, view, row count, and timestamp, without storing CSV content or large location-name metadata.

Known implementation note:

- Form access grant filtering is aligned with existing form response view permissions and organizational scope. Deny-overrides-allow remains the intended policy boundary for future hardening tests.
