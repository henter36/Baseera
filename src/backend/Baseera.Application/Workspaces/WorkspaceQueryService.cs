namespace Baseera.Application.Workspaces;

using Baseera.Application.Abstractions;
using Microsoft.Extensions.Logging;

public sealed class WorkspaceQueryService(
    IWorkspaceRegistry registry,
    WorkspaceContextResolver contextResolver,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<WorkspaceQueryService> logger) : IWorkspaceQueryService
{
    private const int WidgetQueryBudget = 8;

    public async Task<WorkspaceShellDto?> GetWorkspaceAsync(WorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var definition = registry.GetWorkspaceDefinition(request.WorkspaceKey);
        if (definition is null)
        {
            return null;
        }

        var context = await contextResolver.ResolveAsync(definition, request, cancellationToken);
        var authorizedDefinitions = AuthorizedWidgets(definition.Key, context).Take(WidgetQueryBudget).ToList();
        var widgets = new List<WidgetDataEnvelopeDto>(authorizedDefinitions.Count);
        var failures = new List<WorkspaceWidgetFailureDto>();

        foreach (var widgetDefinition in authorizedDefinitions)
        {
            var provider = registry.GetWidgetProvider(widgetDefinition.Key);
            if (provider is null)
            {
                continue;
            }

            try
            {
                widgets.Add(await provider.LoadAsync(context, cancellationToken));
            }
            catch (Exception ex) when (widgetDefinition.EmptyErrorBehavior.AllowPartialFailure)
            {
                logger.LogWarning(
                    ex,
                    "Workspace widget failed safely. Workspace={WorkspaceKey} Widget={WidgetKey} CorrelationId={CorrelationId}",
                    context.WorkspaceKey,
                    widgetDefinition.Key,
                    currentUser.CorrelationId);
                failures.Add(new WorkspaceWidgetFailureDto(widgetDefinition.Key, widgetDefinition.EmptyErrorBehavior.ErrorMessageAr, true));
            }
        }

        var generatedAt = timeProvider.GetUtcNow();
        return new WorkspaceShellDto(
            definition with { RegisteredWidgets = authorizedDefinitions.Select(w => w.Key).ToList() },
            ToDto(context),
            generatedAt,
            WorkspaceContractFactory.Freshness(generatedAt, generatedAt),
            WorkspaceContractFactory.Confidence(ConfidenceLevel.High, null),
            BuildWorkspaceActions(definition),
            authorizedDefinitions,
            widgets,
            failures,
            failures.Count > 0);
    }

    public async Task<IReadOnlyList<WidgetDefinition>?> GetWidgetsAsync(WorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var definition = registry.GetWorkspaceDefinition(request.WorkspaceKey);
        if (definition is null)
        {
            return null;
        }

        var context = await contextResolver.ResolveAsync(definition, request, cancellationToken);
        return AuthorizedWidgets(definition.Key, context).ToList();
    }

    public async Task<WorkspaceWidgetDataDto?> GetWidgetAsync(WorkspaceRequest request, string widgetKey, CancellationToken cancellationToken = default)
    {
        var definition = registry.GetWorkspaceDefinition(request.WorkspaceKey);
        if (definition is null)
        {
            return null;
        }

        var context = await contextResolver.ResolveAsync(definition, request, cancellationToken);
        var widgetDefinition = AuthorizedWidgets(definition.Key, context).FirstOrDefault(widget => widget.Key.Equals(widgetKey, StringComparison.OrdinalIgnoreCase));
        if (widgetDefinition is null)
        {
            return null;
        }

        var provider = registry.GetWidgetProvider(widgetDefinition.Key);
        if (provider is null)
        {
            return null;
        }

        return new WorkspaceWidgetDataDto(widgetDefinition, await provider.LoadAsync(context, cancellationToken));
    }

    private IEnumerable<WidgetDefinition> AuthorizedWidgets(string workspaceKey, WorkspaceContext context)
    {
        return registry.GetWidgetDefinitions(workspaceKey, context.Level)
            .Where(widget => string.IsNullOrWhiteSpace(widget.RequiredPermission) || currentUser.HasPermission(widget.RequiredPermission));
    }

    private IReadOnlyList<WorkspaceAllowedAction> BuildWorkspaceActions(WorkspaceDefinition definition)
    {
        var actions = new List<WorkspaceAllowedAction>();
        if (currentUser.HasPermission(Baseera.Domain.Identity.PermissionCodes.WorkspacesConfigureOwnView))
        {
            actions.Add(new WorkspaceAllowedAction(
                "CONFIGURE_OWN_VIEW",
                "تخصيص العرض",
                definition.Features.SupportsSavedViews,
                definition.Features.SupportsSavedViews ? null : "حفظ العروض مؤجل إلى Issue #21.",
                false,
                null));
        }

        return actions;
    }

    private static WorkspaceContextDto ToDto(WorkspaceContext context)
    {
        return new WorkspaceContextDto(
            context.WorkspaceKey,
            context.Level,
            context.OrganizationId,
            context.RegionId,
            context.FacilityId,
            context.EntityId,
            context.UserScopeSummary,
            context.FromUtc,
            context.ToUtc,
            context.Locale,
            context.TimeZone,
            context.IncludesSensitiveData);
    }
}
