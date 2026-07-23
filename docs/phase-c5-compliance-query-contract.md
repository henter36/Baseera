# Phase C.5 — Query Contract

Endpoints:

- `GET /api/v1/form-compliance/summary`
- `GET /api/v1/form-compliance/regions`
- `GET /api/v1/form-compliance/facilities`
- `GET /api/v1/form-compliance/cycles`
- `GET /api/v1/form-compliance/pending`
- `GET /api/v1/form-compliance/trend`
- `GET /api/v1/form-compliance/export.csv`

All endpoints accept `[AsParameters] FormComplianceQuery`.

Filters:

- `fromUtc`, `toUtc`
- `formDefinitionId`, `campaignId`, `cycleId`
- `regionId`, `facilityId`
- `cycleStatus`, `completionBasis`, `responseStatus`
- `isCompleted`, `isOverdue`, `isAvailable`
- `search`, `sort`, `page`, `pageSize`
- `groupBy` for trend: `cycle`/`day` enum values
- `view` for CSV: `facilities`, `cycles`, `pending` enum values

Normalization:

- `Page >= 1`
- `1 <= PageSize <= 100`
- `FromUtc <= ToUtc`
- `Search` is trimmed

Default cycle behavior excludes cancelled cycles unless `cycleStatus=Cancelled` is explicitly selected.

The source projection starts from `FormFacilityAssignments` and left joins responses so `NotStarted` assignments remain visible.
