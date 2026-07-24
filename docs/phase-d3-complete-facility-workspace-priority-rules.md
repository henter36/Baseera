# Phase D.3 Priority Rules

Current unified priority queue is deterministic and bounded.

Supported sources:

- Critical open notes: base rank `90`.
- Overdue notes: base rank `80` plus capped overdue days.
- Overdue corrective actions: base rank `70` plus capped overdue days.
- Open escalation occurrences: base rank `75` plus escalation level.
- Overdue form assignments: base rank `65` plus capped overdue days.

Unsupported domains do not create priority items because they have no operational records. Their absence appears in Data Quality and Action Center gap chips.

Tie-breakers:

1. Higher priority rank.
2. Earlier due date.
3. Reference number.

