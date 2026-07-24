# Phase D0 Workspace Framework API Contract

Endpoints:

- `GET /api/v1/workspaces/{workspaceKey}`
- `GET /api/v1/workspaces/{workspaceKey}/widgets`
- `GET /api/v1/workspaces/{workspaceKey}/widgets/{widgetKey}`

Query parameters:

- `level`
- `regionId`
- `facilityId`
- `entityId`
- `fromUtc`
- `toUtc`
- `locale`
- `timeZone`

Server behavior:

- Requires `Workspaces.View`.
- Validates level support and organizational scope.
- Returns `404` for unknown or out-of-scope workspace/widget contexts.
- Returns `403` for authenticated users missing required permission.
- Returns `400` for invalid level/date/scope input.
- Does not accept permissions, allowed actions, or scope authority from the client.
