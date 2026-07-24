# Phase D.3 Data Quality

Data Quality prevents users from interpreting missing records as a stable situation.

Statuses:

- `complete`: source exists and has records or structure.
- `partial`: source exists but has no current records or incomplete coverage.
- `unavailable`: no domain model/API exists.

Current unavailable domains:

- Occupancy and inmates.
- Staff and resource readiness.
- Vehicles, weapons, communication devices, and equipment.
- Standalone incidents.
- Risks and treatments.
- Projects and initiatives.
- Operational and emergency plans.
- Decisions and directives.

These statuses are returned by the backend as typed payload, not inferred from UI null checks.

