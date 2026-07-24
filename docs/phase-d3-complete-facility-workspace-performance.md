# Phase D.3 Performance

Performance strategy:

- Keep Workspace shell and widgets within the existing workspace query budget.
- Use server-side aggregation for counts.
- Limit priority queue to 10 items.
- Limit recent activity to 10 items.
- Limit displayed facility units to 12 rows.
- Reuse cached read-service results per request.
- Keep Context Panel lazy-loaded for notes/actions; gap panels do not issue backend calls.
- Query keys include workspace key, facility ID, filters, and panel entity identifiers.

No full-table load or unbounded merge is introduced.

