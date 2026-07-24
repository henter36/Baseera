# Phase D.1 Facility Workspace Test Matrix

Backend unit:
- Facility workspace registration.
- Unsupported level rejection.
- Executive summary classification.
- Confidence degradation when form data has no targets.

Backend integration:
- Facility scoped user can load own facility workspace.
- Out-of-scope facility returns 404 without leaking the facility id.
- Missing workspace permission returns 403.

Frontend:
- Facility title and widgets render.
- Priority item drill-down link works.
- Date filters sync to URL and preserve facility id.

Full validation also runs existing Workspace Framework, Observation Workspace, Dashboard, Forms, Notes, and Corrective Actions tests.

