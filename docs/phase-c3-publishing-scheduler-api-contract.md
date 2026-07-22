# Phase C.3 API Contract

Base: `/api/v1/form-campaigns`

- GET/POST `/`
- GET/PUT `/{campaignId}`
- POST `/{campaignId}/clone|target-preview|publish|pause|resume|cancel|complete`
- GET `/{campaignId}/cycles`, `/{campaignId}/cycles/{cycleId}`, `.../assignments`
- GET `/target-options/regions|facilities`
- POST `/schedule-preview`

Errors: 404 out-of-scope/view-deny; 403 action deny; 409 invalid transition/RowVersion; duplicate occurrence = idempotent.
