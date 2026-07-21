# Phase B.2.3.2 — Security

Permissions:
- `Notes.ViewRouting`
- `Notes.ManageRoutingRules`
- `Notes.ActivateRoutingRules`
- `Notes.RunRouting`
- `Notes.ViewRoutingDiagnostics`

Routing does not grant access. All user selection and reviewer visibility must still satisfy:
- RBAC permission.
- Geographic scope.
- Note type effective access.
- Classification access.
- Existing separation-of-duties rules.

Out-of-scope records remain hidden through existing note scope services. Routing decisions avoid note title, description, attachment names, SQL text, and stack traces.
