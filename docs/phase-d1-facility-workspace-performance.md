# Phase D.1 Facility Workspace Performance

Performance decisions:
- Workspace uses request-scoped read cache for shared dashboard/form calculations.
- Notes and corrective actions are scoped server-side and aggregated with SQL.
- Priority Queue and Recent Activity load bounded sets from each source and merge only those bounded rows.
- Query count is expected to remain fixed relative to item count.
- No refresh loop is introduced; React Query uses explicit query keys including workspace key, facility id, and filters.
- No EF migration is introduced.

Baseline target: the workspace shell plus authorized widgets should stay within a bounded number of queries and not grow linearly with records.

Validation: `OperationalDashboardQueryCountIntegrationTests.Facility_workspace_query_count_is_bounded_and_independent_of_note_volume` asserts the Facility Workspace query count is fixed after increasing note volume and remains under the conservative MVP ceiling.
