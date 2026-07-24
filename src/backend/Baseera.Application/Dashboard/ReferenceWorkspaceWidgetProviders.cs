namespace Baseera.Application.Dashboard;

using Baseera.Application.Workspaces;
using Baseera.Domain.Identity;

public sealed class OperationalSummaryWorkspaceWidgetProvider(
    IOperationalDashboardQueryService dashboard,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = new(
        ReferenceWorkspaceDefinitionProvider.OperationalSummaryWidgetKey,
        "الملخص التشغيلي",
        "Operational Summary",
        "مؤشرات الملاحظات المفتوحة والمتأخرة ضمن نطاق المستخدم.",
        WidgetCategory.Summary,
        new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain, WorkspaceLevel.Facility, WorkspaceLevel.Region, WorkspaceLevel.Headquarters },
        PermissionCodes.DashboardViewOperational,
        "OperationalDashboard.Summary",
        WidgetSize.Wide,
        WidgetSize.Medium,
        WidgetSize.Wide,
        new WidgetRefreshPolicy(60, true),
        new WidgetDataFreshnessPolicy(300, 1800, 3600),
        new WidgetEmptyErrorBehavior("لا توجد بيانات تشغيلية ضمن النطاق.", "تعذر تحميل الملخص التشغيلي.", true),
        true,
        false,
        false,
        true);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var summary = await dashboard.GetSummaryAsync(ToDashboardQuery(context), cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        var payload = new ReferenceOperationalSummaryPayload(
            summary.Workload?.OpenTotal ?? 0,
            summary.Workload?.InProgress ?? 0,
            summary.Workload?.PendingVerification ?? 0,
            summary.Workload?.Unassigned ?? 0,
            summary.Workload?.RequiresRouting ?? 0,
            summary.Risk?.Overdue ?? 0,
            summary.Risk?.DueSoon ?? 0,
            summary.Risk?.CriticalOrHigh ?? 0);

        return WorkspaceContractFactory.Envelope(WorkspaceContractFactory.BuildRequest(
            context,
            Definition.Key,
            generatedAt,
            generatedAt,
            payload,
            [new DrillDownTarget("dashboard.operations", "فتح لوحة المتابعة", new Dictionary<string, string>(), PreserveFilters(context), PermissionCodes.DashboardViewOperational)]));
    }

    private static OperationalDashboardQuery ToDashboardQuery(WorkspaceContext context)
    {
        return new OperationalDashboardQuery
        {
            FromUtc = context.FromUtc,
            ToUtc = context.ToUtc,
            RegionId = context.RegionId,
            FacilityId = context.FacilityId
        };
    }

    private static IReadOnlyDictionary<string, string> PreserveFilters(WorkspaceContext context)
    {
        var filters = new Dictionary<string, string>
        {
            ["fromUtc"] = context.FromUtc.ToString("O"),
            ["toUtc"] = context.ToUtc.ToString("O")
        };
        if (context.RegionId.HasValue)
        {
            filters["regionId"] = context.RegionId.Value.ToString();
        }

        if (context.FacilityId.HasValue)
        {
            filters["facilityId"] = context.FacilityId.Value.ToString();
        }

        return filters;
    }
}

public sealed class CorrectiveActionsSummaryWorkspaceWidgetProvider(
    IOperationalDashboardQueryService dashboard,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = new(
        ReferenceWorkspaceDefinitionProvider.CorrectiveActionsWidgetKey,
        "الإجراءات التصحيحية",
        "Corrective Actions Summary",
        "ملخص الإجراءات التصحيحية المفتوحة والمتأخرة ضمن النطاق.",
        WidgetCategory.Workload,
        new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain, WorkspaceLevel.Facility, WorkspaceLevel.Region, WorkspaceLevel.Headquarters },
        PermissionCodes.DashboardViewCorrectiveActions,
        "OperationalDashboard.CorrectiveActions",
        WidgetSize.Medium,
        WidgetSize.Small,
        WidgetSize.Wide,
        new WidgetRefreshPolicy(60, true),
        new WidgetDataFreshnessPolicy(300, 1800, 3600),
        new WidgetEmptyErrorBehavior("لا توجد إجراءات تصحيحية ضمن النطاق.", "تعذر تحميل ملخص الإجراءات.", true),
        true,
        false,
        false,
        true);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var summary = await dashboard.GetSummaryAsync(new OperationalDashboardQuery
        {
            FromUtc = context.FromUtc,
            ToUtc = context.ToUtc,
            RegionId = context.RegionId,
            FacilityId = context.FacilityId
        }, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        var payload = new ReferenceCorrectiveActionsPayload(
            summary.CorrectiveActions?.Active ?? 0,
            summary.CorrectiveActions?.Overdue ?? 0,
            summary.CorrectiveActions?.PendingVerification ?? 0,
            summary.CorrectiveActions?.Reopened ?? 0,
            summary.CorrectiveActions?.NotesWithStalledActions ?? 0);

        return WorkspaceContractFactory.Envelope(WorkspaceContractFactory.BuildRequest(
            context,
            Definition.Key,
            generatedAt,
            generatedAt,
            payload,
            [new DrillDownTarget("corrective-actions.list", "فتح الإجراءات التصحيحية", new Dictionary<string, string>(), new Dictionary<string, string>(), PermissionCodes.CorrectiveActionsView)]));
    }
}
