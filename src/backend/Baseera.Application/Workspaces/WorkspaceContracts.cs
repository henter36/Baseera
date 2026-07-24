namespace Baseera.Application.Workspaces;

using Baseera.Application.Abstractions;

public enum WorkspaceLevel
{
    Facility = 1,
    Region = 2,
    Headquarters = 3,
    Domain = 4
}

public enum WidgetCategory
{
    Summary = 1,
    Risk = 2,
    Compliance = 3,
    Workload = 4,
    Timeline = 5
}

public enum WidgetSize
{
    Small = 1,
    Medium = 2,
    Large = 3,
    Wide = 4
}

public enum DataFreshnessStatus
{
    Current = 1,
    Delayed = 2,
    Stale = 3,
    Unknown = 4,
    Partial = 5
}

public enum ConfidenceLevel
{
    High = 1,
    Medium = 2,
    Low = 3,
    Unknown = 4
}

public sealed record WorkspaceContext(
    string WorkspaceKey,
    WorkspaceLevel Level,
    Guid? OrganizationId,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? EntityId,
    string UserScopeSummary,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string Locale,
    string TimeZone,
    IReadOnlySet<string> Permissions,
    bool IncludesSensitiveData);

public sealed record WorkspaceRequest(
    string WorkspaceKey,
    WorkspaceLevel? Level,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? EntityId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Locale,
    string? TimeZone);

public sealed record WorkspaceDefinition(
    string Key,
    string TitleAr,
    string TitleEn,
    IReadOnlySet<WorkspaceLevel> SupportedLevels,
    IReadOnlySet<string> RequiredPermissions,
    IReadOnlyList<string> RegisteredWidgets,
    WorkspaceLayoutDefinition DefaultLayout,
    IReadOnlyList<WorkspaceFilterDefinition> AvailableFilters,
    IReadOnlyList<DrillDownDefinition> SupportedDrillDowns,
    WorkspaceFeatureAvailability Features,
    int Version);

public sealed record WorkspaceLayoutDefinition(
    IReadOnlyList<WorkspaceLayoutItemDefinition> Items,
    int Version);

public sealed record WorkspaceLayoutItemDefinition(
    string WidgetKey,
    int Order,
    WidgetSize Size,
    bool IsPinned);

public sealed record WorkspaceFilterDefinition(
    string Key,
    string LabelAr,
    string Type,
    bool IsServerSide);

public sealed record DrillDownDefinition(
    string RouteKey,
    string LabelAr,
    string RequiredPermission);

public sealed record WorkspaceFeatureAvailability(
    bool SupportsSavedViews,
    bool SupportsWidgetConfiguration,
    bool SupportsExport,
    bool IsReferenceOnly);

public sealed record WidgetDefinition(
    string Key,
    string TitleAr,
    string TitleEn,
    string? DescriptionAr,
    WidgetCategory Category,
    IReadOnlySet<WorkspaceLevel> SupportedLevels,
    string? RequiredPermission,
    string? RequiredDataCapability,
    WidgetSize DefaultSize,
    WidgetSize MinSize,
    WidgetSize MaxSize,
    WidgetRefreshPolicy RefreshPolicy,
    WidgetDataFreshnessPolicy DataFreshnessPolicy,
    WidgetEmptyErrorBehavior EmptyErrorBehavior,
    bool SupportsDrillDown,
    bool IsConfigurable,
    bool ContainsSensitiveData,
    bool IsEnabled);

public sealed record WidgetRefreshPolicy(int MinimumRefreshSeconds, bool SupportsManualRefresh);

public sealed record WidgetDataFreshnessPolicy(int CurrentForSeconds, int DelayedAfterSeconds, int StaleAfterSeconds);

public sealed record WidgetEmptyErrorBehavior(string EmptyMessageAr, string ErrorMessageAr, bool AllowPartialFailure);

public sealed record WidgetDataEnvelope<TPayload>(
    string WidgetKey,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset? DataEffectiveAtUtc,
    DataFreshness Freshness,
    WidgetConfidence Confidence,
    WorkspaceScopeSummary ScopeSummary,
    bool IsPartial,
    IReadOnlyList<string> WarningMessages,
    TPayload Payload,
    IReadOnlyList<DrillDownTarget> DrillDownTargets,
    IReadOnlyList<WorkspaceAllowedAction> AllowedActions);

