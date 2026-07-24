# Phase D.1 Facility Workspace Security

- Facility scope is enforced by `WorkspaceContextResolver` and `IOrganizationalScopeService`.
- `Workspaces.View` and `Workspaces.ViewFacility` are required for the workspace.
- Domain permissions control individual widgets: Notes, Corrective Actions, Escalations, Forms Compliance, and Dashboard permissions.
- Corrective action metrics are derived from scoped/viewable notes, preventing note-related leakage.
- Sensitive note/action filtering remains delegated to existing note and corrective-action services.
- Notifications shown are personal to the current user; facility-wide alert normalization is deferred to #19.
- No internal IDs are shown in user-facing header text.

