# Migration Notes — PhaseC3FormPublishingScheduler

Adds: FormCampaigns, FormTargetRules, FormCampaignExclusions, FormCycles, FormFacilityAssignments, OrganizationBusinessCalendarDates.
Unique indexes: org+code (filtered soft-delete), CampaignId+OccurrenceKey, CampaignId+SequenceNumber, CycleId+FacilityId, CampaignId+FacilityId exclusions.
Check constraints: ResponseWindowMinutes > 0; Grace/Close >= 0.
DeleteBehavior Restrict; RowVersion on campaign/cycle/assignment.
Apply from empty DB or main after PR #62.
