# Phase D0 Workspace Framework Performance

Controls:

- Workspace orchestration caps widget execution with `WidgetQueryBudget = 8`.
- Widgets use existing server-side dashboard aggregations.
- No widget loads all records into memory for aggregation in D0.
- React Query keys include workspace key and filter context.
- The reference route does not start refresh loops.
- Widget endpoint allows loading one widget without requesting the full shell.

Future work:

- Add per-widget timeout policies if a slow provider appears.
- Add query-count baselines for high-traffic Facility/Region workspaces in #11-#13.
