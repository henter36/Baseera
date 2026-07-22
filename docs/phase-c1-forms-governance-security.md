# Phase C.1 — Forms Governance Security

## Authorization order (server-only)

1. **RBAC** — `RequireAuthorization` + `AuthPolicies.Forms*` per route
2. **Organizational scope** — `IFormScopeService` filters list queries and validates entity access (mirror Notes)
3. **Classification** — redact title/description when `Classification >= Confidential` and user lacks `Forms.ViewSensitive`; audit sensitive views when policy requires
4. **Form grants** — `FormAccessGrant` Allow/Deny with deny precedence; expired grants ignored; grant scope must ⊆ actor scope
5. **Soft delete** — global query filter; archived forms hidden unless restore path uses `FormDefinitionsIncludingDeleted`
6. **SoD** — `IFormSeparationOfDutiesService` on submit-review, request-changes, approve, reject per `FormGovernancePolicy`

## 404 vs 403

| Situation | HTTP |
|-----------|------|
| ID not found or outside organizational scope | 404 |
| In scope but missing RBAC/grant/capability | 403 |

Matches Notes `NoteAccessHelper` policy to prevent enumeration.

## Threat review

| Threat | Mitigation |
|--------|------------|
| IDOR / scope enumeration | Server-side scope on every read/write; 404 for out-of-scope IDs |
| Privilege escalation via grants | Grantor scope validation; deny > allow; ManageAccess permission required |
| Self-approval | SoD: creator ≠ reviewer, last editor ≠ approver, reviewer ≠ approver (policy flags) |
| Form-grant Deny bypass | `IFormEffectiveAccessService` on write paths; Deny beats Allow/RBAC for that capability |
| Mixed Facility/Unit scope leak | `FullFacilityIds` separate from `UnitIds`; unit scope never promotes facility to full |
| Restore to wrong status | Latest Archive decision `FromStatus` drives restore target |
| DELETE-with-body revoke | Replaced with `POST .../revoke` |
| Admin override abuse | Requires reason + `IsAdministrativeOverride` on review decision + audit |
| Concurrency tampering | RowVersion check → 409 |
| Retention bypass | `FormRetentionPolicyService` evaluates expiry; no hard delete API |
| Sensitive data leakage | Redaction in queries; no full sensitive payload in audit logs |
| Client-side auth only | UI permission gates are cosmetic; all routes enforce server policies |

## SoD defaults (seeded policy)

- `RequireSeparationOfDuties = true`
- `AllowDesignerToReviewOwnForm = false`
- `AllowReviewerToApproveOwnReview = false`
- `RequireReviewBeforeApproval = true`

## Audit events

FormDefinitionCreated, FormDefinitionUpdated, FormSubmittedForReview, FormReviewChangesRequested, FormReviewApproved, FormReviewRejected, FormArchived, FormRestored, FormAccessGranted, FormAccessDenied, FormAccessRevoked, FormGovernancePolicyUpdated, FormRetentionEvaluated, FormSensitiveViewed, FormAdministrativeOverride.

## Manual grep checklist (C.1)

- No hard delete on Forms domain services
- No unauthenticated Forms endpoints
- No `IgnoreQueryFilters` in Forms application layer (restore uses `IncludingDeleted` queryables)
- No mock data in production API path
- No TODO in C.1 scope files
