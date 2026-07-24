# Phase D.1 Facility Workspace API Contract

Uses existing Workspace Framework endpoints:

```http
GET /api/v1/workspaces/facility-operations
  ?level=1
  &facilityId={guid}
  &fromUtc={iso8601}
  &toUtc={iso8601}

GET /api/v1/workspaces/facility-operations/widgets
  ?level=1
  &facilityId={guid}
  &fromUtc={iso8601}
  &toUtc={iso8601}

GET /api/v1/workspaces/facility-operations/widgets/{widgetKey}
  ?level=1
  &facilityId={guid}
  &fromUtc={iso8601}
  &toUtc={iso8601}
```

Behavior:
- `fromUtc` and `toUtc` are optional. When omitted, the Workspace Framework applies its default resolved date range.
- `400` invalid level/date input.
- `403` missing workspace permission.
- `404` missing or out-of-scope facility.
- Widgets unavailable by permission are omitted, not returned as widget-level `403`.

Payloads remain in `WidgetDataEnvelopeDto` with typed server payloads.
