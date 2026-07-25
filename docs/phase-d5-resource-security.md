# Phase D.5 Resource Security

Security rules:

- Facility scope is enforced server-side.
- Domain permission is required per endpoint and widget.
- Out-of-scope resources return 404.
- Missing permission returns 403.
- Raw import payloads are not written to AuditLog.
- Weapons, ammunition, and sensitive individual custody are outside this PR.
- Export is a separate permission and is not implemented as a default UI action.
