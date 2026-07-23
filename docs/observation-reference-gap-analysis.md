# Observation Reference Gap Analysis

## 1. Reference Components

The read-only reference project at `references/observation-system-reference/project-export` contains a mature Arabic RTL operations interface. The relevant observation workflow is concentrated in:

- `src/components/views/site-observations-view.tsx`: compact observation list, filters, selected detail panel, action dialogs, assignment, status updates, incident linking.
- `src/components/views/site-profile-view.tsx`: management bar, tabs, risks, observations, escalations, corrective actions, checklists, resources, timeline patterns.
- `src/components/views/field-operations-view.tsx`: user task/assignment execution pattern.
- `src/components/views/incident-room-view.tsx`: tabbed operational room with decisions, resources, communications, timeline.
- `src/components/views/smart-dashboard-view.tsx`, `operations-priority-board-view.tsx`, `notifications-view.tsx`, `risk-matrix-view.tsx`, `operations-map-view.tsx`: reusable density, badge, priority, notification, and contextual scanning patterns.
- `src/components/shared.tsx`: `ToneBadge`, `StatTile`, `EmptyState`, and skeleton patterns.
- `src/lib/operations-priority-engine.ts`, `notification-service.ts`, `notification-rules.ts`, `operational-checklist-service.ts`, `site-risk-service.ts`, `risk-thresholds.ts`, `aar-corrective-action-service.ts`, `assistant-ops-service.ts`: reference-only service ideas for prioritization, grouped notifications, checklists, site risk, AAR/CAPA, and governed assistant support.
- `DESIGN-INVENTORY.md`, `FEATURES-INVENTORY.md`, `SCREEN-MAP.md`, and `screenshots/05-site-observations.png`, `07-site-profile-overview.png`, `09-site-profile-checklists.png`, `15-site-profile-management-bar.png`: visual and functional anchors.

## 2. Current Baseera Coverage

Baseera already has:

- ASP.NET Core modular monolith with Domain/Application/Infrastructure/Api layers.
- `OperationalNote`, `NoteAssignment`, `NoteStatusHistory`, `CorrectiveAction`, `Attachment`, `EscalationOccurrence`, `Notification`, and immutable `AuditLog`.
- Server-side note filtering, pagination, sorting, scope filtering, note-type access, sensitive redaction, row-version concurrency, and workflow transitions.
- Separate pages for note list, note detail, create/edit, routing, corrective actions, attachments, notifications, and escalations.

Main gap: the daily operator experience is split across pages and uses a table list rather than the reference master-detail operational workspace.

## 3. Design Elements To Rebuild Closely

- Compact card list: `90-130px`, `p-3`, reference number, title, status, severity, overdue/location/assignee/update metadata, no full description.
- Master-detail layout: list and detail visible together on desktop.
- Selected card treatment: tinted background, stronger border, keyboard focus.
- Filter bar above list with text/status/severity/scope controls and quick boolean filters.
- Detail header with key badges and SLA/last-update summary.
- In-page action bar driven by backend `allowedActions`.
- Tabbed detail sections with lazy queries for larger content.
- Timeline style: compact vertical operational events, separate from security audit.
- Arabic RTL density and subdued operational palette.

## 4. Elements To Rebuild With Changes

- Reference dialogs become Baseera side sheets/inline panels where possible.
- Reference `status` mutation maps to Baseera state machine endpoints.
- Reference single `actions` text timeline maps to Baseera corrective actions and status history.
- Reference assignment field maps to Baseera current assignment plus assignment history.
- Reference incident/site links are shown as placeholders until Baseera has first-class note links.
- Dates remain UTC in storage and are displayed in `Asia/Riyadh`.
- Sensitive fields use Baseera redaction and permissions.

## 5. Elements Rejected

- No Prisma schema, migrations, seed data, Next.js API routes, NextAuth/session logic, reference env settings, or reference DB migrations.
- No static seeded observations as a substitute for Baseera APIs.
- No duplicate Observation Engine parallel to Baseera `OperationalNote`.
- No direct copy of reference React components because Baseera uses Vite/React, its own CSS tokens, permission model, and API contracts.

## 6. Conflicts With Issues #64-#85

- The workspace must not bypass scope isolation, note type access, routing decisions, or intake locks introduced by the note-engine issues.
- The UI must not duplicate workflow rules; transitions remain server-authoritative.
- Existing `Resolved` vs `Closed` distinction is currently incomplete in Baseera enum; this is documented as a workflow expansion risk.
- Multi-role assignments are richer than current `NoteAssignment`; Baseera currently supports one current user or department assignment. The workspace displays this honestly and leaves multi-assignment expansion as a model migration.
- Verification and closure separation exists for critical SoD; the workspace must not expose closure actions unless the API allows them.

## 7. Required Data Model

Current implementation can use:

- `OperationalNotes`
- `NoteAssignments`
- `NoteStatusHistories`
- `CorrectiveActions`
- `Attachments`
- `EscalationOccurrences`
- `Notifications`
- `AuditLogs` separately, never mixed with operational timeline

Future normalized additions:

- `NoteOperationalActions`
- `NoteResourceRequests`
- `NoteVerificationRecords`
- `NoteDecisionRecords`
- `NoteLinks`
- expanded `NoteAssignment` roles: owner, executor, team, verifier, closure approver, resource owner, delegate.

