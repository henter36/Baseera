# Phase D.5.1 Workforce Migration

Migrations:

- `20260725180933_PhaseD51FacilityWorkforceReadiness`
- `20260725203357_PhaseD51WorkforceReconciliationExport`

The hardening branch does not squash, delete, or rewrite these migrations. No new migration is required for this pass because the changes are service/catalog/test/documentation hardening only.

Creates:

- `ShiftDefinitions`
- `WorkforceImportBatches`
- `WorkforceMembers`
- `WorkforceRoleDefinitions`
- `DutyRosters`
- `WorkforceAvailabilityEvents`
- `CriticalPositionRequirements`
- `StaffingRequirements`
- `WorkforceAssignments`
- `WorkforceQualifications`
- `WorkforceReadinessSnapshots`
- `DutyRosterAssignments`

Integrity highlights:

- Soft-delete + `RowVersion` on mutable tables; restrict FK deletes.
- Check constraints: midnight-crossing shifts, import batch counts/state/status, roster publish state, availability/assignment/requirement effective ranges, staffing quantity (`MinimumSafe ≤ Required`), no self-supervision, unit requires facility.
- Filtered unique indexes for employee numbers, external personnel ids, role codes, shift codes, roster day uniqueness, import idempotency.
- Operational indexes for facility coverage and snapshot lookup.

Additional D.5.1 reconciliation/export migration:

- Adds durable reconciliation resolution storage.
- Adds import/export support fields needed by the D.5.1 API contract.
- Preserves existing workforce table history.

`Down` paths drop only the D.5.1 additions created by their matching migration. These migrations do not create weapons, payroll, or Region/HQ workspace tables.

## Verification

Run before closing #133:

```bash
dotnet ef database update --project src/backend/Baseera.Infrastructure --startup-project src/backend/Baseera.Api --configuration Release
dotnet test src/backend/tests/Baseera.IntegrationTests/Baseera.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~IT31_migration_workforce_tables_exist"
```

For CI migration verification, apply migrations to a fresh database separate from the application and integration-test databases, then drop that verification database after the run.
