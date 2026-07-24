# Phase D0 Workspace Framework RTL Walkthrough

Frontend shell behavior:

- `WorkspaceShell` renders `dir="rtl"` and Arabic labels.
- `WorkspaceHeader` shows level, scope label, freshness, confidence, generated time, and allowed actions.
- `WorkspaceWidgetContainer` provides consistent title, warning, freshness, drill-down, and sensitive indicators.
- `WorkspaceFilterBar` supports date range URL synchronization.
- `MasterDetailWorkspaceLayout` supports desktop split, tablet split, and mobile list/detail navigation.

Observation Workspace remains on its existing route and keeps its regression tests.
