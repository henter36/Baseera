# Phase D0 Workspace Framework Gap Analysis

Issue: #10 - [Workspaces] Foundation - Decision Workspace Framework

## 1. Current Reusable Components

- Backend authorization already uses server-side permission codes, policies, and `ICurrentUser.HasPermission`.
- Organizational scope is centralized through `IOrganizationalScopeService`, note/form scope coordinators, and dashboard filter builders.
- Operational Dashboard already demonstrates scoped server-side aggregation, query cancellation, permission-specific sections, and bounded result sets.
- Observation Workspace provides a proven RTL master-detail pattern, compact list cards, sticky detail header, action bar, tabs, loading/error/empty states, and URL-backed filters.
- Form Compliance Dashboard provides scoped aggregation, reconciliation warnings, export limits, and real operational metrics.
- Frontend API client already centralizes stable DTOs and authenticated requests.
- React Query is already used for caching, retries, stale times, and keeping previous paged data.
- Existing CSS tokens cover cards, badges, fields, empty/error states, responsive grids, and RTL layout.

## 2. Components That Must Stay Domain-Specific

- Note workflow transitions, status labels, severity labels, row-version commands, and corrective-action routes remain in Notes/Corrective Actions.
- Observation timeline events are operational-note events and are not a generic immutable audit stream.
- Note assignment, verification, closure, escalation, and resource placeholders are not generalized into Workspace Core.
- Form compliance reconciliation and campaign/response semantics stay in Forms.
- Dashboard KPI definitions and period bucketing remain dashboard-specific widget data providers.

## 3. Current Duplication

- Dashboard, Observation Workspace, and Form Compliance each define their own header, filter state, loading/error/empty patterns, and action rendering.
- Allowed actions appear as different shapes: strings in notes/forms, implicit nav actions in dashboards, and future widgets need a single server-authored action contract.
- Freshness and confidence are not represented consistently; dashboard/forms expose generated timestamps but the UI interprets state locally.
- Drill-downs are ad hoc routes built in frontend helpers instead of a server-provided route-key contract.
- Responsive workspace/list-detail behavior exists only in Observation Workspace CSS.

## 4. Architectural Risks

- A generic Workspace Core can become a service locator or module dependency hub if it imports Notes/Forms details.
- Returning raw object payloads would weaken API contracts and frontend type-safety.
- Client-derived permissions or scope parameters would create IDOR risk.
- Unbounded widget orchestration can create N+1 and denial-of-service style query fan-out.
- Shared components can accidentally freeze Observation Workspace behavior if domain-specific assumptions leak into the shell.

## 5. Phase Boundaries

- This phase creates framework contracts, widget registry, orchestration, API, shared frontend shell components, and a feature-flagged reference workspace.
- Facility Workspace, Region Workspace, Headquarters Workspace, and full Decision Workspace are not implemented.
- Saved views persistence is documented as a schema boundary only; no new tables are added in D0.
- No AI, prediction, simulation, production mock data, or dynamic query execution is introduced.
- Observation Workspace behavior remains unchanged except for regression-safe reuse opportunities.

## 6. Expected File Map

- Backend contracts/services: `src/backend/Baseera.Application/Workspaces/*`
- Module widget registration: `src/backend/Baseera.Application/Dashboard/*Workspace*`
- API endpoints: `src/backend/Baseera.Api/Endpoints/ApiEndpoints.cs`
- Authorization/permissions: `IdentityEntities.cs`, `AuthorizationExtensions.cs`, seed reference permissions
- Frontend API types/client: `src/frontend/src/api/client.ts`
- Frontend shared workspace components: `src/frontend/src/workspaces/*`
- Reference page/route: `src/frontend/src/pages/workspaces/ReferenceWorkspacePage.tsx`, `App.tsx`
- Tests: `Baseera.UnitTests/Workspaces/*`, frontend workspace tests, Observation Workspace regression tests
- Docs: Phase D0 workspace framework documentation set

## 7. Architecture Decisions

- Workspace Core owns definitions, context normalization, data freshness, confidence, allowed action and drill-down contracts.
- Modules register widget definitions/providers through DI; Workspace Core depends only on interfaces.
- Widget payloads use typed contracts for the reference widgets and a bounded DTO envelope for transport.
- The server resolves level, scope, permissions, allowed actions, and widget visibility; the frontend only renders returned capabilities.
- Widget orchestration enforces a small query budget and converts safe widget failures into partial results.
- Reference workspace uses real scoped dashboard/note/corrective-action aggregates and is guarded by `Workspaces.View`.
- Shared frontend components are presentation-only and do not infer authorization or build privileged routes from untrusted data.
