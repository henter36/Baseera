# Phase D.1 Facility Workspace Architecture

The MVP uses the shared Workspace Framework from Issue #10.

- `FacilityWorkspaceDefinitionProvider` registers the `facility-operations` workspace.
- Widget providers implement `IWorkspaceWidgetProvider`; no alternate registry, shell, or context resolver is introduced.
- `WorkspaceContextResolver` remains authoritative for `FacilityId`, `WorkspaceLevel.Facility`, permission checks, date range, locale, and timezone.
- `FacilityWorkspaceReadService` is a request-scoped Application read service. It caches shared calculations inside a single request to avoid repeated dashboard summaries.
- Metrics reuse `OperationalDashboardFilterBuilder` for note/corrective-action scope and `IFormComplianceQueryService` for form denominator/completion rules.
- No Domain entity or EF migration is added.

