namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Facility Workspace integration surface: the summary rail and the Intervention Queue / priority-queue
/// feed. Each intervention type below is a fixed, bounded query (never a per-row loop), so the total query
/// count stays constant regardless of how many risks the facility has (see docs/phase-d6-risk-performance.md).
/// </summary>
public sealed class RiskReadinessService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IOrganizationalScopeService scope,
    IAuditService audit,
    RiskRegisterQueryService registerQueryService)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskReadinessService
{
    public Task<RiskWorkspaceSummaryDto> GetWorkspaceSummaryAsync(Guid facilityId, CancellationToken cancellationToken = default) =>
        registerQueryService.GetSummaryAsync(facilityId, cancellationToken);

    public async Task<RiskWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        // Interventions are computed first so its already-fetched treatment-action rows can be reused for
        // the summary's overdue-action count, avoiding a redundant round trip on the shared workspace path.
        var (interventions, overdueTreatmentActions) = await BuildInterventionsAsync(facilityId, 10, cancellationToken);
        var summary = await registerQueryService.BuildSummaryAsync(facilityId, overdueTreatmentActions, cancellationToken);
        return new RiskWorkspacePayload { Summary = summary, Interventions = interventions };
    }

    public async Task<IReadOnlyList<RiskInterventionItemDto>> GetInterventionsAsync(Guid facilityId, int limit, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        var (items, _) = await BuildInterventionsAsync(facilityId, limit, cancellationToken);
        return items;
    }

    private async Task<(IReadOnlyList<RiskInterventionItemDto> Items, int OverdueTreatmentActionsCount)> BuildInterventionsAsync(Guid facilityId, int limit, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var boundedLimit = Math.Clamp(limit, 1, 50);
        var items = new List<RiskInterventionItemDto>();

        var risks = await Db.RiskRecords.AsNoTracking()
            .Include(r => r.CurrentRatingBand)
            .Include(r => r.CurrentResidualAssessment)
            .Where(r => r.FacilityId == facilityId && r.Status != RiskStatus.Closed && r.Status != RiskStatus.Archived)
            .Select(r => new
            {
                r.Id,
                r.RiskCode,
                r.Title,
                r.Status,
                r.CurrentTrend,
                Severity = r.CurrentRatingBand != null ? r.CurrentRatingBand.Severity : (RiskRatingSeverity?)null,
                r.CurrentAssessmentId,
                r.NextReviewDueAtUtc,
                r.AcceptedUntilUtc,
                r.OwnerWorkforceMemberId,
                r.OwnerUserId,
                r.DataFreshAsOfUtc,
                r.RecurrenceKey,
                HasTreatmentPlan = r.TreatmentPlans.Any(),
                ResidualScore = r.CurrentResidualAssessment != null ? r.CurrentResidualAssessment.CalculatedScore : (decimal?)null
            })
            .ToListAsync(cancellationToken);

        foreach (var r in risks.Where(r => r.Severity == RiskRatingSeverity.Critical))
        {
            items.Add(Build(RiskInterventionTypes.CriticalRiskActive, "حرج", 100, r.Id, r.RiskCode, r.Title, "خطر بدرجة حرجة قيد النشاط.", null, "مراجعة فورية"));
        }

        foreach (var r in risks.Where(r => r.Severity == RiskRatingSeverity.High && r.CurrentTrend == RiskTrend.Increasing))
        {
            items.Add(Build(RiskInterventionTypes.HighRiskIncreasing, "عالية", 90, r.Id, r.RiskCode, r.Title, "خطر عالٍ في اتجاه تصاعدي.", null, "مراجعة الاتجاه"));
        }

        foreach (var r in risks.Where(r => r.OwnerWorkforceMemberId is null && r.OwnerUserId is null))
        {
            items.Add(Build(RiskInterventionTypes.RiskWithoutOwner, "متوسطة", 60, r.Id, r.RiskCode, r.Title, "الخطر بلا مالك محدد.", null, "تعيين مالك"));
        }

        foreach (var r in risks.Where(r => r.Status != RiskStatus.Draft && r.CurrentAssessmentId is null))
        {
            items.Add(Build(RiskInterventionTypes.RiskWithoutCurrentAssessment, "عالية", 80, r.Id, r.RiskCode, r.Title, "لا يوجد تقييم حالي معتمد.", null, "إجراء تقييم"));
        }

        foreach (var r in risks.Where(r => r.NextReviewDueAtUtc is not null && r.NextReviewDueAtUtc < now))
        {
            items.Add(Build(RiskInterventionTypes.RiskReviewOverdue, "متوسطة", 65, r.Id, r.RiskCode, r.Title, "موعد المراجعة الدورية تجاوز الاستحقاق.", r.NextReviewDueAtUtc, "مراجعة الخطر"));
        }

        foreach (var r in risks.Where(r => (r.Status == RiskStatus.Active || r.Status == RiskStatus.UnderTreatment) && !r.HasTreatmentPlan))
        {
            items.Add(Build(RiskInterventionTypes.RiskWithoutTreatment, "عالية", 75, r.Id, r.RiskCode, r.Title, "خطر نشط بلا خطة معالجة.", null, "إنشاء خطة معالجة"));
        }

        foreach (var r in risks.Where(r => r.Status == RiskStatus.Accepted && r.AcceptedUntilUtc is not null && r.AcceptedUntilUtc < now.AddDays(14) && r.AcceptedUntilUtc >= now))
        {
            items.Add(Build(RiskInterventionTypes.AcceptedRiskReviewDue, "متوسطة", 55, r.Id, r.RiskCode, r.Title, "اقترب موعد مراجعة القبول.", r.AcceptedUntilUtc, "مراجعة القبول"));
        }

        foreach (var r in risks.Where(r => r.Status == RiskStatus.Accepted && r.AcceptedUntilUtc is not null && r.AcceptedUntilUtc < now))
        {
            items.Add(Build(RiskInterventionTypes.AcceptedRiskExpired, "عالية", 85, r.Id, r.RiskCode, r.Title, "انتهت مدة قبول الخطر دون مراجعة.", r.AcceptedUntilUtc, "إعادة تقييم القبول"));
        }

        foreach (var r in risks.Where(r => r.DataFreshAsOfUtc is null || r.DataFreshAsOfUtc < now.AddDays(-90)))
        {
            items.Add(Build(RiskInterventionTypes.RiskDataStale, "منخفضة", 30, r.Id, r.RiskCode, r.Title, "لم يتم تحديث بيانات الخطر منذ فترة طويلة.", null, "تحديث البيانات"));
        }

        var recurringKeys = risks.GroupBy(r => r.RecurrenceKey).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var r in risks.Where(r => recurringKeys.Contains(r.RecurrenceKey)))
        {
            items.Add(Build(RiskInterventionTypes.PotentialDuplicateRisk, "متوسطة", 50, r.Id, r.RiskCode, r.Title, "قد يتشابه هذا الخطر مع خطر آخر مسجل لنفس التصنيف والسجن.", null, "مراجعة التكرار"));
        }

        var riskIds = risks.Select(r => r.Id).ToList();
        var riskLookup = risks.ToDictionary(r => r.Id, r => (r.RiskCode, r.Title));

        var overduePlans = await Db.RiskTreatmentPlans.AsNoTracking()
            .Where(p => riskIds.Contains(p.RiskRecordId) && p.DueAtUtc < now
                && p.Status != TreatmentPlanStatus.Completed && p.Status != TreatmentPlanStatus.Cancelled && p.Status != TreatmentPlanStatus.Rejected)
            .Select(p => new { p.RiskRecordId, p.DueAtUtc })
            .ToListAsync(cancellationToken);
        foreach (var p in overduePlans)
        {
            var (code, title) = riskLookup[p.RiskRecordId];
            items.Add(Build(RiskInterventionTypes.TreatmentPlanOverdue, "عالية", 78, p.RiskRecordId, code, title, "خطة المعالجة تجاوزت موعدها.", p.DueAtUtc, "متابعة الخطة"));
        }

        // Overdue + blocked actions are pulled in a single round trip and split in memory to keep the
        // workspace's total query-count budget stable regardless of how many intervention types exist.
        var actionsOfInterest = await Db.RiskTreatmentActions.AsNoTracking()
            .Where(a => riskIds.Contains(a.TreatmentPlan.RiskRecordId)
                && ((a.DueAtUtc < now && a.Status != RiskTreatmentActionStatus.Completed && a.Status != RiskTreatmentActionStatus.Cancelled)
                    || a.Status == RiskTreatmentActionStatus.Blocked))
            .Select(a => new { RiskRecordId = a.TreatmentPlan.RiskRecordId, a.DueAtUtc, a.Status })
            .ToListAsync(cancellationToken);
        foreach (var a in actionsOfInterest.Where(a => a.Status == RiskTreatmentActionStatus.Blocked))
        {
            var (code, title) = riskLookup[a.RiskRecordId];
            items.Add(Build(RiskInterventionTypes.TreatmentActionBlocked, "متوسطة", 58, a.RiskRecordId, code, title, "إجراء معالجة معطّل.", null, "إزالة العائق"));
        }
        foreach (var a in actionsOfInterest.Where(a => a.Status != RiskTreatmentActionStatus.Blocked))
        {
            var (code, title) = riskLookup[a.RiskRecordId];
            items.Add(Build(RiskInterventionTypes.TreatmentActionOverdue, "عالية", 72, a.RiskRecordId, code, title, "إجراء معالجة تجاوز موعده.", a.DueAtUtc, "متابعة الإجراء"));
        }

        var controlsOfInterest = await Db.RiskControls.AsNoTracking()
            .Where(c => riskIds.Contains(c.RiskRecordId)
                && ((c.ControlStatus == RiskControlStatus.Implemented && c.ControlEffectiveness == ControlEffectiveness.NotTested)
                    || c.ControlEffectiveness == ControlEffectiveness.Ineffective))
            .Select(c => new { c.RiskRecordId, c.ControlEffectiveness })
            .ToListAsync(cancellationToken);
        foreach (var riskRecordId in controlsOfInterest.Where(c => c.ControlEffectiveness == ControlEffectiveness.NotTested).Select(c => c.RiskRecordId).Distinct())
        {
            var (code, title) = riskLookup[riskRecordId];
            items.Add(Build(RiskInterventionTypes.ControlNotTested, "منخفضة", 35, riskRecordId, code, title, "ضابط مطبق لم يُختبر بعد.", null, "اختبار الضابط"));
        }

        foreach (var riskRecordId in controlsOfInterest.Where(c => c.ControlEffectiveness == ControlEffectiveness.Ineffective).Select(c => c.RiskRecordId).Distinct())
        {
            var (code, title) = riskLookup[riskRecordId];
            items.Add(Build(RiskInterventionTypes.ControlIneffective, "عالية", 70, riskRecordId, code, title, "ضابط قائم غير فعّال.", null, "معالجة الضابط"));
        }

        var pendingReviews = await Db.RiskReviews.AsNoTracking()
            .Where(v => riskIds.Contains(v.RiskRecordId) && v.Status == RiskReviewStatus.Requested)
            .Select(v => new { v.RiskRecordId, v.ReviewType })
            .ToListAsync(cancellationToken);
        foreach (var v in pendingReviews)
        {
            var (code, title) = riskLookup[v.RiskRecordId];
            if (v.ReviewType == RiskReviewType.ClosureApproval)
            {
                items.Add(Build(RiskInterventionTypes.ClosureAwaitingApproval, "متوسطة", 62, v.RiskRecordId, code, title, "طلب إغلاق بانتظار الاعتماد.", null, "اعتماد الإغلاق"));
            }
            else if (v.ReviewType == RiskReviewType.RiskAcceptance)
            {
                items.Add(Build(RiskInterventionTypes.AcceptanceAwaitingApproval, "متوسطة", 62, v.RiskRecordId, code, title, "طلب قبول بانتظار الاعتماد.", null, "اعتماد القبول"));
            }
        }

        var overdueTreatmentActionsCount = actionsOfInterest.Count(a =>
            a.DueAtUtc < now && a.Status != RiskTreatmentActionStatus.Completed && a.Status != RiskTreatmentActionStatus.Cancelled);

        var boundedItems = items
            .OrderByDescending(i => i.PriorityRank)
            .ThenBy(i => i.DueAtUtc ?? DateTimeOffset.MaxValue)
            .Take(boundedLimit)
            .ToList();

        return (boundedItems, overdueTreatmentActionsCount);
    }

    private static RiskInterventionItemDto Build(string type, string severityAr, int rank, Guid riskId, string code, string title, string reason, DateTimeOffset? dueAtUtc, string actionAr) =>
        new(type, severityAr, rank, riskId, code, title, reason, dueAtUtc, null, actionAr);
}
