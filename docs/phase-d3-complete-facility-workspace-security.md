# Phase D.3 Security

Security boundaries remain unchanged:

- `facility-operations` requires `Workspaces.View` and `Workspaces.ViewFacility`.
- Facility scope is resolved server-side.
- Domain widgets are filtered by server-side widget permissions.
- Counts are produced only from existing scoped query builders.
- Missing domains expose no operational counts or identifiers.
- Context Panel detail calls for notes and corrective actions continue to use existing APIs and authorization.
- No client-supplied permission list or scope is trusted.

No new migration, secrets, or audit payloads are introduced.

