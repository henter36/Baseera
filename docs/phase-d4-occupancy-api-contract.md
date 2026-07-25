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

Movement summary fields:
- `admissions` is the total inflow count for `Admission`, `TransferIn`, and `ReturnFromLeave`.
- `releases` is the total outflow count for `Release`, `TransferOut`, `TemporaryLeave`, and `Death`.
- `netMovement = admissions - releases`.
- `dailyTrend` uses the same inflow/outflow classification as the top-level summary.
- Separate counters remain available for important movement types: `transferIn`, `transferOut`, `internalTransfers`, `temporaryLeave`, `returns`, `death`, `hospitalTransfers`, `courtTransfers`, `corrections`, and `otherMovements`.
- Rejected import rows are available only in the immediate import response; there is no historical `rejectedMovements` summary field in D.4.
