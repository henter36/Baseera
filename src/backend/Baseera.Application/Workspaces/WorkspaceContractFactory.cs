namespace Baseera.Application.Workspaces;

public static class WorkspaceContractFactory
{
    public static DataFreshness Freshness(DateTimeOffset generatedAtUtc, DateTimeOffset? dataEffectiveAtUtc)
    {
        if (dataEffectiveAtUtc is null)
        {
            return new DataFreshness(DataFreshnessStatus.Unknown, "غير معروفة", "لا يوجد وقت فعالية للبيانات.");
        }

        var age = generatedAtUtc - dataEffectiveAtUtc.Value;
        if (age <= TimeSpan.FromMinutes(5))
        {
            return new DataFreshness(DataFreshnessStatus.Current, "محدثة", null);
        }

        if (age <= TimeSpan.FromMinutes(30))
        {
            return new DataFreshness(DataFreshnessStatus.Delayed, "متأخرة", "آخر بيانات أقدم من 5 دقائق.");
        }

        return new DataFreshness(DataFreshnessStatus.Stale, "قديمة", "آخر بيانات أقدم من 30 دقيقة.");
    }

    public static WidgetConfidence Confidence(ConfidenceLevel level, string? reasonAr)
    {
        var label = level switch
        {
            ConfidenceLevel.High => "مرتفعة",
            ConfidenceLevel.Medium => "متوسطة",
            ConfidenceLevel.Low => "منخفضة",
            _ => "غير معروفة"
        };

        return new WidgetConfidence(level, label, reasonAr);
    }

    public static WorkspaceScopeSummary ScopeSummary(WorkspaceContext context, string? labelAr = null)
    {
        return new WorkspaceScopeSummary(
            context.Level,
            labelAr ?? context.UserScopeSummary,
            context.RegionId,
            context.FacilityId,
            context.IncludesSensitiveData);
    }

    public static WidgetDataEnvelopeDto Envelope<TPayload>(
        WorkspaceContext context,
        string widgetKey,
        DateTimeOffset generatedAtUtc,
        DateTimeOffset? dataEffectiveAtUtc,
        TPayload payload,
        IReadOnlyList<DrillDownTarget>? drillDownTargets = null,
        IReadOnlyList<WorkspaceAllowedAction>? allowedActions = null,
        IReadOnlyList<string>? warnings = null,
        bool isPartial = false,
        ConfidenceLevel confidence = ConfidenceLevel.High,
        string? confidenceReasonAr = null)
        where TPayload : notnull
    {
        return new WidgetDataEnvelopeDto(
            widgetKey,
            generatedAtUtc,
            dataEffectiveAtUtc,
            isPartial ? new DataFreshness(DataFreshnessStatus.Partial, "جزئية", "تم توليد البيانات مع تحذيرات.") : Freshness(generatedAtUtc, dataEffectiveAtUtc),
            Confidence(confidence, confidenceReasonAr),
            ScopeSummary(context),
            isPartial,
            warnings ?? [],
            payload,
            drillDownTargets ?? [],
            allowedActions ?? []);
    }
}
