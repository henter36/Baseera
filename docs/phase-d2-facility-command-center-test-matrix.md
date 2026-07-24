# Phase D.2 Facility Command Center Test Matrix

Frontend tests added or updated:
- Command header, situation overview, and intervention queue render.
- Date filters sync to the URL while keeping facility context.
- Missing either `Workspaces.View` or `Workspaces.ViewFacility` prevents API calls.
- Note priority opens note details inside context panel.
- Unsupported note actions are disabled with a clear reason.
- Corrective action priority opens detail and history inside context panel.
- Escalation priority opens a safe in-workspace preview.
- Form priority opens a safe in-workspace preview.
- Action center opens without changing route.
- Direct panel URL opens the context panel.
- Panel close preserves filters.
- Partial widget failure is announced without dropping the workspace.

Manual visual checks required:
- Desktop 1440.
- Desktop with panel open.
- Tablet layout.
- Mobile overview.
- Mobile detail.
- Critical, stable, partial, and empty states.

Backend tests:
- No backend contract changes were required in this phase. Existing D.1 workspace integration tests remain the authority for scope, permissions, 400/403/404 behavior, partial failures, and query count.

