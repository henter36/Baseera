# Phase D.2 Facility Command Center UX Scope

Issue: #11, continuation only.

This phase redesigns the existing `facility-operations` workspace into a command-center experience. It keeps the D.1 backend contracts, permissions, scoping, APIs, and widgets, and changes the primary user experience from a dashboard grid to an in-context decision workspace.

In scope:
- Command header for facility identity, operational state, freshness, confidence, period, refresh, and action center.
- Situation overview that merges the executive summary and operational pulse.
- Intervention queue as the primary work surface.
- Context panel for notes, corrective actions, escalations, form compliance previews, and activity previews.
- URL-based panel state using `panel` and `entityId`.
- Focus management, Escape close, and return focus to the selected queue row.
- Responsive desktop/tablet/mobile behavior.
- Command-center visual tokens layered above Baseera tokens.
- Frontend tests for in-workspace detail navigation and partial data behavior.

Out of scope:
- Region Workspace and Headquarters Workspace.
- New backend engines, prediction, optimization, simulation, or AI.
- Saved Views persistence.
- New EF schema or migrations.
- Replacing server-side authorization or scoping.
- Full escalation acknowledgement workflow, full form filling, or corrective action complex forms inside the panel.