public sealed record WidgetDataEnvelopeDto(
    string WidgetKey,
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset? DataEffectiveAtUtc,
    DataFreshness Freshness,
    WidgetConfidence Confidence,
    WorkspaceScopeSummary ScopeSummary,
    bool IsPartial,
    IReadOnlyList<string> WarningMessages,
    object Payload,
    IReadOnlyList<DrillDownTarget> DrillDownTargets,
    IReadOnlyList<WorkspaceAllowedAction> AllowedActions);

public sealed record DataFreshness(DataFreshnessStatus Status, string LabelAr, string? ReasonAr);

public sealed record WidgetConfidence(ConfidenceLevel Level, string LabelAr, string? ReasonAr);

public sealed record WorkspaceScopeSummary(
    WorkspaceLevel Level,
    string LabelAr,
    Guid? RegionId,
    Guid? FacilityId,
    bool IsSensitive);

public sealed record WorkspaceAllowedAction(
    string Code,
    string LabelAr,
    bool Enabled,
    string? DisabledReasonAr,
    bool RequiresConfirmation,
    WorkspaceActionTarget? Target);

public sealed record WorkspaceActionTarget(
    string Kind,
    string? RouteKey,
    IReadOnlyDictionary<string, string> RouteParameters);

public sealed record DrillDownTarget(
    string RouteKey,
    string LabelAr,
    IReadOnlyDictionary<string, string> RouteParameters,
    IReadOnlyDictionary<string, string> PreservedFilters,
    string RequiredPermission);

public sealed record WorkspaceShellDto(
    WorkspaceDefinition Definition,
    WorkspaceContextDto Context,
    DateTimeOffset GeneratedAtUtc,
    DataFreshness Freshness,
    WidgetConfidence Confidence,
    IReadOnlyList<WorkspaceAllowedAction> AllowedActions,
    IReadOnlyList<WidgetDefinition> WidgetDefinitions,
    IReadOnlyList<WidgetDataEnvelopeDto> Widgets,
    IReadOnlyList<WorkspaceWidgetFailureDto> WidgetFailures,
    bool IsPartial);

public sealed record WorkspaceContextDto(
    string WorkspaceKey,
    WorkspaceLevel Level,
    Guid? OrganizationId,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? EntityId,
    string ScopeLabelAr,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string Locale,
    string TimeZone,
    bool IncludesSensitiveData);

public sealed record WorkspaceWidgetFailureDto(
    string WidgetKey,
    string MessageAr,
    bool IsPartialSafe);

public sealed record WorkspaceWidgetDataDto(
    WidgetDefinition Definition,
    WidgetDataEnvelopeDto Data);

public sealed record WorkspaceSavedViewSchema(
    string WorkspaceKey,
    int Version,
    IReadOnlyList<string> WidgetOrder,
    IReadOnlyList<string> HiddenWidgets,
    IReadOnlyList<string> PinnedWidgets,
    IReadOnlyDictionary<string, string> FilterSnapshot);

public interface IWorkspaceWidgetProvider
{
    WidgetDefinition Definition { get; }
    Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken);
}

public interface IWorkspaceDefinitionProvider
{
    WorkspaceDefinition Definition { get; }
}

public interface IWorkspaceRegistry
{
    IReadOnlyList<WorkspaceDefinition> ListWorkspaceDefinitions();
    WorkspaceDefinition? GetWorkspaceDefinition(string workspaceKey);
    IReadOnlyList<WidgetDefinition> GetWidgetDefinitions(string workspaceKey, WorkspaceLevel level);
    IWorkspaceWidgetProvider? GetWidgetProvider(string widgetKey);
}

public interface IWorkspaceQueryService
{
    Task<WorkspaceShellDto?> GetWorkspaceAsync(WorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WidgetDefinition>?> GetWidgetsAsync(WorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceWidgetDataDto?> GetWidgetAsync(WorkspaceRequest request, string widgetKey, CancellationToken cancellationToken = default);
}
