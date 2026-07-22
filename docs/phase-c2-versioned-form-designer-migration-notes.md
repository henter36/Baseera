# Phase C.2 Migration Notes

Migration: `20260722061010_PhaseC2VersionedFormDesigner`

Adds FormVersions, FormSchemaSnapshots, FormVersionReviewDecisions, FormTemplates, FormDefinitions.CurrentLockedVersionId.
Unique (FormDefinitionId, VersionNumber). Unique FormVersionId on snapshots.
Trigger `TR_FormSchemaSnapshots_Immutable` blocks UPDATE/DELETE.
Apply via normal `Database.MigrateAsync` from empty DB or from main after C.1.
