# Phase B.2.3.2 — Routing Resolution

Candidate rules must be active, not archived, match `NoteTypeId`, cover the note scope, and support the trigger.

Specificity order:
1. FacilityUnit.
2. Facility.
3. Region.
4. Headquarters.
5. Global.

Within the highest specificity, ordering is:
1. Lowest `Priority`.
2. `Code` ascending.
3. `Id` ascending.

FacilityUnit matching follows unit isolation: a unit-scoped rule covers only that unit. Facility-wide notes are covered by facility rules, not sibling unit rules.

Ambiguous active rules with identical note type, scope shape, and priority are rejected by the service before activation or update.
