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
- Admission requires a target facility.
- Release requires a source facility.
- Internal transfer requires source and target units.
- Self-transfer is rejected.
- Import does not persist raw source files.
