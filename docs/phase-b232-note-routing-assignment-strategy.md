# Phase B.2.3.2 — Assignment Strategy

Department target:
- Creates a current `NoteAssignment` to `AssignedToDepartmentId`.
- Does not set `AssignedToUserId`.
- Does not replace an existing active assignment automatically.

Role target:
- Resolves active users in the selected role.
- Requires `Notes.StartWork`, `CanView`, `CanProcess`, geographic scope intersection, and classification access.
- Selects deterministically by current workload, last assignment time, Arabic display name, then user id.

No matching rule, no eligible user, invalid target, or existing active assignment does not block note submission. The note opens and a routing decision records the outcome.

Manual rerouting requires row version, reason, idempotency key, `Notes.RunRouting`, and replacement permission when replacing an existing assignment.
