# Phase D0 Workspace Framework Architecture

Workspace Core lives in `Baseera.Application.Workspaces` and depends only on application abstractions:

- `WorkspaceDefinition` describes a workspace shell.
- `WidgetDefinition` describes a widget capability and rendering contract.
- `IWorkspaceWidgetProvider` loads one typed widget payload.
- `IWorkspaceRegistry` validates definitions and prevents duplicate keys.
- `WorkspaceContextResolver` normalizes request context and validates scope server-side.
- `WorkspaceQueryService` orchestrates authorized widgets with bounded fan-out and partial failure handling.

Modules register their own providers through DI. Dashboard currently registers the reference widgets; Workspace Core does not import Notes, Forms, or Dashboard-specific logic.

No database tables or migrations are added in D0.
