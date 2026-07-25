# Phase D.4 Occupancy Performance

Read strategy:
- Latest capacity and census are projected server-side.
- Unit breakdown uses bounded unit rows and grouped latest-per-unit projections.
- Movement summary aggregates server-side by type and day.
- Priority and activity additions are bounded.

Indexes:
- Facility/date/type indexes for movements.
- Facility/unit/date indexes for snapshots.
- Facility/unit/effective date indexes for capacity.
- Unique filtered index for imported external events.

Budgets:
- Workspace occupancy widget performs bounded aggregate reads.
- Context panel does not load raw movement lists.
- Import request is capped at 100 rows.
