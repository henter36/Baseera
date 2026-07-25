# Phase D.4 Occupancy Source Of Truth

Source precedence:

1. Latest authoritative census snapshot.
2. Latest internal snapshot.
3. Derived movement projection, only when explicitly enabled in a later reconciliation workflow.
4. Unknown.

Current D.4 behavior:
- The workspace uses the latest authoritative snapshot where present.
- Internal snapshots are used only when no authoritative snapshot exists.
- Movement totals are shown as period activity, not as current count source.
- Snapshots and movements are not summed together to avoid double counting.
- Missing capacity, missing snapshot, stale snapshot, and conflicting quality produce warnings and lower confidence.
- Reconciliation is documented as a future workflow; D.4 does not create a reconciled snapshot source.

Source conflicts are displayed as data quality warnings. D.4 does not silently correct counts.
