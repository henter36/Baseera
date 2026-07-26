# Phase D.5.1 Workforce Privacy & Security

Rules:

- Facility scope is enforced server-side (`IOrganizationalScopeService`). Out-of-scope facility/member/unit → 404.
- Missing `Workforce.*` permission → 403. `Workspaces.ViewFacility` alone does not grant workforce access.
- Facility summary and coverage endpoints return aggregate counts only (no member display names). Integration tests assert seeded Arabic names are absent from summary JSON.
- `RestrictionCodes` on member detail require `Workforce.ViewSensitiveRestrictions`; otherwise codes are redacted (`null`).
- Availability stores operational restriction codes / reason codes — never medical diagnosis free text.
- Import stores batch metadata + counts only; raw file contents are not written to AuditLog.
- Audit actions are named (`WorkforceMemberCreated`, `DutyRosterPublished`, `WorkforceImportConfirmed`, …) with actor display metadata, not full PII dumps.
- Soft-delete + `RowVersion` on mutable entities; restrict deletes on FKs.

Not implemented: weapons/ammunition custody, payroll/HRIS exports as default UI, Region/HQ workforce aggregation, biometric raw stores.
