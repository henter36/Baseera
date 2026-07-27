# Phase D.5.2 — Sensitive Custody Scope

Phase D.5.2 adds a facility-scoped readiness center for weapons, ammunition, armories, custody transactions, inspections, inventory, import, reconciliation, and sensitive data quality.

In scope:

- independent `SensitiveCustody` domain, not an extension of `ResourceAsset`;
- append-only custody and ammunition ledgers;
- masked serial exposure by default, separate serial permission, and no raw serials in workspace/audit;
- Facility Workspace widget `facility.sensitive-custody`;
- server-side permissions under `SensitiveCustody.*`;
- SQL constraints, rowversion writes, bounded lists, and scoped endpoints.

Out of scope:

- procurement, finance, national licensing workflow, Region/HQ workspaces, AI, screenshots, and visual acceptance evidence.

Issue links: closes #140 when CI/Sonar pass; partially implements #15; continues #11.
