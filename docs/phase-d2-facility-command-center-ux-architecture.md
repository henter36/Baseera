# Phase D.2 Facility Command Center UX Architecture

The redesign is frontend-heavy. The page continues to call:
- `GET /api/v1/workspaces/facility-operations`
- Existing note workspace detail APIs.
- Existing corrective action detail/history APIs.

No alternate Workspace Shell, registry, resolver, or authorization path was introduced.

Main page structure:
- `FacilityWorkspacePage`: owns URL filters, workspace query, panel state, action center state.
- `CommandHeader`: summarizes facility context and global data state.
- `SituationOverview`: renders state classification and operational pulse.
- `InterventionQueue`: compact prioritized list; row selection opens context, not a page.
- `CommandContextPanel`: lazy-loads supported entity details and exposes secondary full-page links.
- `ActionCenter`: in-workspace summary of items needing user attention.

Data loading:
- Workspace shell is loaded once per facility/filter context.
- Panel details are loaded on demand with query keys containing type and entity id.
- The workspace is not reloaded just to open or close a panel.
- Workspace refresh invalidates the shell and panel cache after supported inline note actions.

Security:
- `Workspaces.View` and `Workspaces.ViewFacility` are still checked before the workspace query starts.
- Entity previews use existing APIs, so facility scope and permissions remain server-authoritative.
- Unsupported complex actions are disabled in the context panel and remain available through full pages.

