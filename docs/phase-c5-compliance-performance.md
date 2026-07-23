# Phase C.5 — Performance

Query architecture:

- The shared projection starts from `FormFacilityAssignments`.
- Response data is joined with a left join.
- Grouping for summary, region, facility, cycle, and trend views is expressed as EF queries.
- Pagination is applied after filtering and sorting.
- `TotalCount` is computed from the filtered query.

Indexes reviewed:

- Existing Phase C.3/C.4 migrations already add core campaign, cycle, assignment, and response indexes.
- No duplicate index migration is added in this phase.

Risks and follow-up:

- Average duration is expressed through EF projection and must remain covered by SQL Server integration tests.
- `LoadPreviousCycleRatesAsync` performs one scoped previous-cycle aggregate per visible page row. Page size is bounded to 100; a future optimization can pre-aggregate previous cycles in a single query if this appears in query plans.

Performance smoke target:

- Representative data set: at least 100 cycles and 10,000 assignments.
- Expected behavior: no N+1 over responses or assignments, bounded previous-cycle page lookups, and no in-memory pagination.
