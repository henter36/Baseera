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
