# Phase D.5 Resource API Contract

Endpoints:

- `GET /api/v1/facilities/{facilityId}/resources/summary`
- `GET /api/v1/facilities/{facilityId}/resources/categories`
- `GET /api/v1/facilities/{facilityId}/resources/exceptions?limit=20`
- `GET /api/v1/facilities/{facilityId}/resources/units`
- `GET /api/v1/facilities/{facilityId}/resources/timeline?limit=50`
- `GET /api/v1/facilities/{facilityId}/resources/assets?resourceType={enum}&search={text}&pageSize=50`
- `GET /api/v1/facilities/{facilityId}/resources/assets/{assetId}`
- `POST /api/v1/facilities/{facilityId}/resources/assets`
- `POST /api/v1/facilities/{facilityId}/resources/assets/{assetId}/status`
- `POST /api/v1/facilities/{facilityId}/resources/assets/{assetId}/placements`
- `POST /api/v1/facilities/{facilityId}/resources/maintenance`
- `POST /api/v1/facilities/{facilityId}/resources/requirements`
- `POST /api/v1/facilities/{facilityId}/resources/import/preview`
- `POST /api/v1/facilities/{facilityId}/resources/import/confirm`

Missing permission returns 403. Out-of-scope facility or asset returns 404.

`GET .../assets/{assetId}` returns 404 when the asset is missing or outside facility scope, and 403 when the caller lacks the asset type view permission. Asset codes are organization-scoped (normalized uppercase); duplicate create returns 409. Plate numbers are redacted unless the caller has `Resources.ViewVehicles`.
