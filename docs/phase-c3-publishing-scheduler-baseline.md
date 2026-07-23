# Phase C.3 — Publishing & Scheduler Baseline

## Starting SHA

- Branch: `phase-c3-form-publishing-scheduler`
- Actual start SHA: `d79392d4a2acaa2d09edbc078366457410aa3b41`
- Expected SHA matched: yes (`origin/main` after PR #62 merge)

## Existing entities / tables (Forms + org)

- `FormDefinitions`, `FormGovernancePolicies`, `FormAccessGrants`, `FormReviewDecisions`
- `FormVersions`, `FormSchemaSnapshots`, `FormVersionReviewDecisions`, `FormTemplates`, `FormDefinitionVersionCounters`
- `Organizations`, `Regions`, `Facilities` (`FacilityType`, `IsActive`), `FacilityUnits`
- Soft-delete query filters on definitions, grants, templates, org entities
- `CurrentLockedVersionId` on `FormDefinition` (campaigns must pin Version/Snapshot/Hash after create)

## Current permissions (seeded)

C.1–C.2 Forms.* including `Forms.Publish`, `Forms.Respond`, monitors, analyze, export (several AuthPolicies still unwired).
Roles: FormDesigner, FormReviewer, FormApprover, FormPublisher, FormRegionalMonitor, FormHeadquartersMonitor, Auditor.

## Baseline verification (pre–C.3)

| Suite | Result |
|-------|--------|
| Backend build Release | Succeeded (0 errors) |
| Unit | **536** passed, 0 failed, 0 skipped |
| Integration | **124** passed, 0 failed, **0** skipped |
| Frontend | **177** passed, **1** failed (pre-existing on main), 178 total |

## Architectural risks

- Multi-instance scheduler races — Unique `(CampaignId, OccurrenceKey)` + `(CycleId, FacilityId)` + RowVersion claim
- Preview scope leakage for regional users
- Dynamic criteria injection if allowlist is bypassed
- Catch-up explosion without `MaxCatchUpOccurrencesPerRun`
- Organizational drift mutating historical cycles if assignments are not frozen

## Temporal risks

- Asia/Riyadh conversions; DST with `America/New_York` for engine tests
- Month-end / leap year (29 Feb, day 30/31)
- Invalid / ambiguous local times
- Scheduler restart catch-up ordering

## Scope boundaries

| Issue | In C.3? |
|-------|---------|
| **#47** publish, targeting, recurrence, cycles, facility assignments | **Yes** |
| **#48** FormResponse / fill / review answers | **No** |
| **#50** reminders, escalations, timed notifications | **No** |
