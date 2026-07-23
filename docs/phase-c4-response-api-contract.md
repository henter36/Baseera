# Phase C.4 — API Contract

- GET `/api/v1/form-response-workspace`
- GET/PUT/POST `/api/v1/form-assignments/{assignmentId}/response` (+ `/draft`, `/validate`, `/submit`)
- GET `/api/v1/form-response-reviews`
- GET/POST `/api/v1/form-responses/{responseId}/review|return|approve|reject|close`
- GET submissions + history

Errors: 400/401/404 (out-of-scope)/409 conflict DTO/413/422 structured validation. CorrelationId via middleware.
