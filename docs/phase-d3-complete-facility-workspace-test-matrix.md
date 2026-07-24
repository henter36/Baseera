# Phase D.3 Test Matrix

Backend:

- Facility workspace definition includes structure and data-quality widgets.
- Focused widget providers call only their focused read service operation.
- Existing workspace framework, scope, permission, and query count tests remain applicable.

Frontend:

- Command Header and Intervention Queue regression.
- 12 section navigation buttons.
- `section=urgent` URL synchronization.
- Resources section unavailable state.
- Facility unit Context Panel.
- Data Quality gap Context Panel.
- Existing note/corrective action/escalation/form panel behavior.
- Date filters preserve facility context.
- Unauthorized users do not call the API.

Integration:

- Existing SQL Server workspace integration tests continue to validate 400/403/404, facility scope, widget authorization, and query count baseline.

