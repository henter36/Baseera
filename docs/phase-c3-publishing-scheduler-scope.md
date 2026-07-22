# Phase C.3 Scope — Form Publishing & Scheduler

Closes Issue **#47** (Epic **#45**). Depends on merged **#46** / PR #62.

## In scope
- FormCampaign aggregate, targeting rules, exclusions, cycles, facility assignments
- Recurrence (Once/Daily/Weekly/Monthly/CustomDates), Asia/Riyadh + DST-safe engine
- Target preview (same resolver as publish), immutable cycle snapshots
- Idempotent multi-instance scheduler
- API `/api/v1/form-campaigns`, RTL UI wizard/list/detail/cycles
- Permissions, audit, migration

## Out of scope
- **#48** FormResponse / fill / answer review — not started
- **#50** reminders / escalations / timed notifications — not started
