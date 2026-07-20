# Phase B.2.3.1 Baseline

## Branch

- Branch: `phase-b231-note-types-access-intake`
- Base: `origin/main`
- Starting SHA: `389f530d74b9cc8de7c9ac1483fd2401394f8795`

## Backend

- Build: Passed.
- Unit tests: 278 passed, 0 skipped.
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
- Frontend production build emits the existing Vite chunk size warning.

## Decision

Baseline accepted. Phase B.2.3.1 implementation may proceed.
