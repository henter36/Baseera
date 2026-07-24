# Phase D0 Workspace Framework Security

Security decisions:

- All widget visibility is derived server-side from `ICurrentUser`.
- Allowed actions are server-authored using `WorkspaceAllowedAction`.
- Region/facility identifiers are validated through `IOrganizationalScopeService`.
- Out-of-scope region/facility identifiers return `404`.
- Widget metadata does not carry command secrets or privilege-bypassing identifiers.
- Partial failures log widget key and correlation id only; payloads and sensitive filters are not logged.
- Normal read operations are not audit logged. Future saved/shared layout changes must be audited.
