# Phase D.5.1 Workforce Performance

Budgets and bounds:

- Workspace payload: summary + optional coverage/units/roles/rosters + data quality (permission-gated slices). Large detail panes are lazy-loaded by route/panel state.
- Member list: `pageSize` clamped to `MemberPageSizeLimit` (100).
- Qualification list: facility-scoped endpoint supports paging/filtering and avoids per-member fan-out.
- Roster list: Top 50 by duty date.
- Member availability history on detail: Top 50 events.
- Timeline: bounded recent events only; no unrestricted historical export through Workspace.
- Import: max 500 rows.
- Read queries use `AsNoTracking` where appropriate.
- Facility summary upserts one facility-level `WorkforceReadinessSnapshot` per UTC day (updates same-day row).
- Query-count guard: facility workspace integration test budget stays within 110 select statements for seeded workforce data.
- Payload guard: workspace response remains summary-first; member/qualification detail pages use bounded pages instead of embedding all members in the workspace payload.
- Large-dataset guard: services use grouped database queries and keyed dictionaries; no per-member or per-shift qualification query loop is required for summary/coverage calculations.

Indexes (migration highlights):

- Members: org+employee number (unique filtered), org+external id (unique filtered), facility+employment, facility+unit.
- Assignments: member+primary+from, facility+unit+role+from.
- Availability: member+starts+ends.
- Rosters: unique facility/shift/date (with/without unit filters).
- Import batches: unique facility+source+reference+hash.
- Role definitions: unique org+code.
- Snapshots: facility+capturedAt and composite facility/unit/shift/role+capturedAt.

Frontend admin page parallel-fetches only permitted slices; workspace does not load all members for aggregation.

## Verification Commands

Run before closing #133:

```bash
dotnet test src/backend/tests/Baseera.IntegrationTests/Baseera.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Facility_workspace_query_count_stays_within_budget|FullyQualifiedName~Facility_workspace_payload_size_stays_within_budget|FullyQualifiedName~Large_workforce_member_list_remains_bounded"
```

These checks cover query count, workspace payload size, and bounded member-list behavior for larger seeded workforce data.
