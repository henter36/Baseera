# Phase B.3.1 — Dashboard Security

## Permission gates

| Permission | Sections |
|------------|----------|
| `Dashboard.ViewOperational` | Workload KPIs, trends, breakdowns |
| `Dashboard.ViewRisk` | Risk/overdue/escalation/routing failure KPIs |
| `Dashboard.ViewRouting` | Routing summary section |
| `Dashboard.ViewCorrectiveActions` | Corrective action KPIs and CA priority queue |

Endpoints enforce permissions in `OperationalDashboardQueryService`. Summary and priority-queues accept any dashboard permission; trends/breakdowns require `Dashboard.ViewOperational`.

## Organizational scope

All note-derived metrics use `INoteScopeService.FilterQueryableAsync` before aggregation. Corrective actions join scoped notes. Routing decisions and escalation occurrences are limited to in-scope target IDs.

Rules match existing project policy: Global, Headquarters, Region, MultipleRegions, Facility, MultipleFacilities, FacilityUnit.

## Effective note type access

`INoteTypeAccessService.FilterViewableNotesAsync` excludes note types without View capability for the current user. Deny overrides apply.

## Classification

Unlike list APIs (redaction), dashboard **excludes** notes/actions with `Classification >= Confidential` when the user lacks `Notes.ViewSensitive` / `CorrectiveActions.ViewSensitive`.

## Soft delete

Global query filters on `IsDeleted` apply automatically.

## Drill-down parity

Dashboard cards link to list pages with equivalent filter query parameters; integration tests assert overdue summary matches `GET /api/v1/notes?overdueOnly=true` totalCount.
