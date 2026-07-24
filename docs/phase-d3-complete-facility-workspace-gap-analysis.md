# Phase D.3 Complete Facility Workspace Gap Analysis

Branch: `phase-d3-complete-facility-operations-workspace`

## Summary

Latest `main` includes the Workspace Framework, Facility Workspace MVP, and D.2 Command Center UX. The current Facility Workspace has real data for organization structure, notes, corrective actions, escalations/notifications, and form compliance. The repository does not currently contain domain models, DbSets, APIs, or permissions for inmates, occupancy capacity, staff readiness, vehicles, weapons, communication devices, equipment readiness, incidents as a standalone domain, risk treatment, projects, operational plans, emergency plans, decisions, directives, or tasks.

Phase D.3 therefore expands the Facility Workspace using real available data only and exposes unavailable domains as explicit data-quality gaps. It does not create mock production records or generic entities.

## Domain Gap Matrix

| Domain | Entity Exists | Real Data | API | Permission | Screen | Integrated Now | Gap | Migration Needed | Follow-up |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Facility | Yes | Yes | Yes | Organization/Workspace | Facilities | Yes | None | No | #11 |
| FacilityUnit | Yes | Yes when seeded | Organization lookup | Organization/Workspace | Lookup/admin | Yes | No capacity/current population fields | No | #11 |
| Building | Yes | Yes when seeded | No dedicated page | Organization/Workspace | No dedicated page | Count only | No operational readiness fields | No | #15 |
| FacilityAssetLocation | Yes | Yes when seeded | No dedicated page | Organization/Workspace | No dedicated page | Count only | No asset inventory/status model | No | #15 |
| Staff | No | No | No | No | No | Gap state | Workforce model absent | Yes, future | #15 |
| Inmate | No | No | No | No | No | Gap state | Occupancy and inmate movement absent | Yes, future | #124 |
| Vehicle | No | No | No | No | No | Gap state | Vehicle readiness absent | Yes, future | #15/new issue |
| Weapon | No | No | No | No | No | Gap state | Weapon/custody model absent | Yes, future | #15/new issue |
| CommunicationDevice | No | No | No | No | No | Gap state | Device readiness absent | Yes, future | #15/new issue |
| Equipment | No | No | No | No | No | Gap state | Equipment inventory/maintenance absent | Yes, future | #15/new issue |
| Incident/Occurrence | EscalationOccurrence only | Yes for escalations | Yes for escalations | Escalations | Escalation pages | Partial | No standalone incident domain | Yes, future | #127 |
| Risk/RiskTreatment | No | No | No | No | No | Gap state | Risk engine absent | Yes, future | #16 |
| Project/Initiative | No | No | No | No | No | Gap state | Project model absent | Yes, future | #126 |
| OperationalPlan/EmergencyPlan | No | No | No | No | No | Gap state | Plan readiness absent | Yes, future | #128 |
| Document | Attachment exists | Limited | Attachments | Attachment policy | Entity pages | Not promoted | No facility document registry | Future | #128 |
| Decision/Directive | No | No | No | No | No | Gap state | Decision model absent | Yes, future | #125 |
| Task | Assignments/actions exist | Yes in notes/actions | Notes/actions APIs | Domain permissions | Domain pages | Partial | No general task engine | Future | #11/#15 |

## Integratable Without Mock Data

- Facility header and context.
- Facility unit/building/location structure.
- Open notes, critical notes, overdue notes, unassigned notes, and note type buckets.
- Corrective action counts and valid average closure hours.
- Escalation occurrence counts and personal unread notifications.
- Form compliance metrics from the existing compliance service.
- Priority queue from notes, corrective actions, escalations, and overdue form assignments.
- Recent activity from notes, corrective actions, escalations, and form assignments.
- Data-quality status for every requested domain.

## Security and Performance Risks

- Missing domains must not be represented by notes or comments because that would blur authorization and audit boundaries.
- Data-quality gap states must not leak counts for unauthorized domains. Unavailable domains expose only architecture status, not operational data.
- Facility unit operational counts must be facility-scoped and reuse the existing scoped notes/actions filters.
- Priority and timeline remain bounded; missing domains do not trigger unbounded scans.
- No migration is required for this phase.

## Expected File Map

- Backend workspace DTOs, providers, read service, definitions, DI registration.
- Frontend `FacilityWorkspacePage.tsx`, API client types, command-center CSS, tests.
- D.3 documentation and screenshot artifacts.
