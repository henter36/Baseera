# Phase B.2.3.1 Effective Access

Effective note type access is calculated per user, note type, and capability.

Formula:

```text
EffectiveAccess = UserOverride ?? RoleGrantsUnion
Allowed = EffectiveAccess
    AND RBAC permission
    AND geographic scope
    AND classification access
```

Rules:

- Multiple roles are combined with OR for each capability.
- User direct deny overrides role grants.
- User direct allow can add a capability not present in roles.
- No role grant and no user override means no type capability.
- `SystemAdministrator` receives explicit seeded grants; services do not bypass by role name.

Capabilities:

- View
- Create
- Assign
- Process
- SubmitForVerification
- Review
- Cancel
- Reopen
- Archive
- Restore

