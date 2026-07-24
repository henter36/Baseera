# Phase D.3 Complete Facility Workspace Scope

Phase D.3 continues implementation of Issue #11 by expanding the Facility Workspace into a broader prison operations workspace. It remains facility-only and does not implement Region or Headquarters workspaces.

In scope:

- Internal section navigation for all requested prison operation domains.
- Real facility structure from `FacilityUnit`, `Building`, and `FacilityAssetLocation`.
- Existing notes, corrective actions, escalations, notifications, and form compliance.
- Multi-domain data-quality visibility.
- Context Panel support for available entities and safe gap previews for unavailable domains.
- URL state for `section`, `panel`, and `entityId`.

Out of scope:

- New resource, risk, project, plan, decision, incident, inmate, or staff persistence models.
- AI, prediction, optimization, or simulation.
- Region and Headquarters workspaces.
- Mock production data.

