# CI Integration Performance

## Scope

Issue: [#135](https://github.com/henter36/Baseera/issues/135)

Branch: `ci/reuse-integration-fixtures`

Base SHA: `28ca0984f265261ebcaec29686009ef754bb7390`

## Baseline

Baseline command:

```bash
source "$HOME/.baseera-dev.env"
export BASEERA_TEST_CONNECTION="$BASEERA_CONNECTION"
/usr/bin/time -p dotnet test src/backend/tests/Baseera.IntegrationTests/Baseera.IntegrationTests.csproj -c Release --logger "console;verbosity=detailed"
```

Baseline results:

| Metric | Value |
| --- | ---: |
| Integration tests | 195 |
| Passed | 195 |
| Failed | 0 |
| Skipped | 0 |
| Test duration | 12.5403 minutes |
| Wall-clock | 970.99 seconds |
| Console log size | 49,568,979 bytes |
| `Baseera_Test_*` databases | 29 |
| Migration executions | 580 migration applications across 29 databases |
| Demo seed executions | 29, inferred from the old per-factory startup setting |

Latest `main` backend CI before this branch:

| Step | Duration |
| --- | ---: |
| Backend job | 6m12s |
| Restore | 14s |
| Build | 39s |
| Unit tests | 11s |
| Integration tests | 3m49s |
| Apply migrations | 29s |

Source: GitHub Actions run `30188503935`, backend job `89757297130`.

First PR CI run after collection fixtures, before sharding:

| Step | Duration |
| --- | ---: |
| Backend job | 5m29s |
| Restore | 11s |
| Build | 39s |
| Unit tests | 11s |
| Integration tests | 3m17s |
| Apply migrations | 27s |

This improved backend CI by about 11.6%, below the 30% target, so the conditional sharding round was enabled.

## Changes

The integration suite now uses four shared xUnit collections:

| Collection | Database |
| --- | --- |
| `integration-core` | one database |
| `integration-forms` | one database |
| `integration-operations` | one database |
| `integration-workforce` | one database |

Each collection applies migrations once, starts the API with migrations disabled, seeds the existing deterministic baseline once, captures baseline primary keys, and resets by deleting rows created after the baseline. Triggers and constraints are disabled only during test cleanup and re-enabled before each test runs.

The factory still supports independent databases for tests that need isolated command interceptors or concurrency behavior. These remain intentionally separate:

| Test area | Reason |
| --- | --- |
| query-count tests using `WithInterceptor` | command interception must be scoped to the factory under measurement |
| escalation recipient batching query-count test | user-scope command counter must observe only that scenario |
| background job lease concurrency test | uses independent factory to avoid cross-collection lock state |
| workforce query-count test | interceptor scope must remain isolated from other requests |

Two dashboard correctness tests that did not need interceptors were moved to the Operations collection.

## After

Local verification command:

```bash
source "$HOME/.baseera-dev.env"
export BASEERA_TEST_CONNECTION="$BASEERA_CONNECTION"
/usr/bin/time -p dotnet test src/backend/tests/Baseera.IntegrationTests/Baseera.IntegrationTests.csproj -c Release --no-build --no-restore --logger "console;verbosity=minimal" --blame-hang --blame-hang-timeout 10m
```

After results:

| Metric | Value |
| --- | ---: |
| Integration tests | 198 |
| Passed | 198 |
| Failed | 0 |
| Skipped | 0 |
| Test duration | 5m20s |
| Wall-clock | 362.77 seconds |
| Console log size | 494 bytes |
| `Baseera_Test_*` databases | 11 by construction: 4 collection databases and 7 isolated factories |
| Migration executions | 220 migration applications by construction: 20 migrations across 11 databases |
| Demo seed executions | 11 total startup seeds instead of 29 |

Local deltas:

| Metric | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Integration wall-clock | 970.99s | 362.77s | 62.64% |
| Test databases | 29 | 11 | 62.07% |
| Migration applications | 580 | 220 | 62.07% |
| Console log size | 49,568,979 bytes | 494 bytes | 99.99% |

## CI Changes

The backend workflow now:

- uses pinned `actions/cache` NuGet package caching;
- cancels older runs for the same PR/ref;
- pulls the pinned `mssql-tools` image once before SQL readiness polling;
- runs unit and integration tests with `--no-build --no-restore`;
- runs integration tests with the explicit csproj, minimal console logging, and `--blame-hang`;
- fails the job if integration tests report skipped tests;
- verifies migrations against a dedicated `Baseera_Migration_${GITHUB_RUN_ID}_${GITHUB_RUN_ATTEMPT}` database and drops it afterward.
- runs integration tests in four collection-aligned shards after the first CI run showed the backend job still missed the 30% wall-clock target.

Shard discovery was checked locally with `dotnet test --list-tests --filter ...`:

| Shard | Tests |
| --- | ---: |
| core | 30 |
| forms | 54 |
| operations | 91 |
| workforce | 23 |
| total | 198 |

## Duplicate Test Review

`scripts/analyze-integration-tests.py` generates `docs/integration-test-duplication-report.md`. The current report detected 198 integration test methods and records the top 50 suspected overlaps. No tests were deleted or merged in this round because the candidates differ by domain, permission, scope, endpoint, or scenario semantics.