## 8. Required APIs

Implemented in Baseera style:

- `GET /api/v1/notes/workspace`: paged note list using existing server-side query.
- `GET /api/v1/notes/{id}/workspace`: detail bundle with summary, allowed actions, assignments, corrective actions, attachments metadata, timeline, and placeholder sections for future resources/decisions/links.
- Existing workflow endpoints remain the command surface: submit, assign, start-work, submit-for-verification, return-for-rework, verify-closure, reopen, cancel, corrective-actions, attachments.

## 9. Required Permissions

Server-side only:

- `Notes.View`
- `Notes.Create`
- `Notes.Update`
- `Notes.Assign`
- `Notes.StartWork`
- `Notes.SubmitForVerification`
- `Notes.ReturnForRework`
- `Notes.VerifyClosure`
- `Notes.Reopen`
- `Notes.Cancel`
- `CorrectiveActions.View/Create`
- `Attachments.Upload/Download`

The frontend only renders returned `allowedActions` and existing permissions; it does not decide workflow validity.

## 10. Technical And Security Risks

- IDOR if workspace aggregate loads child rows before verifying note scope. Mitigation: load note through existing note query/scope/type-access first.
- N+1 list loading. Mitigation: use existing paged projection and batch current-assignment lookup.
- Sensitive data leakage in timeline. Mitigation: use status history only and avoid audit payloads in public timeline.
- Concurrency conflicts on workflow commands. Mitigation: rowVersion remains required.
- Visual drift from reference. Mitigation: card dimensions, filter placement, split ratios, and tabs follow reference screenshots.

## 11. Phased Execution Plan

1. Add this gap analysis and visual comparison.
2. Add workspace query DTO/service endpoints over current domain.
3. Add frontend API types and workspace page with URL filters and selected note deep link.
4. Add compact card list, responsive master-detail layout, tabs, action bar, loading/empty/error states.
5. Add tests for server allowed actions/scope and frontend rendering/filter preservation.
6. Later phases: normalized resource requests, decision records, multi-role assignment engine, richer verification records, E2E happy/failure paths.

## 12. Expected Files To Modify

- `docs/observation-reference-gap-analysis.md`
- `src/backend/Baseera.Application/Notes/NoteWorkspaceDtos.cs`
- `src/backend/Baseera.Application/Notes/NoteWorkspaceQueryService.cs`
- `src/backend/Baseera.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `src/backend/Baseera.Api/Endpoints/ApiEndpoints.cs`
- `src/frontend/src/api/client.ts`
- `src/frontend/src/App.tsx`
- `src/frontend/src/pages/notes/ObservationWorkspacePage.tsx`
- `src/frontend/src/pages/notes/ObservationWorkspacePage.test.tsx`
- `src/frontend/src/index.css`

## Visual And Functional Comparison

### Nearly Identical

- Page starts with compact search/filter/action band.
- Desktop split uses a dense note list and same-page details.
- Cards show only operational scanning fields and no full description.
- Active item uses tinted background and stronger border.
- Detail area uses small badges, metadata grid, and tabbed sections.
- Timeline is compact and chronological.

### Similar With Baseera Changes

- Reference left/right split is adapted to Baseera 35%/65% requirement.
- Reference dialogs are represented as Baseera inline action prompts or existing command navigation where richer forms already exist.
- Reference statuses are mapped to Baseera numeric enum labels.
- Reference location code is replaced by Baseera region/facility labels or IDs until organization display projection is expanded.

### Not Transferred

- Next.js routes, Prisma, auth, seed data, map implementation, assistant calls, and reference CSS variables are not transferred.
- Operational modules outside notes are documented for reuse but not implemented in this note workspace phase.

### Wireframe

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Observation Workspace header + create button                                │
├─────────────────────────────────────────────────────────────────────────────┤
│ Search + status + severity + region + facility + quick scopes/flags          │
├────────────── 35% collapsible list ─────────────┬──────────── 65% detail ───┤
│ NOTE CARD 90-130px                              │ Sticky note header        │
│ ref | severity | status | overdue               │ allowed action bar        │
│ title max 2 lines                               │ tabs                      │
│ location | owner | due | open actions | update  │ active tab paged content  │
│ selected/hover/focus states                     │ timeline / assignments    │
└─────────────────────────────────────────────────┴───────────────────────────┘
```

### Approximate Dimensions

- Desktop list: `minmax(320px, 35%)`; detail: `minmax(0, 65%)`.
- Collapsed list: `56px`.
- Card: `min-height: 96px`, `max-height: 132px`, padding `12px`, gap `8px`.
- Filter bar: `44px` controls, wrapping on tablet.
- Detail header: sticky top inside workspace, `120-170px` depending on badges.
- Mobile: list first; selecting a note hides list and shows detail full width with a back button.

### Baseera Components Reused

- Current permission provider.
- `api` client and React Query.
- Existing enum label/tone helpers.
- Existing list utilities for date formatting and query errors.
- Existing CSS tokens: `--surface`, `--line`, `--brand`, `--danger`, `--ok`, `--muted`.
- Existing note workflow, assignment, corrective action, attachment, and history APIs.
