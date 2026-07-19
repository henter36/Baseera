# Phase A.1 — Completion Report (updated)

**Date:** 2026-07-19  
**Branch:** `phase-a1-security-hardening`  
**PR:** https://github.com/henter36/Baseera/pull/2  
**Commit SHA:** `465db83faba7bcea09b227fd8378f6060f49034d`

---

## Final decision

# Phase A Accepted

Phase B may start after this report. Do not merge until review preference is confirmed; acceptance here is for the Phase A.1 security gate, not an instruction to merge.

---

## Evidence

### CI jobs (run `29679359977` on tip `465db83`, and PR run `29679575735`)

| Job | Result |
|-----|--------|
| secret-scan (gitleaks full history) | success |
| backend restore/build | success |
| NuGet High/Critical gate + fail-closed self-test | success |
| Unit tests | success — **53** passed, 0 failed, 0 skipped |
| Integration tests | success — **21** passed, 0 failed, 0 skipped |
| EF migrations | success |
| frontend (tsc/lint/test/prod-auth refuse/build/npm audit) | success — **7** vitest passed |

### Secrets / history

- Working tree and rewritten history: no non-placeholder SQL passwords (local scan `non_placeholder_password_hits=0`).
- Historical secret from original commit removed via `git filter-repo --replace-text` (values never reprinted).
- `.gitleaks.toml` no longer allowlists the historical commit.
- Gitleaks full-history scan succeeds in CI.
- GitHub Actions secret `BASEERA_CI_SA_PASSWORD` rotated to a new strong password (not the historical value).
- Operators must still rotate any **local/shared** SQL instances (e.g. `uqeb-sql`) if they reused the historical password.

### Correction-round controls

- Attachment `EnsureEntityInScopeAsync`: existence + DB-derived scope; orphan IDs rejected for Global/HQ; NotFound anti-enumeration.
- `StoragePathGuard` canonical relative-path checks (rejects `..`, rooted, sibling-prefix).
- National AuditLog requires `Audit.View` **and** Global/Headquarters scope.
- PrivilegeGuard: Global/HQ grants require Manage + Grant* + matching scope + admin role; target user validated.
- Entra Fail-Fast for Production/Staging; attachment storage health write/read/delete probe.
- NuGet gate: no `|| true`; tool failure fails the job; JSON High/Critical fail; fail-closed self-test script.

---

## Residual notes (non-blocking for Accepted)

- Bot review comments (CodeRabbit/Gemini/Sourcery) may remain on PR discussion; no unresolved human review-thread requirement open for gate closure.
- External `qlty` check may report separately; NuGet High/Critical gate in-repo is clean.
- Attachment malware scanner integration remains deferred with honest `PendingScan`.
- Rotate local Docker SQL SA if it still uses the historical password.

---

## Decision rationale

All mandatory Phase A.1 acceptance criteria that can be proven in-repo and in CI are met: history cleaned, secret scanning green, CI fully green including non-skipped integration tests, High/Critical NuGet clean, and the listed security control fixes verified by tests.
