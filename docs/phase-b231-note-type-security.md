# Phase B.2.3.1 Security

Every note operation combines:

- RBAC permission.
- Geographic scope.
- Note type capability.
- Classification access.

Direct access to a note whose type is not viewable returns `404` to prevent enumeration. A note inside scope and viewable by type but lacking the operation capability returns `403`.

Corrective actions inherit note type access from their parent operational note. Notifications and escalation recipient selection must not grant access to a target that the user cannot view by note type.

Audit events added:

- `NoteTypeCreated`
- `NoteTypeUpdated`
- `NoteTypeActivated`
- `NoteTypeDeactivated`
- `RoleNoteTypeGrantsUpdated`
- `UserNoteTypeOverridesUpdated`
- `UserNoteIntakeProfileUpdated`
- `NoteLocationSelected`
- `NoteLocationChanged`

