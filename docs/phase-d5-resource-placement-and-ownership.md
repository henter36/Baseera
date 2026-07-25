# Phase D.5 Resource Placement And Ownership

Ownership and operational location are separate:

- `OwnershipOrganizationId` identifies the owning organization.
- `OperationalFacilityId` and `OperationalFacilityUnitId` identify where the resource operates.
- `ResourcePlacement` preserves history and enforces one active placement per asset.

Moving a resource for maintenance or temporary deployment does not transfer ownership. Facility summaries count resources by active operational facility to avoid double counting.
