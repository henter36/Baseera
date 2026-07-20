# Phase B.2.3.2 — Note Routing Scope

Phase B.2.3.2 adds managed note routing rules and automatic assignment for operational notes after Phase B.2.3.1 note types and effective access.

Included:
- `NoteRoutingRule` management.
- Submit-time routing for `Draft -> Open`.
- Department and role-based processing targets.
- Default reviewer role metadata.
- Default due-date selection from user input, routing rule, then note type.
- Append-only routing decisions, rule history, and note-type access change history.
- Manual routing run and routing preview API.
- Limited routing effectiveness view.

Excluded:
- Dashboard, reports, export, email, SMS, push, AI, workflow designer, Phase B.3.1, and Phase C.

The implementation remains a modular monolith and uses SQL Server as the source of truth.
