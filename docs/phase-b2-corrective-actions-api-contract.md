# Phase B.2.1 — API Contract

## Routes

- `GET /api/v1/corrective-actions`
- `GET /api/v1/corrective-actions/{id}`
- `GET /api/v1/notes/{noteId}/corrective-actions`
- `POST /api/v1/notes/{noteId}/corrective-actions`
- `PUT /api/v1/corrective-actions/{id}`
- `POST /api/v1/corrective-actions/{id}/submit`
- `POST /api/v1/corrective-actions/{id}/assign`
- `POST /api/v1/corrective-actions/{id}/start-work`
- `POST /api/v1/corrective-actions/{id}/submit-for-verification`
- `POST /api/v1/corrective-actions/{id}/return-for-rework`
- `POST /api/v1/corrective-actions/{id}/verify-completion`
- `POST /api/v1/corrective-actions/{id}/reopen`
- `POST /api/v1/corrective-actions/{id}/cancel`
- `POST /api/v1/corrective-actions/{id}/archive`
- `POST /api/v1/corrective-actions/{id}/restore`
- `GET /api/v1/corrective-actions/{id}/history`
- `GET /api/v1/corrective-actions/{id}/assignments`
- `GET /api/v1/corrective-actions/{id}/attachments`

## List Filters

`search`, `noteId`, `status`, `priority`, `classification`, `ownerDepartmentId`, `assignedToUserId`, `regionId`, `facilityId`, `facilityUnitId`, `overdueOnly`, `dueSoonDays`, `dueFrom`, `dueTo`, `createdFrom`, `createdTo`, `page`, `pageSize`, `sortBy`, `sortDesc`.

Sorting uses an explicit allowlist. Page size is capped server-side.

## Responses

- 400: validation failure as ProblemDetails.
- 403: in-scope record but missing permission.
- 404: missing or out-of-scope record.
- 409: RowVersion conflict or invalid workflow transition.

DTOs are dedicated application contracts; EF entities are not returned.
