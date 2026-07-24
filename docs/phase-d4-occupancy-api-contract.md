# Phase D.4 Occupancy API Contract

All endpoints require authentication, server-side permission checks, and facility scope validation.

Read:

```text
GET /api/v1/facilities/{facilityId}/occupancy/summary?asOfUtc={iso8601}
GET /api/v1/facilities/{facilityId}/occupancy/units?asOfUtc={iso8601}
GET /api/v1/facilities/{facilityId}/occupancy/movements/summary?fromUtc={iso8601}&toUtc={iso8601}
```

Write:

```text
POST /api/v1/facilities/{facilityId}/occupancy/capacity
POST /api/v1/facilities/{facilityId}/occupancy/snapshots
POST /api/v1/facilities/{facilityId}/occupancy/movements/import
```

The workspace continues to use:

```text
GET /api/v1/workspaces/facility-operations?level=1&facilityId={guid}&fromUtc={iso8601}&toUtc={iso8601}
```

Responses do not include inmate names, civil IDs, or raw source files.
