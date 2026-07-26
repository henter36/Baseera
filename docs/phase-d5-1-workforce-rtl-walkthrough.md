# Phase D.5.1 Workforce RTL Walkthrough

Workforce views inherit the Phase D.2 command-center Arabic RTL layout.

Checks:

- Facility Workspace section `القوى البشرية والتغطية` (`?section=workforce`) keeps coverage strip, rails, and exceptions right-aligned.
- Context Panel types `workforce-member` / `workforce-shift` / `workforce-role` / `workforce-gap` open from the right panel without flipping LTR chrome.
- Admin page `/facilities/:facilityId/workforce` section nav (المشهد العام، التغطية، الورديات، الوحدات، الأدوار، الأعضاء، المتطلبات، الاستيراد، جودة البيانات) remains RTL and wraps on mobile without horizontal scroll on primary rails.
- Full-page drill-downs return to `/workspaces/facilities/{facilityId}?section=workforce` or `/facilities/{facilityId}/workforce`.
- Focusable controls are buttons/links only where actions exist; empty/unauthorized states use shared Workspace shells.

Screenshots folder: `docs/screenshots/phase-d5-1/` (capture notes in that README; PNG captures may still be pending).
