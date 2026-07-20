# Phase B.2.2 API Contract

## Policies

- `GET /api/v1/escalation-policies`
- `GET /api/v1/escalation-policies/{id}`
- `POST /api/v1/escalation-policies`
- `PUT /api/v1/escalation-policies/{id}`
- `POST /api/v1/escalation-policies/{id}/activate`
- `POST /api/v1/escalation-policies/{id}/deactivate`
- `POST /api/v1/escalation-policies/{id}/archive`
- `POST /api/v1/escalation-policies/{id}/restore`

## Rules

- `GET /api/v1/escalation-policies/{id}/rules`
- `POST /api/v1/escalation-policies/{id}/rules`
- `PUT /api/v1/escalation-rules/{id}`
- `POST /api/v1/escalation-rules/{id}/enable`
- `POST /api/v1/escalation-rules/{id}/disable`

## Runs and Occurrences

- `POST /api/v1/escalations/run`
- `GET /api/v1/escalations/occurrences`
- `GET /api/v1/escalations/occurrences/{id}`
- `POST /api/v1/escalations/occurrences/{id}/retry`

## Notifications

- `GET /api/v1/notifications`
- `GET /api/v1/notifications/unread-count`
- `GET /api/v1/notifications/{id}`
- `POST /api/v1/notifications/{id}/read`
- `POST /api/v1/notifications/read-all`
- `POST /api/v1/notifications/{id}/archive`

All lists are server-side paginated and use explicit filter DTOs.
