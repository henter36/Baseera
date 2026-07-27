# Sensitive Custody Performance

Budgets:

- workspace payload: bounded summary, top 10 interventions, top 20 data-quality issues, top 20 timeline rows;
- weapon list: maximum 100 rows;
- transactions/inventory/inspections: maximum 100 rows;
- no per-member or per-weapon fan-out loops in workspace summary;
- full Facility Workspace query-count budget: 140 SELECT statements after adding the sensitive custody widget; observed local value was 134 and remained independent of note volume;
- query cache keys are facility-scoped through workspace filters;
- context panels are lazy and opened only by route state.

Regression coverage includes SQL integration for workspace redaction and API scope, plus frontend DOM tests for safe section rendering.
