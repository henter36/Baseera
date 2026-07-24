namespace Baseera.Application.Workspaces;

using Baseera.Domain.Identity;
using static Baseera.Application.Workspaces.FacilityWorkspaceWidgetProviderSupport;

internal sealed class FacilityHeaderWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.HeaderWidgetKey,
        TitleAr = "تعريف السجن",
        TitleEn = "Facility Context",
        DescriptionAr = "اسم السجن والمنطقة والفترة الحالية.",
        Category = WidgetCategory.Summary,
        RequiredPermission = PermissionCodes.WorkspacesViewFacility,
        DataCapability = "Facility.Context",
        Size = WidgetSize.Wide
    });

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
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.ExecutiveSummaryWidgetKey,
        TitleAr = "الملخص التشغيلي",
        TitleEn = "Executive Facility Summary",
        DescriptionAr = "تقييم deterministic للحالة الحالية دون ذكاء اصطناعي.",
        Category = WidgetCategory.Summary,
        RequiredPermission = PermissionCodes.DashboardViewOperational,
        DataCapability = "Facility.ExecutiveSummary",
        Size = WidgetSize.Large
    });

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
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.NotesOverviewWidgetKey,
        TitleAr = "الملاحظات التشغيلية",
        TitleEn = "Notes Overview",
        DescriptionAr = "مؤشرات الملاحظات المفتوحة والحرجة والمتأخرة ضمن السجن.",
        Category = WidgetCategory.Workload,
        RequiredPermission = PermissionCodes.NotesView,
        DataCapability = "Notes.Overview",
        Size = WidgetSize.Medium
    });

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var payload = await readService.GetNotesOverviewAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            payload,
            ConfidenceLevel.High,
            [new DrillDownTarget("notes.workspace", "فتح مساحة الملاحظات", new Dictionary<string, string>(), FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.NotesView)]);
    }
}

internal sealed class FacilityCorrectiveActionsWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.CorrectiveActionsWidgetKey,
        TitleAr = "الإجراءات التصحيحية",
        TitleEn = "Corrective Actions",
        DescriptionAr = "حالة الإجراءات التصحيحية المرتبطة بالملاحظات المصرح بها.",
        Category = WidgetCategory.Workload,
        RequiredPermission = PermissionCodes.CorrectiveActionsView,
        DataCapability = "CorrectiveActions.Overview",
        Size = WidgetSize.Medium
    });

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var payload = await readService.GetCorrectiveActionsAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            payload,
            ConfidenceLevel.High,
            [new DrillDownTarget("corrective-actions.list", "فتح الإجراءات التصحيحية", new Dictionary<string, string>(), FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.CorrectiveActionsView)]);
    }
}

internal sealed class FacilityAlertsEscalationsWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.AlertsEscalationsWidgetKey,
        TitleAr = "التنبيهات والتصعيدات",
        TitleEn = "Alerts and Escalations",
        DescriptionAr = "التصعيدات التشغيلية وتنبيهات المستخدم المرتبطة بها.",
        Category = WidgetCategory.Risk,
        RequiredPermission = PermissionCodes.EscalationsViewOccurrences,
        DataCapability = "Escalations.Overview",
        Size = WidgetSize.Medium
    });

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var payload = await readService.GetAlertsEscalationsAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            payload,
            ConfidenceLevel.Medium,
            [new DrillDownTarget("escalations.occurrences", "فتح حوادث التصعيد", new Dictionary<string, string>(), FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.EscalationsViewOccurrences)],
            ["لا يوجد Facility Alert مستقل؛ تعرض الأداة التصعيدات التشغيلية والتنبيهات الشخصية المرتبطة بها فقط."]);
    }
}

internal sealed class FacilityFormComplianceWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.FormComplianceWidgetKey,
        TitleAr = "التزام النماذج",
        TitleEn = "Form Compliance",
        DescriptionAr = "ملخص الالتزام بنفس قواعد لوحة التزام النماذج.",
        Category = WidgetCategory.Compliance,
        RequiredPermission = PermissionCodes.FormsViewComplianceDashboard,
        DataCapability = "Forms.Compliance",
        Size = WidgetSize.Medium
    });

    public async Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
    {
        var facilityId = FacilityWorkspaceContextGuard.RequireFacilityId(context);
        var payload = await readService.GetFormComplianceAsync(context, cancellationToken);
        var generatedAt = timeProvider.GetUtcNow();
        return Envelope(
            context,
            Definition.Key,
            generatedAt,
            payload,
            payload.TargetedForms == 0 ? ConfidenceLevel.Medium : ConfidenceLevel.High,
            [new DrillDownTarget("form-compliance.facility", "فتح التزام النماذج", new Dictionary<string, string> { ["facilityId"] = facilityId.ToString() }, FacilityWorkspaceDrillDownFilters.Preserve(context), PermissionCodes.FormsViewComplianceDashboard)]);
    }
}

internal sealed class FacilityPriorityQueueWorkspaceWidgetProvider(
    IFacilityWorkspaceReadService readService,
    TimeProvider timeProvider) : IWorkspaceWidgetProvider
{
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.PriorityQueueWidgetKey,
        TitleAr = "قائمة الأولويات",
        TitleEn = "Priority Queue",
        DescriptionAr = "أعلى العناصر التي تحتاج تدخلًا وفق قواعد deterministic.",
        Category = WidgetCategory.Risk,
        RequiredPermission = PermissionCodes.DashboardViewRisk,
        DataCapability = "Facility.PriorityQueue",
        Size = WidgetSize.Large
    });

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
    public WidgetDefinition Definition { get; } = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
    {
        Key = FacilityWorkspaceDefinitionProvider.RecentActivityWidgetKey,
        TitleAr = "آخر الأحداث",
        TitleEn = "Recent Activity",
        DescriptionAr = "آخر أحداث تشغيلية محدودة من المصادر الحالية، وليست Timeline كاملًا.",
        Category = WidgetCategory.Timeline,
        RequiredPermission = PermissionCodes.DashboardViewOperational,
        DataCapability = "Facility.RecentActivity",
        Size = WidgetSize.Wide
    });

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
