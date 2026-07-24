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

    public static WidgetDataEnvelopeDto Envelope<TPayload>(WidgetDataEnvelopeBuildRequest<TPayload> request)
        where TPayload : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        var freshness = request.IsPartial
            ? new DataFreshness(DataFreshnessStatus.Partial, "جزئية", "تم توليد البيانات مع تحذيرات.")
            : Freshness(request.Timing.GeneratedAtUtc, request.Timing.DataEffectiveAtUtc);

        return new WidgetDataEnvelopeDto(
            request.WidgetKey,
            request.Timing.GeneratedAtUtc,
            request.Timing.DataEffectiveAtUtc,
            freshness,
            Confidence(request.Options.Confidence, request.Options.ConfidenceReasonAr),
            ScopeSummary(request.Context),
            request.Options.IsPartial,
            request.Options.Warnings,
            request.Payload,
            request.Options.DrillDownTargets,
            request.Options.AllowedActions);
    }

    public static WidgetDataEnvelopeBuildRequest<TPayload> BuildRequest<TPayload>(
        WorkspaceContext context,
        string widgetKey,
        DateTimeOffset generatedAtUtc,
        DateTimeOffset? dataEffectiveAtUtc,
        TPayload payload,
        IReadOnlyList<DrillDownTarget>? drillDownTargets = null)
        where TPayload : notnull
    {
        return new WidgetDataEnvelopeBuildRequest<TPayload>(
            context,
            widgetKey,
            new WidgetDataEnvelopeTiming(generatedAtUtc, dataEffectiveAtUtc),
            payload,
            WidgetDataEnvelopeOptions.Default with { DrillDownTargets = drillDownTargets ?? [] });
    }
}

public sealed record WidgetDataEnvelopeBuildRequest<TPayload>(
    WorkspaceContext Context,
    string WidgetKey,
    WidgetDataEnvelopeTiming Timing,
    TPayload Payload,
    WidgetDataEnvelopeOptions Options)
    where TPayload : notnull
{
    public bool IsPartial => Options.IsPartial;
}

public sealed record WidgetDataEnvelopeTiming(
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset? DataEffectiveAtUtc);

public sealed record WidgetDataEnvelopeOptions(
    IReadOnlyList<DrillDownTarget> DrillDownTargets,
    IReadOnlyList<WorkspaceAllowedAction> AllowedActions,
    IReadOnlyList<string> Warnings,
    bool IsPartial,
    ConfidenceLevel Confidence,
    string? ConfidenceReasonAr)
{
    public static WidgetDataEnvelopeOptions Default { get; } = new(
        [],
        [],
        [],
        false,
        ConfidenceLevel.High,
        null);
}
