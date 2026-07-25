# Phase D.5.1 Workforce Performance

Budgets and bounds:

- Workspace payload: summary + optional coverage/units/roles + data quality (permission-gated slices).
- Member list: `pageSize` clamped to `MemberPageSizeLimit` (100).
- Roster list: Top 50 by duty date.
- Member availability history on detail: Top 50 events.
- Import: max 500 rows.
- Read queries use `AsNoTracking` where appropriate.
- Facility summary upserts one facility-level `WorkforceReadinessSnapshot` per UTC day (updates same-day row).

Indexes (migration highlights):

- Members: org+employee number (unique filtered), org+external id (unique filtered), facility+employment, facility+unit.
- Assignments: member+primary+from, facility+unit+role+from.
- Availability: member+starts+ends.
- Rosters: unique facility/shift/date (with/without unit filters).
- Import batches: unique facility+source+reference+hash.
- Role definitions: unique org+code.
- Snapshots: facility+capturedAt and composite facility/unit/shift/role+capturedAt.

Frontend admin page parallel-fetches only permitted slices; workspace does not load all members for aggregation.
