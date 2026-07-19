# Phase A.1 — Completion Report (updated)

**Date:** 2026-07-19  
**Branch:** `phase-a1-security-hardening`  
**PR:** https://github.com/henter36/Baseera/pull/2  
**Commit SHA:** `43c0f035fd379b21125394268c7027554a32a282`

---

## Final decision

# Phase A Accepted

Phase B may start after this report. Do **not** merge PR #2 as part of this round unless explicitly requested.

---

## Final Static Analysis and Review Closure

### Findings before this round

| Source | Count / note |
|--------|----------------|
| Gemini High/Medium threads | **7** open |
| Sonar new-code gate | Failed on **7.0%** duplication (EF migration Up/Down) |
| Sonar open issues on tip before final CPD fix | went to **0** issues / **0** vulns / hotspots reviewed **100%** once smells closed |
| Qlty | Check `success` with advisory text “3 blocking issues / vulnerabilities” (supply-chain patterns); gate itself passes |
| CodeRabbit | Major: zero-GUID Entra placeholders, http redirect; plus remaining ReadExactly / Reason / login write throttle |

### Closed

| Area | Result |
|------|--------|
| Gemini threads | **7/7** fixed, tested, replied, resolved |
| CI least privilege | `permissions: contents: read` only |
| Gitleaks | HTTPS-only + official checksums file verification; full history |
| Frontend supply chain | `npm ci --ignore-scripts`, `npm run typecheck`, no `npx` |
| Attachments / PrivilegeGuard / Audit / Health / MSAL | Implemented per checklist with tests |
| Sonar QG | **OK** on `a1c7f12` — duplication **1.0%** (≤3%), bugs/vulns/smells **0** |
| Baseera CI | **green** (`secret-scan`, `backend`, `frontend`) |
| Production Entra fail-closed | Rejects `YOUR_`, zero-GUIDs, non-HTTPS/localhost redirect |

### False positives / justified notes

- **EF migration CPD**: Automatic Analysis ignored broad `sonar.exclusions` for Migrations; fixed by **deduplicating Up/Down helpers** in code (not by disabling a Sonar rule).
- **Qlty “3 blocking issues” while check=`pass`**: advisory description remains; evidence in-repo shows HTTPS-only curl (`--proto-redir '=https'`), `--ignore-scripts`, and no `npx`. Sonar reports **0 vulnerabilities** on the PR.

### Review threads treated

| Topic | Tests / evidence |
|-------|------------------|
| Magic bytes dynamic min | `AttachmentRulesTests` (+ `ReadExactly`) |
| MSAL single-flight | `msalInit.test.ts` |
| Health async I/O | `AttachmentStorageHealthCheckTests` |
| SHA-256 async | `ComputeSha256Async_hashes_and_rewinds` |
| Entra placeholder/redirect | `authGuards.test.ts` + CI refuse steps |
| Login bookkeeping concurrency | throttle + ignore `DbUpdateConcurrencyException` |

### Test counts (tip verification)

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| Unit | **71** | 0 | **0** |
| Integration | **21** | 0 | **0** |
| Frontend | **16** | 0 | **0** |

### CI / Qlty / Sonar (tip `a1c7f12`)

| Gate | Result |
|------|--------|
| Baseera CI | success |
| SonarCloud Quality Gate | **OK** |
| Qlty check | **pass** |
| CodeRabbit | pass |
| Gemini High threads | resolved |

### Final commit SHA

`43c0f035fd379b21125394268c7027554a32a282`  
(Sonar QG first cleared on `a1c7f12`; this tip includes CodeRabbit closure fixes.)

---

## Residual notes

- Attachment malware scanner remains deferred (`PendingScan`).
- Rotate local SQL SA if it still used any historical password.
- Do not start Phase B until operators confirm merge preference; acceptance here is the Phase A.1 security gate.

---

## Decision rationale

All mandatory Phase A.1 acceptance criteria proven in-repo and on PR checks are met: secrets/history cleaned, CI green with zero skipped tests, Sonar QG green, Gemini High closed, supply-chain controls enforced without rule suppressions.
