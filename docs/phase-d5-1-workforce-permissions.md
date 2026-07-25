# Phase D.5.1 Workforce Permissions

Permissions (`PermissionCodes`):

| Code | Purpose |
|------|---------|
| `Workforce.ViewSummary` | Summary, data-quality, workspace widget |
| `Workforce.ViewCoverage` | Coverage, units, requirements list, rosters list |
| `Workforce.ViewMembers` | Roles list, members list/detail |
| `Workforce.ViewSensitiveRestrictions` | Restriction codes on member detail |
| `Workforce.ManageMembers` | Create members |
| `Workforce.ManageAssignments` | Create assignments |
| `Workforce.ManageQualifications` | Create qualifications |
| `Workforce.ManageRequirements` | Record staffing requirements |
| `Workforce.ManageRosters` | Create/publish rosters and roster assignments |
| `Workforce.RecordAvailability` | Record availability events |
| `Workforce.Import` | Import preview/confirm |
| `Workforce.Export` | Seeded permission; export UI/endpoint not shipped in this slice |
| `Workforce.Reconcile` | Application reconcile service; HTTP endpoint not mapped in this slice |

Seed grants (development initializer):

- `SystemAdministrator` — all permissions.
- `HeadquartersExecutive` — summary set (`ViewSummary` / `ViewCoverage` / `ViewMembers`).
- `DecisionSupportDirector`, `FacilityDirector`, `WorkforceOfficer` — full manager set including sensitive restrictions, import, export, reconcile.
- `RegionalDirector`, `RegionalCoordinator`, `FacilityCoordinator` — no workforce grants in this seed (resource/occupancy only where applicable).

RBAC role `WorkforceOfficer` is distinct from operational `WorkforceRoleDefinition`.
