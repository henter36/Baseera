# Targeting Rules

- AllFacilities / Regions / Facilities / DynamicCriteria
- Dynamic allowlist only: RegionId, FacilityType, IsActive (typed DTO → EF expressions)
- Exclusions win after inclusion; reason required
- Same IFormTargetResolver for preview and cycle generation
- SQL-side filtering via scope + EF; TargetSnapshotHash = SHA-256 of frozen assignment lines
