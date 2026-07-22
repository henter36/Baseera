# Phase C.1 — Forms Governance Baseline

Branch: `phase-c1-forms-governance-foundation`
Start SHA: `66f35f3f0916e398df6a7318d7bff13b0cc85433` (`main` after PR #60 merge)

## Pre-C.1 gate baselines

| Gate | Result |
|------|--------|
| `dotnet build` Release | 0 errors, 3 pre-existing warnings |
| Unit tests | 348 passed, 0 skipped |
| Integration tests | 92 passed, 0 skipped (requires `BASEERA_TEST_CONNECTION` + SQL Server) |
| Frontend tests | 121 passed, 0 skipped |

### Pre-existing warnings (not introduced by C.1)

- `NU1510`: `System.Security.Cryptography.Xml` package prune warning in `Baseera.Api`
- `CS8619`: nullability mismatch in `NoteTypeAccessService.cs`

## Current patterns to follow

| Concern | Pattern |
|---------|---------|
| Permissions | `PermissionCodes` + `AuthPolicies` + `RequireAuthorization` per route |
| Scope | `INoteScopeService` / `IOrganizationalScopeService`; out-of-scope → 404 |
| Classification | Redact at query layer; `ViewSensitive` permission + audit on sensitive view |
| Audit | `IAuditService.WriteAsync` + append-only guard on `AuditLog` |
| Soft delete | `SoftDeletableEntity` + global query filter + `*IncludingDeleted` for restore |
| Concurrency | `RowVersion` on `EntityBase`; Base64 in API; 409 on mismatch |
| Seed | Idempotent `EnsureRolePermissionsAsync` in `DatabaseInitializer` |

## Existing Forms stubs

- Roles: `FormDesigner`, `FormReviewer` (seeded)
- Permissions (stub, to be replaced): `Forms.Design`, `Forms.Publish`, `Forms.Submit`, `Forms.Review`
- No Forms domain, API, or frontend yet

## Risks to avoid

- IDOR / scope enumeration (always filter server-side; 404 for out-of-scope)
- Client-side-only authorization
- Hard delete on operational records
- Self-approval without SoD checks
- Grant scope expansion beyond grantor scope
- N+1 queries / in-memory scope filtering
- `IgnoreQueryFilters` except documented restore/admin paths
- Sensitive data in audit logs or error messages

## Baseline decision

Accepted — proceed with Phase C.1 implementation.
