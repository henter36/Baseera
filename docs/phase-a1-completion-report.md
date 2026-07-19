# Phase A.1 — Completion Report (updated)

**Date:** 2026-07-19  
**Branch:** `phase-a1-security-hardening`  
**PR:** https://github.com/henter36/Baseera/pull/2  
**Commit SHA:** _(pending push of static-analysis closure)_

---

## Final decision

# Phase A Conditionally Accepted

Local verification for the final static-analysis / Gemini closure round is green. Final **Phase A Accepted** requires Baseera CI + Qlty/Sonar quality gates green on the pushed tip with Gemini High threads resolved. Phase B must not start until then. Do not merge this PR as part of this round.

---

## Final Static Analysis and Review Closure

### Findings before this round (Qlty/Sonar + Gemini)

| Source | Open items addressed |
|--------|----------------------|
| Gemini High | Dynamic magic-byte minimum; MSAL single-flight (`useEffect` + `loginEntra`) |
| Gemini Medium | Async attachment health I/O; async SHA-256 + caller |
| Qlty/Sonar-class smells (code review list) | Workflow least-privilege; Gitleaks HTTPS+checksum; `npm ci --ignore-scripts`; no `npx`; FrozenSet allowlists; `StoragePathGuard` split; PrivilegeGuard / AttachmentService complexity; Audit regex timeout fail-closed; Audit append-only helper; Organization module constant; LoginPage `type="button"`; pinned SQL/tools images |

Approximate open review findings closed in this round: **7 Gemini threads** + the CI/supply-chain and cognitive-complexity items listed in the user closure checklist. No rule suppressions (`NoWarn`, `SuppressMessage`, Sonar/Qlty disables, `continue-on-error`, or broad allowlists) were added.

### False positives

- None accepted as “Safe” without code change.  
- `mcr.microsoft.com/mssql-tools18:latest` is unpublished on MCR as of this round; replaced with digest-pinned `mcr.microsoft.com/mssql-tools@sha256:62556500…` (sqlcmd path `/opt/mssql-tools/bin/sqlcmd`).

### Review threads treated

| Thread topic | Fix | Primary tests |
|--------------|-----|---------------|
| Magic bytes min length | `GetRequiredSignatureLength` + dynamic check | `AttachmentRulesTests` (1–2 byte text; short PDF/JPEG; non-ZIP office) |
| MSAL init once | `ensureMsalInitialized` Promise single-flight | `msalInit.test.ts` |
| Health check async I/O | `WriteAllBytesAsync` / `ReadAllBytesAsync` + `finally` cleanup | `AttachmentStorageHealthCheckTests` |
| SHA-256 async | `ComputeSha256Async` + `UploadAsync` await | `AttachmentRulesTests.ComputeSha256Async_hashes_and_rewinds` |

### Files modified (this round)

- `.github/workflows/ci.yml`
- `scripts/check-nuget-vulnerabilities.sh`
- `src/backend/Baseera.Api/Authorization/AuthorizationExtensions.cs`
- `src/backend/Baseera.Api/Health/HealthExtensions.cs`
- `src/backend/Baseera.Application/Attachments/AttachmentRules.cs`
- `src/backend/Baseera.Application/Attachments/StoragePathGuard.cs`
- `src/backend/Baseera.Application/Security/PrivilegeGuard.cs`
- `src/backend/Baseera.Infrastructure/Attachments/AttachmentService.cs`
- `src/backend/Baseera.Infrastructure/Audit/AuditService.cs`
- `src/backend/Baseera.Infrastructure/Persistence/BaseeraDbContext.cs`
- `src/backend/Baseera.Infrastructure/Persistence/DatabaseInitializer.cs`
- `src/backend/tests/Baseera.UnitTests/*` (+ health/audit tests; Api project reference)
- `src/frontend/package.json` (`typecheck`)
- `src/frontend/src/auth/AuthProvider.tsx`, `msalInit.ts`, `msalInit.test.ts`
- `src/frontend/src/pages/LoginPage.tsx`

### Local test counts (this tip, pre-push)

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| Unit | **71** | 0 | **0** |
| Integration (`BASEERA_TEST_CONNECTION`) | **21** | 0 | **0** |
| Frontend (vitest) | **13** | 0 | **0** |

Also green locally: `dotnet build` Release; `npm ci --ignore-scripts` + `typecheck`/`lint`/`build`/`npm audit --audit-level=high`; NuGet gate + fail-closed self-test; gitleaks full history (no leaks).

### CI / Qlty / Sonar

- **Pending** on push of this tip to PR #2.  
- Workflow now: `permissions: contents: read` only (no unused `security-events: write`); gitleaks HTTPS-only + SHA-256; frontend without lifecycle scripts / without `npx`.

### Final commit SHA

Recorded after push in the Evidence section below.

---

## Evidence (prior green tip)

### CI jobs (historical tip `465db83` / `16cbec3`)

| Job | Result |
|-----|--------|
| secret-scan (gitleaks full history) | success |
| backend restore/build | success |
| NuGet High/Critical gate + fail-closed self-test | success |
| Unit tests | success (prior count 53) |
| Integration tests | success — **21** passed, 0 skipped |
| EF migrations | success |
| frontend | success (prior vitest 7) |

### Secrets / history

- Working tree and rewritten history: no non-placeholder SQL passwords.
- Historical secret removed via `git filter-repo --replace-text` (values never reprinted).
- Gitleaks full-history scan succeeds.
- GitHub Actions secret `BASEERA_CI_SA_PASSWORD` rotated.
- Operators must still rotate any **local/shared** SQL instances if they reused the historical password.

### Phase A.1 controls retained

- Attachment scope anti-enumeration; FrozenSet allowlists; dynamic magic bytes; async SHA-256.
- `StoragePathGuard` traversal / sibling / mixed-separator rejection.
- PrivilegeGuard hierarchy + Global/HQ grant rules (refactored, behavior preserved).
- AuditLog append-only (shared helper); secret redaction with regex timeout fail-closed.
- MSAL initialize single-flight; TestAuth/Seed fail-fast outside allowlisted environments.
- Attachment malware scanner remains deferred with honest `PendingScan`.

---

## Decision rationale

This closure round addresses the mandatory Gemini High findings and the listed static-analysis / supply-chain items without weakening Phase A.1 security controls. **Phase A Conditionally Accepted** until remote CI and Qlty/Sonar quality gates confirm the pushed tip; then upgrade to **Phase A Accepted**. Phase B remains blocked.
