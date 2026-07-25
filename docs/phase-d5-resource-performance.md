# Phase D.5 Resource Performance

Performance budget:

- Workspace payload uses summary, categories, top exceptions, unit distribution, and bounded timeline.
- Asset lists are capped by `pageSize` and service maximum.
- Exceptions are capped to Top 50 service-side.
- Timeline is capped to Top 50 service-side.
- Read queries use `AsNoTracking`.
- Core indexes cover facility/type/status, facility unit/status, status event date, maintenance status/due date, requirements, and import idempotency.

The implementation avoids loading all resources into the frontend for aggregation.
