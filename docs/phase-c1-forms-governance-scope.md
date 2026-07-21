# Phase C.1 — Forms Governance Scope

## Included

- `FormDefinition` aggregate with governance lifecycle (Draft → InReview → Approved/Rejected/ChangesRequested → Archived/Restore)
- Append-only `FormReviewDecision` audit trail for workflow transitions
- Org singleton `FormGovernancePolicy` (SoD, retention defaults, audit flags)
- `FormAccessGrant` with Allow/Deny, capability, optional scope window, soft revoke
- Server-side authorization stack: RBAC → organizational scope → classification redaction → form grants (deny > allow) → soft-delete filter → SoD on sensitive transitions
- EF migration `PhaseC1FormsGovernanceCore` with filtered unique index on `FormDefinition.Code`
- Full REST API under `/api/v1/forms` (definitions, workflow, review decisions, grants, governance policy, retention status)
- RTL frontend: list, create, edit, detail, review, access grants, governance settings
- Unit, integration, and frontend tests for C.1 behavior

## Excluded (Issues #46–#51)

- Form field schema / drag-and-drop designer / versioned designer
- Campaign publish, schedule, reminders, escalations
- Response fill/submit/approve, site compliance dashboard
- Reports/export execution (`Forms.Export` permission seeded only)
- AI features

## Explicit non-endpoints

No publish, respond, export, or field-designer routes in C.1.

## Acceptance criteria met

- Real SQL Server migration (no in-memory-only persistence)
- No hard delete on form operational records
- RowVersion concurrency (409 on mismatch)
- Out-of-scope IDs return **404**; in-scope missing permission returns **403**
- Audit events for create/update/workflow/grants/governance/sensitive view (no full sensitive payload)
- Loading/empty/error/forbidden/not-found/conflict states in UI
