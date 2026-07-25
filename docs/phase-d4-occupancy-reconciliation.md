# Phase D.4 Occupancy Reconciliation

D.4 lays the reconciliation boundary but does not implement silent correction.

Supported now:
- Store snapshots.
- Store movements.
- Show movement totals for the selected period.
- Show warnings when source quality is partial, stale, or conflicting.

Deferred:
- Reconciled projection generation.
- Approval workflow for resolving discrepancies.
- Daily materialized occupancy projection.

Any future reconciliation must create an auditable record with reason, source summary, difference, decision maker, and effective timestamp.
