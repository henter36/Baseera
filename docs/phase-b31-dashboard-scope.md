# Phase B.3.1 — Dashboard Scope

## Included

- Operational decision dashboard at `/dashboard` (Arabic RTL)
- KPI summary, trends (7/30/90 days), breakdowns, priority queues
- Server-side Scope + Effective Note Type Access + Classification exclusion + soft-delete
- Permissions: `Dashboard.ViewOperational`, `Dashboard.ViewRisk`, `Dashboard.ViewRouting`, `Dashboard.ViewCorrectiveActions`
- Drill-down to notes/corrective-actions lists with matching query filters
- `DueSoonDays` and `UnassignedOnly` on notes list API for parity

## Excluded

- Export PDF/Excel/CSV, report builder, scheduled reports
- Email/SMS/WhatsApp, AI predictions/recommendations
- Digital twin, optimization engine, national KPI engine
- Form builder, vehicles, weapons, workforce modules
- Phase C, D, E, F, G
- Caching layer (none added)
- Schema migration (permissions seeded via `DatabaseInitializer` only)
