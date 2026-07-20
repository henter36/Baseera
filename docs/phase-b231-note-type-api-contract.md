# Phase B.2.3.1 API Contract

Note type endpoints:

- `GET /api/v1/note-types`
- `GET /api/v1/note-types/{id}`
- `POST /api/v1/note-types`
- `PUT /api/v1/note-types/{id}`
- `POST /api/v1/note-types/{id}/activate`
- `POST /api/v1/note-types/{id}/deactivate`

Role grants:

- `GET /api/v1/roles/{id}/note-type-grants`
- `PUT /api/v1/roles/{id}/note-type-grants`

User overrides and intake:

- `GET /api/v1/users/{id}/note-type-overrides`
- `PUT /api/v1/users/{id}/note-type-overrides`
- `GET /api/v1/users/{id}/effective-note-type-access`
- `GET /api/v1/users/{id}/note-intake-profile`
- `PUT /api/v1/users/{id}/note-intake-profile`

Current user:

- `GET /api/v1/me/note-intake-context`
- `GET /api/v1/me/note-intake-context/facilities?regionId=...`
- `GET /api/v1/me/note-types`
- `GET /api/v1/me/note-type-access`

Eligible users:

- `GET /api/v1/notes/{id}/eligible-assignees`
- `GET /api/v1/notes/{id}/eligible-reviewers`

