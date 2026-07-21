# Phase B.2.3.2 — API Contract

Routing rule endpoints:
- `GET /api/v1/note-routing-rules`
- `GET /api/v1/note-routing-rules/{id}`
- `POST /api/v1/note-routing-rules`
- `PUT /api/v1/note-routing-rules/{id}`
- `POST /api/v1/note-routing-rules/{id}/activate`
- `POST /api/v1/note-routing-rules/{id}/deactivate`
- `POST /api/v1/note-routing-rules/{id}/archive`
- `POST /api/v1/note-routing-rules/{id}/restore`
- `POST /api/v1/note-routing-rules/validate`
- `POST /api/v1/note-routing-rules/preview`

Note routing endpoints:
- `POST /api/v1/notes/{id}/routing/run`
- `POST /api/v1/notes/{id}/routing/preview`

Effectiveness endpoint:
- `GET /api/v1/note-routing/effectiveness`

Note list filter:
- `requiresRouting=true` returns notes requiring manual routing.
