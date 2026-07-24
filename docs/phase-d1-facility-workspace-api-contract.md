# Phase D.1 Facility Workspace API Contract

Uses existing Workspace Framework endpoints:

```http
GET /api/v1/workspaces/facility-operations?level=1&facilityId={guid}&fromUtc={utc}&toUtc={utc}
GET /api/v1/workspaces/facility-operations/widgets?level=1&facilityId={guid}
GET /api/v1/workspaces/facility-operations/widgets/{widgetKey}?level=1&facilityId={guid}
```

Behavior:
- `400` invalid level/date input.
- `403` missing workspace permission.
- `404` missing or out-of-scope facility.
- Widgets unavailable by permission are omitted, not returned as widget-level `403`.

Payloads remain in `WidgetDataEnvelopeDto` with typed server payloads.

