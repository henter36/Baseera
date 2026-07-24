# Phase D.2 Facility Command Center UX Gap Analysis

## Current Experience

The current Facility Workspace is architecturally correct but visually behaves like a traditional dashboard. It renders the workspace shell header, then a filter card, then a twelve-column grid of bordered widgets. Each widget repeats the same surface, border, radius, heading, freshness badge, and metric cards. The result is operationally valid, but it does not guide the user toward a decision.

## Why It Still Looks Like The Previous Design

1. The page uses `WorkspaceShell`, `WorkspaceHeader`, and `WorkspaceWidgetContainer` exactly as the reference workspace does.
2. `workspace-widget`, `workspace-metric`, and `facility-priority-list` use the same white surfaces, borders, and compact card language as the administrative pages.
3. Executive summary, notes, corrective actions, alerts, form compliance, priority queue, and recent activity are all peer widgets instead of a hierarchy.
4. The priority queue is visually just another list inside a card, not the operational command surface.
5. Drill-down links navigate to old pages by default, so the workspace is a launchpad rather than a command center.

## Old Colors And Components Reused

- `var(--surface)`, `var(--line)`, `var(--bg-accent)`, `var(--muted)`, `var(--danger)`, and `var(--ok)` dominate the screen.
- `WorkspaceWidgetContainer` creates identical bordered cards for every widget.
- `workspace-metric` creates one bordered mini-card per number.
- `WorkspaceDrillDownLink` sends users out to legacy routes.
- `WorkspaceHeader` keeps the same page-title pattern as normal application pages.

## Links That Leave The Workspace

- Priority note: `notes.workspace` currently navigates to `/notes/workspace?noteId=...`.
- Priority corrective action: `corrective-actions.list` currently navigates to `/corrective-actions?id=...`.
- Escalations: `escalations.occurrences` navigates to `/settings/escalations/occurrences`.
- Form compliance: `form-compliance.facility` navigates to `/form-compliance/facilities/{facilityId}`.
- Widget footers repeat the same external drill-down behavior.

## Details Suitable For Context Panel

- Note priority item: summary, reference, title, severity, due date, overdue days, owner, reason, plus detail loaded from `api.notes.get`, history, and note corrective actions.
- Corrective action item: summary, state, due date, owner, priority reason, plus detail loaded from `api.correctiveActions.get` and history.
- Escalation item: operational summary from queue/recent activity, target reference, severity, trigger reason, due date, and an optional full occurrences page link until per-occurrence detail is available in the queue target.
- Form compliance item: overdue campaign/occurrence summary from queue and facility compliance payload, with full form compliance page as a secondary action.
- Recent activity item: event summary and linked entity preview based on the embedded drill-down target.

## Actions Possible In Workspace

- Supported now: load note/corrective-action details, show server-derived state, show timelines, and expose the legacy full page link as secondary.
- Limited inline actions can be shown as server-backed buttons for loaded note and corrective-action details, using existing APIs where a safe one-step action is available.
- Deferred: complex assignment, verification, and form response flows remain full-page secondary paths until their forms are extracted safely.

## Components Not To Use As-Is

- `WorkspaceWidgetContainer` for Facility Workspace primary layout.
- `workspace-metric` for every KPI.
- `WorkspaceDrillDownLink` as the default interaction for queue rows.
- Traditional repeated card sections for notes/actions/forms/alerts.

## Proposed Design

The redesigned screen uses a facility-specific command surface on top of the existing Workspace Framework contract:

- Command Header: facility identity, status, refresh, time range, freshness, confidence, partial warning, action center toggle.
- Situation Overview: one dominant operational status panel and an operational pulse rail for notes, actions, escalations, and forms.
- Intervention Queue: compact selectable rows, keyboard navigable, with visual priority bands.
- Context Panel: in-workspace preview/detail area driven by URL query state.
- Internal navigation: overview, priorities, notes, actions, compliance, activity as in-page sections.

## Wireframes

Desktop:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│ Command Header: facility | status | period | refresh | actions          │
├─────────────────────────────────────────────────────────────────────────┤
│ Situation Overview 60%                         │ Intervention Queue 40% │
│ ┌ status / driver / change / pending action ┐  │ priority rows          │
│ └ pulse rail: notes/actions/escalations/forms┘ │ selected row           │
├─────────────────────────────────────────────────────────────────────────┤
│ activity / compliance / recent changes          │ Context Panel optional │
└─────────────────────────────────────────────────────────────────────────┘
```

Tablet:

```text
Command Header
Segmented navigation + filters
Situation Overview
Intervention Queue
Context Panel as overlay/split
```

Mobile:

```text
Command Header
Overview / Queue
Tap row -> detail focus in same route
Back button -> restores queue and filters
```

## Interaction Map

- Selecting a row updates `panel` and `entityId` in the URL.
- Closing the panel removes only panel parameters and keeps filters.
- Browser Back/Forward opens and closes the panel.
- Escape closes the panel.
- Focus moves into the panel and returns to the selected row on close.
- Full page links are secondary.

## Primary User States

1. Stable facility with no priority items: show calm status, empty queue, and recent activity.
2. Attention/intervention facility: status and queue dominate.
3. Critical facility: critical accent is limited to status marker and priority rows.
4. Partial data: command header warning and affected panel/section note.
5. Unauthorized entity preview: safe error inside panel; workspace remains loaded.

