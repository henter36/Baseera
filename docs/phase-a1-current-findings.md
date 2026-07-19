# Phase A.1 — Current Findings (updated)

**Date:** 2026-07-19  
**Branch:** main (local workspace, A.1 hardening applied)  
**Scope:** Security, Authorization and Foundation Hardening

## Summary

Phase A.1 closed the Critical/High gaps discovered in the initial audit. Working tree no longer embeds SQL credentials; TestAuth/Demo Seed fail-fast outside Development/Testing; Global vs Headquarters semantics fixed; privilege escalation guarded; soft-delete filters, audit immutability, attachment allowlist/scan honesty, health readiness, Entra docs, and CI are in place.

---

## Findings status

| ID | Finding | Severity | Status |
|----|---------|----------|--------|
| F01 | Hardcoded SQL password fallback in tests | Critical | **Verified** — `BASEERA_TEST_CONNECTION` required; no fallback |
| F02 | Credential in git history (`38abac7`) | Critical | **Partially Implemented** — removed from tree; rotation required; gitleaks allowlist for that commit only |
| F03 | Defaults `UseTestAuth`/`DemoOrganization` true | Critical | **Verified** — defaults false in `appsettings.json` |
| F04 | No Production/Staging Fail-Fast | Critical | **Verified** — `EnvironmentSecurityGuard` + host tests |
| F05 | `X-Test-User` accepted broadly | Critical | **Verified** — handler rejects outside allowlist |
| F06 | Auto-provision active users | Critical | **Verified** — pre-provisioned only |
| F07 | `CanAccess` grants Global/HQ to any auth user | Critical | **Verified** — fixed + unit tests |
| F08 | HQ treated as Global | High | **Verified** |
| F09 | No privilege-escalation checks | Critical | **Verified** — `PrivilegeGuard` + tests |
| F10 | Frontend defaults to test/`dev-admin` | Critical | **Verified** — prod gate script; test headers only in DEV+test |
| F11 | Incomplete MSAL flow | High | **Verified** — popup login, silent token, logout, config error UI |
| F12 | Soft-delete filters missing | High | **Verified** — global filters + tests |
| F13 | AuditLog mutable via DbContext | High | **Verified** — SaveChanges reject + interceptor + tests |
| F14 | Attachment scan/scope/entity gaps | Critical | **Verified** — allowlist, PendingScan, scope checks, cross-scope test |
| F15 | NU1903 / OpenAPI / Cryptography.Xml | High | **Verified** — OpenAPI removed; Cryptography.Xml pinned ≥10.0.6 |
| F16 | Static `/health` | Medium | **Verified** — `/health/live` + `/health/ready` |
| F17 | No CI | High | **Verified** — `.github/workflows/ci.yml` + gitleaks |
| F18 | Manual Ensure-only authz | High | **Verified** — centralized permission policies on endpoints |
| F19 | Cascade / check constraints incomplete | Medium | **Verified** — check constraints + filtered unique indexes + Restrict FKs |
| F20 | Migrations always on startup | Medium | **Verified** — default false in production appsettings |
| F21 | Placeholder connection strings | Low | **Verified** |
| F22–F25 | Baseline coverage / Entra wiring / soft-delete fields | — | **Verified** / completed under A.1 |

---

## Credential rotation (mandatory operational step)

A SQL SA password previously used as a local docker/dev fallback exists in git history of commit `38abac7`. **Rotate that credential everywhere it may have been reused.** This document does not reprint the value.

---

## Residual risks

1. Git history still contains the old secret until history is rewritten (explicit ops decision) or the credential is fully rotated and retired.
2. Cascade delete policies on some organizational FKs may still need Restrict conversion in a follow-up migration.
3. Attachment malware scanning is honest (`PendingScan`) but a production scanner integration is deferred.
4. CI SQL service requires repository secret `BASEERA_CI_SA_PASSWORD`.
