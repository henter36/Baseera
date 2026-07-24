namespace Baseera.Application.Workspaces;

using Microsoft.Extensions.Logging;

public sealed class WorkspaceRegistry : IWorkspaceRegistry
{
    private readonly IReadOnlyDictionary<string, WorkspaceDefinition> workspaces;
    private readonly IReadOnlyDictionary<string, IWorkspaceWidgetProvider> widgets;
    private readonly ILogger<WorkspaceRegistry> logger;

    public WorkspaceRegistry(
        IEnumerable<IWorkspaceDefinitionProvider> workspaceProviders,
        IEnumerable<IWorkspaceWidgetProvider> widgetProviders,
        ILogger<WorkspaceRegistry> logger)
    {
        this.logger = logger;
        workspaces = BuildWorkspaceMap(workspaceProviders);
        widgets = BuildWidgetMap(widgetProviders);
        ValidateWorkspaceWidgets();
    }

    public IReadOnlyList<WorkspaceDefinition> ListWorkspaceDefinitions() => workspaces.Values.OrderBy(w => w.Key).ToList();

    public WorkspaceDefinition? GetWorkspaceDefinition(string workspaceKey)
    {
        return workspaces.TryGetValue(NormalizeKey(workspaceKey), out var definition) ? definition : null;
    }

    public IReadOnlyList<WidgetDefinition> GetWidgetDefinitions(string workspaceKey, WorkspaceLevel level)
    {
        var workspace = GetWorkspaceDefinition(workspaceKey);
        if (workspace is null || !workspace.SupportedLevels.Contains(level))
        {
            return [];
        }

        return workspace.RegisteredWidgets
            .Select(GetWidgetProvider)
            .OfType<IWorkspaceWidgetProvider>()
            .Select(provider => provider.Definition)
            .Where(definition => definition.IsEnabled && definition.SupportedLevels.Contains(level))
            .ToList();
    }

    public IWorkspaceWidgetProvider? GetWidgetProvider(string widgetKey)
    {
        return widgets.TryGetValue(NormalizeKey(widgetKey), out var provider) ? provider : null;
    }

    private static IReadOnlyDictionary<string, WorkspaceDefinition> BuildWorkspaceMap(IEnumerable<IWorkspaceDefinitionProvider> providers)
    {
        var definitions = providers
            .Select(provider => provider.Definition)
            .ToArray();
        var map = new Dictionary<string, WorkspaceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            var key = NormalizeKey(definition.Key);
            if (!map.TryAdd(key, definition))
            {
                throw new InvalidOperationException($"Duplicate workspace key '{definition.Key}'.");
            }
        }

        return map;
    }

    private static IReadOnlyDictionary<string, IWorkspaceWidgetProvider> BuildWidgetMap(IEnumerable<IWorkspaceWidgetProvider> providers)
    {
        var map = new Dictionary<string, IWorkspaceWidgetProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            var key = NormalizeKey(provider.Definition.Key);
            if (!map.TryAdd(key, provider))
            {
                throw new InvalidOperationException($"Duplicate workspace widget key '{provider.Definition.Key}'.");
            }
        }

        return map;
    }

    private void ValidateWorkspaceWidgets()
    {
        foreach (var workspace in workspaces.Values)
        {
            foreach (var widgetKey in workspace.RegisteredWidgets)
            {
                var provider = GetWidgetProvider(widgetKey);
                if (provider is null)
                {
                    logger.LogWarning("Workspace {WorkspaceKey} references missing widget {WidgetKey}.", workspace.Key, widgetKey);
                    continue;
                }

                var overlaps = workspace.SupportedLevels.Intersect(provider.Definition.SupportedLevels).Any();
                if (!overlaps)
                {
                    throw new InvalidOperationException($"Widget '{widgetKey}' does not support any level for workspace '{workspace.Key}'.");
                }
            }
        }
    }

    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();
}
