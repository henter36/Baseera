namespace Baseera.Application.Workspaces;

using Baseera.Domain.Identity;
using static Baseera.Application.Workspaces.FacilityWorkspaceWidgetProviderSupport;

internal sealed class FacilityHeaderWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.HeaderWidgetKey,
        "تعريف السجن",
        "Facility Context",
        "اسم السجن والمنطقة والفترة الحالية.",
        WidgetCategory.Summary,
        PermissionCodes.WorkspacesViewFacility,
        "Facility.Context",
        WidgetSize.Wide);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var facility = await readService.GetFacilityAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        var payload = new FacilityWorkspaceHeaderPayload
        {
            FacilityId = facility.FacilityId,
            FacilityNameAr = facility.FacilityNameAr,
            RegionId = facility.RegionId,
            RegionNameAr = facility.RegionNameAr,
            FacilityType = facility.FacilityType,
            FromUtc = context.FromUtc,
            ToUtc = context.ToUtc,
            CalculatedAtUtc = generatedAt
        };

        return Envelope(context, Definition.Key, generatedAt, payload);
    }
}

internal sealed class FacilityExecutiveSummaryWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.ExecutiveSummaryWidgetKey,
        "الملخص التشغيلي",
        "Executive Facility Summary",
        "تقييم deterministic للحالة الحالية دون ذكاء اصطناعي.",
        WidgetCategory.Summary,
        PermissionCodes.DashboardViewOperational,
        "Facility.ExecutiveSummary",
        WidgetSize.Large);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var metrics = await readService.GetMetricsAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        var status = FacilityWorkspaceRules.ClassifyStatus(metrics);
        var payload = new FacilityExecutiveSummaryPayload
        {
            StatusCode = status.Code,
            StatusAr = status.LabelAr,
            PriorityIssues = FacilityWorkspaceRules.PriorityIssueCount(metrics),
            TopDriverAr = FacilityWorkspaceRules.TopDriver(metrics),
            ChangeSummaryAr = FacilityWorkspaceRules.ChangeSummary(metrics),
            TopPendingActionAr = FacilityWorkspaceRules.TopPendingAction(metrics),
            ConfidenceReasons = FacilityWorkspaceRules.ConfidenceReasons(metrics),
            CalculatedAtUtc = generatedAt
        };

        return Envelope(context, Definition.Key, generatedAt, payload, FacilityWorkspaceRules.Confidence(metrics));
    }
}

internal sealed class FacilityNotesOverviewWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.NotesOverviewWidgetKey,
        "الملاحظات التشغيلية",
        "Notes Overview",
        "مؤشرات الملاحظات المفتوحة والحرجة والمتأخرة ضمن السجن.",
        WidgetCategory.Workload,
        PermissionCodes.NotesView,
        "Notes.Overview",
        WidgetSize.Medium,
        sensitive: false);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var metrics = await readService.GetMetricsAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            metrics.Notes,
            ConfidenceLevel.High,
            [new DrillDownTarget("notes.workspace", "فتح مساحة الملاحظات", new Dictionary<string, string>(), FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.NotesView)]);
    }
}

internal sealed class FacilityCorrectiveActionsWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.CorrectiveActionsWidgetKey,
        "الإجراءات التصحيحية",
        "Corrective Actions",
        "حالة الإجراءات التصحيحية المرتبطة بالملاحظات المصرح بها.",
        WidgetCategory.Workload,
        PermissionCodes.CorrectiveActionsView,
        "CorrectiveActions.Overview",
        WidgetSize.Medium);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var metrics = await readService.GetMetricsAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            metrics.CorrectiveActions,
            ConfidenceLevel.High,
            [new DrillDownTarget("corrective-actions.list", "فتح الإجراءات التصحيحية", new Dictionary<string, string>(), FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.CorrectiveActionsView)]);
    }
}

internal sealed class FacilityAlertsEscalationsWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.AlertsEscalationsWidgetKey,
        "التنبيهات والتصعيدات",
        "Alerts and Escalations",
        "التصعيدات التشغيلية وتنبيهات المستخدم المرتبطة بها.",
        WidgetCategory.Risk,
        PermissionCodes.EscalationsViewOccurrences,
        "Escalations.Overview",
        WidgetSize.Medium);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var metrics = await readService.GetMetricsAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            metrics.Alerts,
            ConfidenceLevel.Medium,
            [new DrillDownTarget("escalations.occurrences", "فتح حوادث التصعيد", new Dictionary<string, string>(), FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.EscalationsViewOccurrences)],
            ["لا يوجد Facility Alert مستقل؛ تعرض الأداة التصعيدات التشغيلية والتنبيهات الشخصية المرتبطة بها فقط."]);
    }
}

internal sealed class FacilityFormComplianceWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.FormComplianceWidgetKey,
        "التزام النماذج",
        "Form Compliance",
        "ملخص الالتزام بنفس قواعد لوحة التزام النماذج.",
        WidgetCategory.Compliance,
        PermissionCodes.FormsViewComplianceDashboard,
        "Forms.Compliance",
        WidgetSize.Medium);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var metrics = await readService.GetMetricsAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            metrics.FormCompliance,
            metrics.FormCompliance.TargetedForms == 0 ? ConfidenceLevel.Medium : ConfidenceLevel.High,
            [new DrillDownTarget("form-compliance.facility", "فتح التزام النماذج", new Dictionary<string, string> { ["facilityId"] = context.FacilityId!.Value.ToString() }, FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.FormsViewComplianceDashboard)]);
    }
}

internal sealed class FacilityPriorityQueueWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.PriorityQueueWidgetKey,
        "قائمة الأولويات",
        "Priority Queue",
        "أعلى العناصر التي تحتاج تدخلًا وفق قواعد deterministic.",
        WidgetCategory.Risk,
        PermissionCodes.DashboardViewRisk,
        "Facility.PriorityQueue",
        WidgetSize.Large);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var payload = await readService.GetPriorityQueueAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(context, Definition.Key, generatedAt, payload);
    }
}

internal sealed class FacilityRecentActivityWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(
        FacilityWorkspaceDefinitionProvider.RecentActivityWidgetKey,
        "آخر الأحداث",
        "Recent Activity",
        "آخر أحداث تشغيلية محدودة من المصادر الحالية، وليست Timeline كاملًا.",
        WidgetCategory.Timeline,
        PermissionCodes.DashboardViewOperational,
        "Facility.RecentActivity",
        WidgetSize.Wide);

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var payload = await readService.GetRecentActivityAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(context, Definition.Key, generatedAt, payload, ConfidenceLevel.Medium, warnings: ["هذه قائمة أحداث محدودة وليست التسلسل الزمني التشغيلي الكامل."]);
    }
}

internal static class FacilityWorkspaceWidgetProviderSupport
{
    public static WidgetDataEnvelopeDto Envelope<TPayload>(
        WorkspaceContext context,
        string widgetKey,
        DateTimeOffset generatedAt,
        TPayload payload,
        ConfidenceLevel confidence = ConfidenceLevel.High,
        IReadOnlyList<DrillDownTarget>? drillDowns = null,
        IReadOnlyList<string>? warnings = null)
        where TPayload : notnull =>
        WorkspaceContractFactory.Envelope(new WidgetDataEnvelopeBuildRequest<TPayload>(
            context,
            widgetKey,
            new WidgetDataEnvelopeTiming(generatedAt, generatedAt),
            payload,
            WidgetDataEnvelopeOptions.Default with
            {
                Confidence = confidence,
                DrillDownTargets = drillDowns ?? [],
                Warnings = warnings ?? []
            }));
}
