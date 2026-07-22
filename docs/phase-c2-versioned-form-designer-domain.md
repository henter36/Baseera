# Phase C.2 — Domain: Versioned Form Designer

## Entities
- `FormVersion` — draftable schema with status machine and RowVersion.
- `FormSchemaSnapshot` — immutable canonical schema created on approve-lock.
- `FormVersionReviewDecision` — append-only version review history.
- `FormTemplate` — organization/department/private templates cloned from locked snapshots.
- `FormDefinition.CurrentLockedVersionId` — pointer to latest locked version; prior locked versions remain readable.

## Status machine
Draft → InReview → (ChangesRequested | Rejected | Locked)
ChangesRequested → InReview
Rejected → Draft
Locked — terminal for edits.

## Schema
Typed AST only (`FormSchemaDocument` … `FormFormulaNode`). No eval, dynamic, or executable expressions.
Issue #47 (publish / responses / campaigns) has **not** started.
