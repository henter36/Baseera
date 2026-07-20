# Phase B.2.3.2 — Domain Model

New entities:
- `NoteRoutingRule`: soft-deletable, row-versioned routing rule scoped by `ScopeType`, `RegionId`, `FacilityId`, and `FacilityUnitId`.
- `NoteRoutingDecision`: append-only decision record for each routing attempt.
- `NoteRoutingRuleHistory`: append-only snapshot history for rule changes.
- `NoteTypeAccessChangeHistory`: append-only history for role grants and user overrides.

Updated entity:
- `NoteAssignment` includes optional `RoutingDecisionId` to link an automatic assignment to its decision.

Main constraints:
- Unique rule `Code`.
- Check constraints for routing scope shape.
- Check constraints for department vs role processing target.
- Unique `DecisionKey`.
- Restrict foreign keys.
- Append-only guards in `BaseeraDbContext` for decisions and histories.

Migration:
- `PhaseB232NoteRoutingAutoAssignment`.
