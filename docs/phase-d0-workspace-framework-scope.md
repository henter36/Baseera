# Phase D0 Workspace Framework Scope

Issue #10 creates the shared Workspace framework foundation only.

Included:
- Workspace contracts for Facility, Region, Headquarters, and Domain levels.
- Server-side context resolution, authorization, scope validation, widget registry, and query orchestration.
- Module-owned widget registration through DI.
- Shared frontend workspace shell, header, widget container, action bar, filter bar, and master-detail layout foundation.
- Feature-flagged `/workspaces/reference` route using real dashboard aggregates.

Excluded:
- Full Facility Workspace, Region Workspace, Headquarters Workspace (#11-#13).
- Saved view persistence and shared layout administration (#21).
- AI, prediction, simulation, dynamic query execution, or production mock data.
- Moving note workflow logic into Workspace Core.
