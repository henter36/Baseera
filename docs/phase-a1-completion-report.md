# Phase A.1 — Completion Report (updated)

**Date:** 2026-07-19  
**Branch:** `phase-a1-security-hardening`  
**PR:** https://github.com/henter36/Baseera/pull/1

---

## Final decision

# Phase A Conditionally Accepted

Phase B must not start until this upgrades to **Phase A Accepted**.

### Remaining gates for Accepted

1. Confirm CI is fully green after history rewrite + `BASEERA_CI_SA_PASSWORD` rotation (secret-scan, backend, frontend).
2. Confirm integration tests run with zero skips against SQL (`BASEERA_TEST_CONNECTION`).
3. Confirm operators completed rotation of the historical SQL credential everywhere it may have been reused (value never reprinted here).
4. Confirm gitleaks passes on full history without a commit allowlist for the old secret.

---

## This correction round

| Area | Change |
|------|--------|
| CI NuGet gate | `scripts/check-nuget-vulnerabilities.sh` — no `\|\| true`; tool failure fails job; JSON High/Critical fail; fail-closed self-test |
| History | `.gitleaks.toml` commit allowlist removed; filter-repo redaction prepared for historical password |
| Attachments | Always resolve entity existence + scope; orphan IDs rejected for Global/HQ; NotFound anti-enumeration |
| Paths | `StoragePathGuard` via `GetRelativePath` (rejects `..`, rooted, sibling prefix) |
| Audit | National audit requires `Audit.View` **and** Global/Headquarters scope |
| PrivilegeGuard | Global/HQ grants require Manage + specific Grant* + matching scope + admin role; target user validated |
| Health | Attachment probe write/read/delete; Entra Fail-Fast on Production/Staging |
| Tests | Expanded unit/integration/frontend coverage |

---

## Evidence snapshot (local session)

| Check | Result |
|-------|--------|
| Unit tests | **53 passed**, 0 failed, 0 skipped |
| Frontend vitest | re-run after auth guard fix |
| NuGet High/Critical | none reported by gate script |
| NuGet fail-closed self-test | passed (simulated tool exit 42) |
| Integration tests | require `BASEERA_TEST_CONNECTION` (not treated as success when skipped) |

---

## Residual risks

- Force-push after history rewrite required for remotes to drop the old blob.
- CI SQL service depends on repository secret `BASEERA_CI_SA_PASSWORD` being set to a **new** strong password (not the historical value).
- Attachment malware scanner still honest (`PendingScan`) until a real scanner is integrated.

---

## Decision rationale

Code and unit-level proofs for the correction round are in place. Full **Accepted** still requires live CI green + history purge verified by gitleaks + non-skipped integration run + operational rotation confirmation.
