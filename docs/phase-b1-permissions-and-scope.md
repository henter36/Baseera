# Phase B.1 — Permissions and Scope

See also: [permissions-matrix.md](./permissions-matrix.md).

## Permission codes (Action-Level)

| Code | Use |
|------|-----|
| `Notes.View` | List / detail / history / assignments / attachments metadata |
| `Notes.ViewSensitive` | Unredacted Confidential+ content; sensitive view audited |
| `Notes.Create` | Create draft |
| `Notes.Update` | Update editable fields; submit Draft→Open |
| `Notes.Assign` | Assign / reassign |
| `Notes.StartWork` | Assigned/Reopened → InProgress |
| `Notes.SubmitForVerification` | InProgress → PendingVerification |
| `Notes.ReturnForRework` | PendingVerification → InProgress |
| `Notes.VerifyClosure` | PendingVerification → Closed |
| `Notes.Reopen` | Closed → Reopened |
| `Notes.Cancel` | Draft/Open → Cancelled |
| `Notes.Archive` | Soft delete |
| `Notes.Restore` | Restore soft-deleted |

Services authorize by **permission codes**, never by role name.

## Scope shapes (B.1)

| ScopeType | Required ids |
|-----------|--------------|
| Global (0) | none |
| Headquarters (1) | none — **not** Global |
| Region (2) | RegionId |
| Facility (3) | FacilityId (+ consistent RegionId if supplied) |
| FacilityUnit (4) | FacilityId + FacilityUnitId |

Not supported: MultipleRegions, MultipleFacilities.

## Access rules

Implemented in `INoteScopeService` and applied to every Notes query/command/attachment path:

| Actor scope | Can see |
|-------------|---------|
| Global | All notes within granted Global visibility |
| Headquarters | Headquarters-scoped notes (not all regions) |
| Region | Region + child facilities/units |
| Facility | Facility + its units |
| FacilityUnit | That unit only |

- Outside scope → **404** (not 403) to prevent enumeration
- Missing permission **inside** known accessible scope → **403**
- Soft-deleted facilities/units rejected on create/update
- Soft-deleted roles do not leak via UserRoles / RolePermissions / privilege calculation

## Sensitivity

- Confidential+ requires `Notes.ViewSensitive` for title/description/snippets
- Without it: list/detail show redacted placeholders (`[محجوب]`); note still listed if in scope
- Sensitive detail view writes `AuditLog` with `IsSensitiveView = true`

## Assignment target validation

Assignee user must be: exists, not deleted, active, `ProvisioningStatus = Active`, have work permission, and **scope intersection** with the note. Department assignment does not grant users access outside their own scopes.
