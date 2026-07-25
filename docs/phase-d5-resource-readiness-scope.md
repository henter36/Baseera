# Phase D.5 Resource Readiness Scope

Phase D.5 partially implements Issue #15 and continues Issue #11. It introduces the core asset slice of the Facility Resource Readiness Center.

Included: shared resource architecture, vehicles, communication devices, operational equipment, non-weapon security equipment, facility/fixed assets, status history, operational placement, maintenance work orders, requirements, readiness/gap calculations, bounded import, and Facility Workspace integration.

Excluded: workforce, weapons, ammunition, sensitive individual custody, procurement/finance, warehouse inventory, Region Workspace, Headquarters Workspace, AI, prediction, and full resource procurement workflows.

`Workspaces.ViewFacility` is not sufficient for resource data. Each resource widget, section, and endpoint requires `Resources.*` domain permissions.
