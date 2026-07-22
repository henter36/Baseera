# Phase C.1 — Forms Governance API Contract

Base path: `/api/v1/forms`  
Auth: Bearer (Entra) or TestAuth in Development.  
Errors: ProblemDetails; validation → **400**; in-scope forbidden → **403**; missing/out-of-scope → **404**; RowVersion / invalid transition → **409**.

## Endpoints

| Method | Path | Permission |
|--------|------|------------|
| GET | `/api/v1/forms` | Forms.View |
| GET | `/api/v1/forms/{id}` | Forms.View |
| POST | `/api/v1/forms` | Forms.Create |
| PUT | `/api/v1/forms/{id}` | Forms.UpdateDraft |
| POST | `/api/v1/forms/{id}/submit-review` | Forms.SubmitForReview |
| POST | `/api/v1/forms/{id}/request-changes` | Forms.RequestChanges |
| POST | `/api/v1/forms/{id}/approve` | Forms.Approve |
| POST | `/api/v1/forms/{id}/reject` | Forms.Reject |
| POST | `/api/v1/forms/{id}/archive` | Forms.Archive |
| POST | `/api/v1/forms/{id}/restore` | Forms.Restore |
| GET | `/api/v1/forms/{id}/review-decisions` | Forms.View |
| GET | `/api/v1/forms/{id}/retention-status` | Forms.View |
| GET | `/api/v1/forms/{id}/access-grants` | Forms.ManageAccess |
| POST | `/api/v1/forms/{id}/access-grants` | Forms.ManageAccess |
| POST | `/api/v1/forms/{id}/access-grants/{grantId}/revoke` | Forms.ManageAccess |
| GET | `/api/v1/forms/governance-policy` | Forms.ManageGovernance |
| PUT | `/api/v1/forms/governance-policy` | Forms.ManageGovernance |

Workflow/archive/restore/grant-revoke bodies use `FormTransitionRequest` (`reason`, `rowVersion` where applicable).

## List query parameters

`page`, `pageSize`, `search`, `status`, `classification`, `regionId`, `facilityId`, `sortBy`, `sortDesc`.

Server-side scope filter applied before pagination. Sensitive fields redacted in list/detail when user lacks `Forms.ViewSensitive`.

## State machine

| From | To | Trigger |
|------|-----|---------|
| Draft | InReview | submit-review |
| InReview | Approved | approve |
| InReview | ChangesRequested | request-changes |
| InReview | Rejected | reject |
| ChangesRequested | InReview | submit-review |
| Approved | Archived | archive |
| Rejected | Draft | reject policy (default rework) |
| Rejected | Archived | reject policy |
| Archived | Approved or Rejected | restore (prior status from latest Archive decision) |

No direct status mutation via PUT.

## DTO highlights

- `RowVersion`: Base64 byte array on mutable aggregates
- `FormDetailDto.AllowedActions`: server-computed action allowlist for UI gating
- `FormAccessGrantDto`: principal type (User/Role), capability, effect (Allow/Deny), optional scope + validity window
- `FormGovernancePolicyDto`: singleton org policy with retention and SoD flags

## Enums (numeric JSON)

`FormDefinitionStatus`, `FormReviewDecisionType`, `FormAccessCapability`, `FormAccessGrantEffect`, `FormAccessGrantPrincipalType`, `ClassificationLevel`, `ScopeType`.

## Not implemented in C.1

Publish, respond, export, field schema/designer endpoints.
