# Phase C.3 Domain

## Aggregates
- FormCampaign (pinned FormVersionId, FormSchemaSnapshotId, SchemaHash)
- FormTargetRule (AllFacilities|Regions|Facilities|DynamicCriteria)
- FormCampaignExclusion (reason required)
- FormCycle (OccurrenceKey, TargetSnapshotHash, frozen counts)
- FormFacilityAssignment (org snapshot fields; no response state)
- OrganizationBusinessCalendarDate

## State machines
Campaign: Draft→Scheduled/Active→Paused↔→Cancelled/Completed (no revive from Cancelled/Completed).
Cycle: Scheduled→Open→Grace→Closed (or Cancelled).

## Immutability
After publish: version/snapshot/hash/targeting/exclusions/recurrence/timezone/window policy frozen. Clone to Draft for material changes.
