# Phase B.2.3.2 Baseline

## Branch

- Branch: `phase-b232-note-routing-auto-assignment`
- Base: `origin/main`
- Starting SHA: `bc175421ad3e85842b4bd1bd79967b682224c61a`

## Backend

- Build: Passed.
- Unit tests: 284 passed, 0 skipped.
- Integration tests: 73 passed, 0 skipped.
- `BASEERA_TEST_CONNECTION`: Provided from local secret environment and exported without a fixed database name. The integration fixture created isolated test databases.

## Frontend

- `npm ci --ignore-scripts`: Passed.
- Typecheck: Passed.
- Lint: Passed with existing Fast Refresh warnings in `src/auth/AuthProvider.tsx`.
- Tests: 98 passed, 0 skipped.
- Production build: Passed with non-secret local Entra validation values.
- `npm audit --audit-level=high`: Passed, 0 high/critical vulnerabilities.

## Existing Warnings

- Backend build emits existing `NU1510` warning for `System.Security.Cryptography.Xml`.
- Backend build emits existing nullable reference warning in `NoteTypeAccessService`.
- Frontend production build emits the existing Vite chunk size warning.

## Decision

Baseline accepted. Phase B.2.3.2 implementation may proceed.
