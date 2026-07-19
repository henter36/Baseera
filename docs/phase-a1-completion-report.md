# Phase A.1 — Completion Report

**Date:** 2026-07-19  
**Scope:** Security, Authorization and Foundation Hardening  
**Branch:** main (local workspace)

---

## Final decision

# Phase A Conditionally Accepted

Phase B (Notes / Assignments) **must not start** until this upgrades to **Phase A Accepted**.

### Conditions to upgrade to Accepted

1. **Rotate** the historical SQL SA credential that appeared in commit `38abac7` wherever it was reused (shared SQL containers / other projects). Do not commit the new secret.
2. Set GitHub Actions secret `BASEERA_CI_SA_PASSWORD` and confirm CI is green (unit + integration + frontend + gitleaks + vuln gate).
3. Run integration tests locally with `BASEERA_TEST_CONNECTION` against a dedicated test database and confirm all pass.
4. Confirm operators will not reuse the historical password in any environment.

---

## Problems discovered → remediation

| ID | Severity | Root cause | Remediation | Proof |
|----|----------|------------|-------------|-------|
| F01 | Critical | Password fallback in integration factory | Removed; require `BASEERA_TEST_CONNECTION` or skip | `BaseeraApiFactory`, `IntegrationConnectionFactAttribute` |
| F02 | Critical | Secret in git history | Tree cleaned; rotation required; gitleaks allowlist for that commit only | `.gitleaks.toml`, findings note (value not reprinted) |
| F03–F05 | Critical | TestAuth/Seed defaults & env bypass | Defaults false; Fail-Fast; handler env gate; boolean alone insufficient | `EnvironmentSecurityGuard`, `StartupGuardHostTests` |
| F06 | Critical | Auto-provision active users | Pre-provisioned only; reject unknown/pending/inactive/archived | `UserProvisioningService`, `UserProvisioningTests` |
| F07–F08 | Critical/High | Global≡HQ bug | Split national vs HQ access | `OrganizationalScopeService`, unit tests |
| F09 | Critical | No escalation checks | `PrivilegeGuard` + reason-required assigns | `PrivilegeGuardTests` |
| F10 | Critical | Frontend test default | Prod auth check script; DEV-only test headers | `check-production-auth.mjs`, `client.ts` |
| F11 | High | Incomplete MSAL | Popup login, silent token, logout, config error | `AuthProvider.tsx`, `docs/entra-id-configuration.md` |
| F12 | High | Missing soft-delete filters | Global query filters + filtered unique indexes | `BaseeraDbContext`, `SoftDeleteFilterTests`, migration |
| F13 | High | Mutable AuditLog | SaveChanges + interceptor reject Modified/Deleted | `AuditImmutabilityTests` |
| F14 | Critical | Attachment gaps | Allowlist, magic bytes, PendingScan, scope checks | `AttachmentService`, cross-scope integration test |
| F15 | High | NU1903 / OpenAPI / Cryptography.Xml | OpenAPI removed; pin `System.Security.Cryptography.Xml` 10.0.10 | `dotnet list … --vulnerable` clean |
| F16 | Medium | Static health | `/health/live`, `/health/ready` | `HealthExtensions` |
| F17 | High | No CI | GitHub Actions workflow | `.github/workflows/ci.yml` |
| F18 | High | Ensure-only authz | Permission policies on endpoints | `AuthorizationExtensions`, `ApiEndpoints` |
| F19 | Medium | Indexes/cascade | Filtered unique indexes + Restrict FKs | `PhaseA1FilteredIndexesAndRestrict` |
| F20 | Medium | Auto-migrate in prod | Default `ApplyMigrationsOnStartup=false` | `appsettings.json` |

---

## Files changed (primary)

- Backend security: `EnvironmentSecurityGuard`, `PrivilegeGuard`, `OrganizationalScopeService`, `UserProvisioningService`, `TestAuthHandler`, `Program.cs`
- Persistence: `BaseeraDbContext`, `EntityConfigurations`, migrations `PhaseA1Hardening`, `PhaseA1FilteredIndexesAndRestrict`
- Attachments / Audit / Health / Authz policies / Middleware
- Frontend: `AuthProvider`, `client.ts`, `LoginPage`, `scripts/check-production-auth.mjs`, `.env.production`
- CI: `.github/workflows/ci.yml`, `.gitleaks.toml`
- Docs: `phase-a1-current-findings.md`, `entra-id-configuration.md`, this report
- Tests: unit suite expanded (33 passing); integration suite expanded (skip without connection)

---

## Secret scan results

- Working tree: no embedded SQL passwords / client secrets (placeholders only).
- Git history: historical credential in `38abac7` (allowlisted in `.gitleaks.toml` after removal from tree).
- **Value not printed in this report.** Rotation remains mandatory.

---

## Package vulnerability results

```
dotnet list src/backend/Baseera.slnx package --vulnerable --include-transitive
```

All projects: **no vulnerable packages** after pinning `System.Security.Cryptography.Xml` to **10.0.10** (patches CVE-2026-33116 / related advisories).

---

## Migrations

- `InitialPhaseA`
- `PhaseA1Hardening` — `ProvisioningStatus`, UserScope check constraints
- `PhaseA1FilteredIndexesAndRestrict` — filtered unique indexes for soft-delete, Restrict on org/identity FKs

Deploy via `dotnet ef database update` with `BASEERA_CONNECTION` (not auto in Production by default). Helper: `scripts/deploy-db.sql`.

---

## CI

Workflow `.github/workflows/ci.yml` includes:

- Gitleaks
- Backend restore/build/unit/integration
- NuGet vulnerability gate (solution-wide)
- EF migrate on CI SQL
- Frontend npm ci / tsc / lint / test / prod-auth refuse / production build / npm audit

Requires repository secret: `BASEERA_CI_SA_PASSWORD`.

**CI not yet observed green on GitHub in this session** (condition #2).

---

## Test results (this session)

| Suite | Result |
|-------|--------|
| Backend unit | **33 passed** |
| Backend integration | **10 skipped** (`BASEERA_TEST_CONNECTION` unset) |
| Frontend vitest | **1 passed** |
| Frontend prod auth gate (`VITE_AUTH_MODE=test`) | **refused (exit 1)** as required |
| Frontend production build | **succeeded** with Entra placeholders |

---

## Residual risks

1. Historical secret until rotated / history rewritten.
2. Malware scanner integration deferred — status remains honest (`PendingScan` until Clean).
3. Last-global-admin removal guard is partial (self-assign blocked; dedicated revoke endpoint not yet present).
4. Entra tenant validation depends on correct production `AzureAd:*` configuration (documented, no real secrets in repo).

---

## Decision rationale

Controls required for production hardening are implemented in code and covered by unit tests. Acceptance criteria that require **operational confirmation** (credential rotation + live CI/integration against SQL) are not yet proven in this environment; therefore the only honest verdict is **Phase A Conditionally Accepted**, not Accepted.
