# Phase D.5.1 Workforce API Contract

Base path: `/api/v1/facilities/{facilityId}/workforce`

| Method | Path | Permission |
|--------|------|------------|
| GET | `/summary` | `Workforce.ViewSummary` |
| GET | `/coverage` | `Workforce.ViewCoverage` |
| GET | `/units` | `Workforce.ViewCoverage` |
| GET | `/roles` | `Workforce.ViewMembers` |
| GET | `/members?search=&pageSize=` | `Workforce.ViewMembers` (pageSize clamped 1–100, default 50) |
| GET | `/members/{memberId}` | `Workforce.ViewMembers` |
| POST | `/members` | `Workforce.ManageMembers` |
| POST | `/assignments` | `Workforce.ManageAssignments` |
| POST | `/qualifications` | `Workforce.ManageQualifications` |
| GET | `/requirements` | `Workforce.ViewCoverage` |
| POST | `/requirements` | `Workforce.ManageRequirements` |
| GET | `/rosters` | `Workforce.ViewCoverage` |
| POST | `/rosters` | `Workforce.ManageRosters` |
| POST | `/rosters/{rosterId}/assignments` | `Workforce.ManageRosters` |
| POST | `/rosters/{rosterId}/publish` | `Workforce.ManageRosters` → 204 |
| POST | `/availability` | `Workforce.RecordAvailability` |
| POST | `/import/preview` | `Workforce.Import` |
| POST | `/import/confirm` | `Workforce.Import` |
| GET | `/data-quality` | `Workforce.ViewSummary` |

Missing permission → 403. Out-of-scope / missing entity → 404. Duplicate org employee number on create → conflict/`InvalidOperationException` mapped by API error pipeline. Duplicate primary assignment windows rejected. Published roster mutation rejected.

Workspace shell loads `facility.workforce` widget when `Workforce.ViewSummary` is present; coverage/roles inside payload are permission-gated.
