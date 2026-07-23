# Phase C.5 — Compliance Scope

Phase C.5 adds a form compliance dashboard, drill-downs, completion indicators, and CSV export for Issue #49.

In scope:

- Dashboard metrics over form campaign cycle facility assignments.
- Summary, region, facility, cycle, pending, trend, and CSV API endpoints.
- RTL dashboard route `/form-compliance` and scoped routes for region, facility, and cycle.
- CSV export for `facilities`, `cycles`, and `pending` views only.
- Permissions `Forms.ViewComplianceDashboard` and `Forms.ExportComplianceDashboard`.

Out of scope:

- Reminders, escalation, notifications, SLA engines, and Issue #50.
- Excel, PDF, report designer, advanced analytics, forecasting, AI summaries, and Issue #51.
- GIS maps, permanent assignment owner model, form schema changes, answer editing, and raw answer export.

The source of truth remains `FormCycle`, `FormFacilityAssignment`, `FormCampaignResponsePolicy`, `FormResponse`, and `FormResponseSubmission`. No aggregate table or shared cross-user cache is introduced.
