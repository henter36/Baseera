# Phase D.3 Facility Workspace Architecture

The implementation continues to use the Workspace Framework introduced in Issue #10. `facility-operations` remains the single workspace key, and modules register widgets through the existing registry.

Backend changes:

- `FacilityStructurePayload` exposes unit/building/location structure and unit-level operational counts.
- `FacilityDataQualityPayload` exposes domain coverage, freshness context, confidence text, and follow-up references.
- `FacilityStructureWorkspaceWidgetProvider` and `FacilityDataQualityWorkspaceWidgetProvider` are registered via DI.
- `FacilityWorkspaceReadService` remains the read orchestration point and uses scoped, bounded queries.

Frontend changes:

- `FacilityWorkspacePage` now provides 12 internal sections.
- The command-center shell remains the D.2 shell, but the section deck now covers operations, occupancy, resources, risks, projects, compliance, plans, decisions, timeline, and data quality.
- Missing domains are rendered as safe gap states, not fake dashboards.

No EF migration is introduced.

