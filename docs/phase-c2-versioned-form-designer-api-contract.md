# Phase C.2 API Contract

Base: `/api/v1`

Versions under `/forms/{formId}/versions` …
Templates under `/form-templates`.

See Issue #46 for the full route list. DTOs are explicit records; EF entities are never returned.
Concurrency: Base64 `rowVersion`. Conflicts and illegal transitions → HTTP 409.
Invalid schema → HTTP 400. Out of scope / view deny → 404. Action deny → 403.
