# Phase B.2.1 Corrective Actions Baseline

تاريخ خط الأساس: 2026-07-19

## Branch

- Repository: `henter36/Baseera`
- Working branch: `phase-b2-corrective-actions-core`
- Base branch: `origin/main`
- Start SHA: `12ab345777104e2cc4dca2a9177eeb5332c7a358`
- Reference Phase B.1 merge SHA: `12ab345777104e2cc4dca2a9177eeb5332c7a358`

## Initial failed baseline attempt

### Backend build

Command:

```bash
dotnet build src/backend/Baseera.slnx -c Release
```

Result: failed to complete locally. The command produced no diagnostic output for several minutes and was cancelled after approximately five minutes.

The same command was then run outside the managed sandbox because the first run appeared to be blocked by the execution environment. It completed successfully:

```text
Build succeeded.
Warnings: 2 occurrences of the same NU1510 warning
Errors: 0
Time Elapsed: 00:00:23.22
```

Second diagnostic command:

```bash
dotnet build src/backend/Baseera.slnx -c Release --no-restore -v minimal
```

Result: failed to complete locally. The command emitted the pre-existing warning below, then produced no further progress and was cancelled after approximately five minutes.

Pre-existing warning:

```text
Baseera.Api.csproj : warning NU1510: PackageReference System.Security.Cryptography.Xml will not be pruned. Consider removing this package from your dependencies, as it is likely unnecessary.
```

### Backend unit tests

Command:

```bash
dotnet test src/backend/tests/Baseera.UnitTests -c Release
```

Result:

```text
Passed: 236
Failed: 0
Skipped: 0
Total: 236
```

### Backend integration tests

Command:

```bash
dotnet test src/backend/tests/Baseera.IntegrationTests -c Release
```

Result:

```text
Passed: 0
Failed: 0
Skipped: 54
Total: 54
```

`BASEERA_TEST_CONNECTION` is not set in the current environment. The integration test assembly is configured to skip all integration tests when this variable is missing.

### Frontend

Frontend baseline commands were not run because the backend integration baseline did not meet the required skipped count of zero.

Expected baseline count from the phase brief: 78 frontend tests, skipped 0.

## Integration Test Connection Setup

Integration tests require `BASEERA_TEST_CONNECTION` to be exported in the same shell that runs `dotnet test`.

Use a connection string shape like this, with credentials loaded from a local secret source:

```bash
export BASEERA_TEST_CONNECTION='Server=<host>,<port>;User Id=<user>;Password=<from-secret-store>;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true'
```

The integration fixture replaces any supplied `Database` value with a unique per-run name. The example omits `Database` for clarity; providing one is optional and it will be overridden by the fixture.

Do not commit SQL Server passwords or real connection strings to Git.

## Successful baseline rerun

The baseline was rerun after loading the local developer environment from `$HOME/.baseera-dev.env`, starting the `baseera-sql` container, deriving the mapped SQL Server port from Docker, exporting `BASEERA_TEST_CONNECTION` with the fixture-controlled database behavior above, and verifying SQL Server readiness with `SELECT 1`.

### Backend build

Command:

```bash
dotnet build src/backend/Baseera.slnx -c Release --tl:off
```

Result:

```text
Build succeeded.
Warnings: 2 occurrences of the same NU1510 warning
Errors: 0
```

Pre-existing warning:

```text
Baseera.Api.csproj : warning NU1510: PackageReference System.Security.Cryptography.Xml will not be pruned. Consider removing this package from your dependencies, as it is likely unnecessary.
```

### Backend unit tests

Command:

```bash
dotnet test src/backend/tests/Baseera.UnitTests/Baseera.UnitTests.csproj -c Release --no-build --logger "console;verbosity=normal"
```

Result:

```text
Passed: 236
Failed: 0
Skipped: 0
Total: 236
```

### Backend integration tests

Command:

```bash
dotnet test src/backend/tests/Baseera.IntegrationTests/Baseera.IntegrationTests.csproj -c Release --no-build --logger "console;verbosity=normal"
```

Result:

```text
Passed: 54
Failed: 0
Skipped: 0
Total: 54
```

### Frontend

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

```text
npm ci: Passed
Typecheck: Passed
Lint: Passed with 2 pre-existing warnings in src/auth/AuthProvider.tsx
Test Files: 13 passed
Tests: 78 passed
Production build: Passed with non-secret local Entra validation values
npm audit: 0 vulnerabilities
```

## CI Status

Latest `main` CI runs checked with:

```bash
gh run list --repo henter36/Baseera --branch main --limit 5
```

Observed latest runs:

- `29696768230` - `Baseera CI` - `completed/success` - push to `main` at `2026-07-19T17:24:34Z`
- `29695679878` - `Baseera CI` - `completed/success` - push to `main` at `2026-07-19T16:51:20Z`
- `29682203948` - `Baseera CI` - `completed/success` - push to `main` at `2026-07-19T09:47:41Z`

## Baseline Decision

Baseline accepted. Phase B.2.1 implementation may proceed.
