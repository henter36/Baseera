# Phase D.2 Facility Command Center Interaction Model

Primary flow:
1. User opens `/workspaces/facilities/{facilityId}`.
2. Command header and situation overview explain the current state.
3. Intervention queue lists the highest-priority items.
4. Selecting a row updates the URL with `panel={type}&entityId={id}` and opens the context panel.
5. Closing the panel removes only panel parameters and preserves filters.

Supported panel types:
- `note`: loads note workspace detail, linked corrective actions, timeline, and inline note actions supported by current APIs.
- `corrective-action`: loads detail and status history.
- `escalation`: shows a safe priority preview until a normalized occurrence detail contract exists.
- `form`: shows a safe compliance preview; full form entry remains explicit.
- `activity`: shows event summary and source reference.

Navigation rules:
- Queue rows do not navigate away by default.
- Full page links are secondary and explicit.
- Browser Back/Forward reflects panel open/close state.
- Escape closes the panel.
- Focus moves into the panel and returns to the selected row on close.

