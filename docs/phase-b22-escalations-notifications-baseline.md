# Phase B.2.2 Escalations and Notifications Baseline

تاريخ خط الأساس: 2026-07-20

## Starting Point

- Branch: `phase-b22-escalations-notifications-core`
- Base: latest `origin/main`
- Starting SHA: `35aa48d8861469c28aa3cee181ceb7bd52bf6c44`
- Accepted Phase B.2.1 merge SHA: `35aa48d8861469c28aa3cee181ceb7bd52bf6c44`

## Environment

Integration tests used a local SQL Server container with a connection string supplied through `BASEERA_TEST_CONNECTION`.

Safe example:

```bash
export BASEERA_TEST_CONNECTION='Server=<host>,<port>;User Id=<user>;Password=<from-secret-store>;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true'
```

The integration fixture creates a unique database per test run. Do not commit passwords or real connection strings.

## Backend Baseline

Command:

```bash
dotnet build src/backend/Baseera.slnx -c Release --tl:off
```

Result: Passed.

Existing warning:

- `NU1510` for `System.Security.Cryptography.Xml` in `Baseera.Api`; present before Phase B.2.2 implementation.

Commands:

```bash
dotnet test src/backend/tests/Baseera.UnitTests/Baseera.UnitTests.csproj -c Release
dotnet test src/backend/tests/Baseera.IntegrationTests/Baseera.IntegrationTests.csproj -c Release
```

Results:

- Unit: 268 passed, 0 failed, 0 skipped.
- Integration: 67 passed, 0 failed, 0 skipped.

Note: an initial in-sandbox test attempt failed because MSBuild named pipes and Docker socket access were blocked by the local sandbox. The same baseline passed outside the sandbox with the same repository state.

## Frontend Baseline

Commands:

```bash
cd src/frontend
npm ci --ignore-scripts
npm run typecheck
npm run lint
npm test
VITE_AUTH_MODE=entra \
VITE_ENTRA_CLIENT_ID=11111111-1111-4111-8111-111111111111 \
VITE_ENTRA_TENANT_ID=22222222-2222-4222-8222-222222222222 \
VITE_ENTRA_API_SCOPE=api://33333333-3333-4333-8333-333333333333/.default \
VITE_ENTRA_REDIRECT_URI=https://app.example.sa \
npm run build
npm audit --audit-level=high
```

Results:

- `npm ci --ignore-scripts`: Passed.
- Typecheck: Passed.
- Lint: Passed with pre-existing Fast Refresh warnings in `src/auth/AuthProvider.tsx`.
- Frontend tests: 91 passed, 0 failed, 0 skipped.
- Production build: Passed with non-secret Entra validation values.
- npm audit: 0 high or critical vulnerabilities.

## Decision

Baseline accepted. Phase B.2.2 implementation may proceed.
