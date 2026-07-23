# Migration Notes — PhaseC3FormPublishingScheduler

Adds: FormCampaigns, FormTargetRules, FormCampaignExclusions, FormCycles, FormFacilityAssignments, OrganizationBusinessCalendarDates.
Unique indexes: org+code (filtered soft-delete), CampaignId+OccurrenceKey, CampaignId+SequenceNumber, CycleId+FacilityId, CampaignId+FacilityId exclusions.
Alternate key: FormCycles (CampaignId, Id) — composite assignment FK references (CampaignId, CycleId).
Check constraints: ResponseWindowMinutes > 0; Grace/Close >= 0.
DeleteBehavior Restrict; RowVersion on FormCampaign, FormTargetRule, FormCampaignExclusion, FormCycle, FormFacilityAssignment, OrganizationBusinessCalendarDate.
Apply from empty DB or main after PR #62.
