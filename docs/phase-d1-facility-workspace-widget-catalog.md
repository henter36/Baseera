# Phase D.1 Facility Workspace Widget Catalog

Workspace key: `facility-operations`

- `facility.header`: facility name, region, type, date range.
- `facility.executive-summary`: deterministic operational status and top driver.
- `facility.notes-overview`: open, critical, overdue, unassigned, user-action, new notes, top note types.
- `facility.corrective-actions`: open, overdue, in-progress, pending verification, reopened, critical, average closure hours.
- `facility.alerts-escalations`: open/critical escalations and personal unread escalation notifications.
- `facility.form-compliance`: values from the Form Compliance service.
- `facility.priority-queue`: top 10 bounded priority items from notes, corrective actions, escalations, and forms.
- `facility.recent-activity`: top 10 bounded recent events from current operational tables.

Widgets are hidden entirely when the user lacks the widget permission.

Acknowledgement counts are intentionally deferred. The current data model has unread personal notifications, but no independent facility acknowledgement state; the MVP does not equate unread items with operational acknowledgement.
