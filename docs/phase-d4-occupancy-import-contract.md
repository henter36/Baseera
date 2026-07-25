# Phase D.4 Occupancy Import Contract

`InmateMovementImportRequest` includes:

- `sourceSystem`
- `importReference`
- `rows[]`

Each row includes:
- `inmateReferenceHash`
- `movementType`
- source and target facility/unit identifiers as applicable
- `occurredAtUtc`
- `externalEventId`
- optional `reasonCode`

Rules:
- `externalEventId` is idempotent per source.
- `sourceSystem` and `externalEventId` are trimmed before comparison and persistence.
- Duplicate rows inside the same request are counted in the immediate `duplicateRows` result and are not added to the database.
- Events that already exist in the database are counted in `duplicateRows` using an initial bounded lookup for the import request.
- Concurrent imports rely on the SQL Server unique constraint as the final authority. When a duplicate race occurs, the service performs one recovery reload of the existing keys, converts rows that became persisted to `duplicateRows`, retries only rows that remain new, and does not swallow unrelated database errors.
- Admission requires a target facility.
- Release requires a source facility.
- Internal transfer requires source and target units. Facility identifiers may be omitted when both units are in the requested facility.
- Self-transfer is rejected.
- Import does not persist raw source files.
- Rejected rows are returned only in the immediate `OccupancyImportResult.RejectedRows` for that batch. D.4 does not persist a historical rejected-row metric, so movement summaries do not expose a misleading always-zero rejected count.

Movement summary classification:
- Inflow: `Admission`, `TransferIn`, `ReturnFromLeave`.
- Outflow: `Release`, `TransferOut`, `TemporaryLeave`, `Death`.
- Neutral: `InternalTransfer`, `HospitalTransfer`, `CourtTransfer`, `Correction`, `Other`.
- Top-level `Admissions`, `Releases`, `NetMovement`, and `DailyTrend` use the same classification source.
