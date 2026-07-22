# Phase C.2 Security Review

Auth stack per operation: Authentication → RBAC → Scope → Classification → Form Allow/Deny → Version status → SoD → RowVersion → Schema validation.
New permissions: `Forms.CloneVersion`, `Forms.ViewVersionHistory`, `Forms.ManageTemplates`.
Snapshots: application SaveChanges guard + SQL Server trigger reject UPDATE/DELETE.
Audit events omit full schema JSON; hash + counts only.
