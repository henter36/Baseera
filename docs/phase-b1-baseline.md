# Phase B.1 — Baseline (pre-implementation)

**Date:** 2026-07-19  
**Branch:** `phase-b1-notes-core`  
**Start SHA:** `5dc0b5b8efab8214c8a38cad839f8964f9f59f08`  
**Base:** `main` after merge of PR #2 (Phase A.1)

---

## Build

| Check | Result |
|-------|--------|
| `dotnet build src/backend/Baseera.slnx -c Release` | **Succeeded** (0 errors) |

### Pre-existing warnings (before B.1 code)

| Warning | Location | Notes |
|---------|----------|--------|
| **NU1510** | `Baseera.Api.csproj` — `System.Security.Cryptography.Xml` | Package may be pruned as unnecessary; kept pending B.1 vulnerability review (direct pin for High/Critical hygiene). |
| EF Core **10622** (expected at runtime) | `Role` global query filter vs required `RolePermission` / `UserRole` navigations | To be fixed in B.1 by adding compatible filters on join entities. |

---

## Tests (local)

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| Unit (`Baseera.UnitTests`) | **71** | 0 | **0** |
| Integration (`Baseera.IntegrationTests`) | **21** (after SQL container restart) | 0* | **0** |
| Frontend (`vitest`) | **16** | 0 | **0** |

\* First integration attempt failed with `Connection refused` because local Docker SQL (`uqeb-sql`) was stopped. After `docker start uqeb-sql` and readiness: **21 passed, 0 skipped**.

### Frontend toolchain

| Step | Result |
|------|--------|
| `npm ci --ignore-scripts` | OK (0 vulnerabilities) |
| `npm run typecheck` | OK |
| `npm run lint` | OK (existing AuthProvider export-components warnings only) |
| `npm test` | 16 passed |
| `npm run build` (Entra non-placeholder env) | OK |

---

## CI (main at merge tip)

Prior green evidence from Phase A.1 tip before merge; post-merge `main` inherits the same tree as PR #2 tip `f25aef0` + merge commit `5dc0b5b`.

---

## Decision

**Baseline accepted.** Proceed with Phase B.1 implementation on `phase-b1-notes-core`.

---

## NU1510 / `System.Security.Cryptography.Xml` follow-up (during B.1 implementation)

The direct pin on `System.Security.Cryptography.Xml` in `Baseera.Api.csproj` was removed to test
whether the NU1510 "package will not be pruned" warning could be cleared. Result:

- With the pin **removed**, `dotnet build` still emitted NU1510 for the *transitive* copy pulled in
  by `Microsoft.Identity.Web`, and `scripts/check-nuget-vulnerabilities.sh` **failed** — it resolved
  `System.Security.Cryptography.Xml 9.0.0`, which has two published High-severity advisories
  ([GHSA-37gx-xxp4-5rgx](https://github.com/advisories/GHSA-37gx-xxp4-5rgx),
  [GHSA-w3x6-4m5h-cxqf](https://github.com/advisories/GHSA-w3x6-4m5h-cxqf)). The failure surfaced via
  `Baseera.UnitTests`, which references `Baseera.Api` but (unlike `Baseera.IntegrationTests`) had no
  pin of its own to force NuGet's highest-wins resolution to a patched version.
- **Decision: restored the `System.Security.Cryptography.Xml` `Version="10.0.10"` pin** in
  `Baseera.Api.csproj` (with an inline comment explaining why). The NU1510 hygiene warning remains
  (harmless — it only means the package could theoretically be pruned once `Microsoft.Identity.Web`
  bumps its own dependency), but the vulnerability gate is green again.
- Re-ran `bash scripts/check-nuget-vulnerabilities.sh src/backend/Baseera.slnx` after restoring the
  pin: **"No High/Critical NuGet vulnerabilities reported."**
