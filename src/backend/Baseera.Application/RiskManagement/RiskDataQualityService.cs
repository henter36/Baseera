namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Fixed catalog of data-quality codes for the risk register, mirroring SensitiveCustodyDataQualityService's
/// shape: one bounded COUNT query per code, only non-zero issues surfaced. An empty result here is never
/// treated as proof the domain is healthy — it only means none of these specific, known defects were found.
/// </summary>
public sealed class RiskDataQualityService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskDataQualityService
{
    private static readonly HashSet<RiskStatus> OpenStatuses =
    [
        RiskStatus.Draft, RiskStatus.UnderAssessment, RiskStatus.PendingReview, RiskStatus.Active,
        RiskStatus.UnderTreatment, RiskStatus.Monitoring, RiskStatus.PendingAcceptance,
        RiskStatus.Accepted, RiskStatus.PendingClosure, RiskStatus.Reopened
    ];

    public async Task<RiskDataQualityPayload> GetDataQualityAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var open = Db.RiskRecords.AsNoTracking().Where(r => r.FacilityId == facilityId && OpenStatuses.Contains(r.Status));
        var all = Db.RiskRecords.AsNoTracking().Where(r => r.FacilityId == facilityId);

        var issues = new List<RiskDataQualityIssueDto>
        {
            await IssueAsync(RiskDataQualityCodes.MissingOwner, "متوسطة",
                "لا يمكن تحديد مسؤولية المتابعة.", nameof(RiskRecord), "مالك الخطر",
                "تعيين مالك للخطر.",
                open.CountAsync(r => r.OwnerWorkforceMemberId == null && r.OwnerUserId == null, cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.MissingCurrentAssessment, "عالية",
                "لا يمكن معرفة درجة الخطر الحالية.", nameof(RiskRecord), "ضابط المخاطر",
                "إجراء تقييم حالي واعتماده.",
                open.CountAsync(r => r.Status != RiskStatus.Draft && r.CurrentAssessmentId == null, cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.MissingReviewDate, "متوسطة",
                "لا يوجد موعد مراجعة محدد لمتابعة الخطر.", nameof(RiskRecord), "ضابط المخاطر",
                "تحديد موعد المراجعة القادم.",
                open.CountAsync(r => r.NextReviewDueAtUtc == null && r.Status != RiskStatus.Draft, cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.ReviewOverdue, "عالية",
                "تجاوز موعد المراجعة المقرر دون تحديث.", nameof(RiskRecord), "ضابط المخاطر",
                "إجراء المراجعة الدورية فورًا.",
                open.CountAsync(r => r.NextReviewDueAtUtc != null && r.NextReviewDueAtUtc < now, cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.ActiveWithoutTreatment, "عالية",
                "خطر نشط دون خطة معالجة موثقة.", nameof(RiskRecord), "ضابط المخاطر",
                "إنشاء خطة معالجة.",
                open.CountAsync(r => (r.Status == RiskStatus.Active || r.Status == RiskStatus.UnderTreatment) && !r.TreatmentPlans.Any(), cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.TreatmentWithoutOwner, "متوسطة",
                "خطة معالجة دون مالك محدد.", nameof(RiskTreatmentPlan), "ضابط المخاطر",
                "تعيين مالك لخطة المعالجة.",
                Db.RiskTreatmentPlans.CountAsync(p => p.RiskRecord.FacilityId == facilityId && p.OwnerWorkforceMemberId == null
                    && p.Status != TreatmentPlanStatus.Completed && p.Status != TreatmentPlanStatus.Cancelled && p.Status != TreatmentPlanStatus.Rejected, cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.OverdueTreatmentAction, "عالية",
                "إجراء معالجة تجاوز موعد استحقاقه.", nameof(RiskTreatmentAction), "منفذ الإجراء",
                "تحديث حالة الإجراء أو استكماله.",
                Db.RiskTreatmentActions.CountAsync(a => a.TreatmentPlan.RiskRecord.FacilityId == facilityId && a.DueAtUtc < now
                    && a.Status != RiskTreatmentActionStatus.Completed && a.Status != RiskTreatmentActionStatus.Cancelled, cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.CompletedActionWithoutEvidence, "متوسطة",
                "إجراء اكتمل دون توثيق دليل الإكمال المطلوب.", nameof(RiskTreatmentAction), "منفذ الإجراء",
                "توثيق ملخص وأدلة الإكمال.",
                Db.RiskTreatmentActions.CountAsync(a => a.TreatmentPlan.RiskRecord.FacilityId == facilityId
                    && a.Status == RiskTreatmentActionStatus.Completed && a.CompletionEvidenceRequired
                    && string.IsNullOrWhiteSpace(a.CompletionSummary), cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.ControlNotTested, "منخفضة",
                "ضابط مطبق لم يخضع للاختبار بعد.", nameof(RiskControl), "ضابط المخاطر",
                "جدولة اختبار الضابط.",
                Db.RiskControls.CountAsync(c => c.RiskRecord.FacilityId == facilityId
                    && c.ControlStatus == RiskControlStatus.Implemented && c.ControlEffectiveness == ControlEffectiveness.NotTested, cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.IneffectiveControlWithoutTreatment, "عالية",
                "ضابط غير فعّال دون خطة معالجة مرتبطة بالخطر.", nameof(RiskControl), "ضابط المخاطر",
                "إنشاء خطة معالجة لتعويض الضابط غير الفعّال.",
                Db.RiskControls.CountAsync(c => c.RiskRecord.FacilityId == facilityId && c.ControlEffectiveness == ControlEffectiveness.Ineffective
                    && !c.RiskRecord.TreatmentPlans.Any(p => p.Status != TreatmentPlanStatus.Cancelled && p.Status != TreatmentPlanStatus.Rejected), cancellationToken)),

            await IssueAsync(RiskDataQualityCodes.StaleData, "منخفضة",
                "لم يتم تحديث بيانات الخطر منذ فترة طويلة.", nameof(RiskRecord), "ضابط المخاطر",
                "مراجعة الخطر وتحديث بياناته.",
                open.CountAsync(r => r.DataFreshAsOfUtc == null || r.DataFreshAsOfUtc < now.AddDays(-90), cancellationToken))
        };

        var recurringCount = await all
            .GroupBy(r => r.RecurrenceKey)
            .Where(g => g.Count() > 1)
            .CountAsync(cancellationToken);
        if (recurringCount > 0)
        {
            issues.Add(new RiskDataQualityIssueDto(
                RiskDataQualityCodes.PotentialDuplicate, "متوسطة", recurringCount,
                "قد تتكرر بيانات نفس الخطر تحت أكثر من سجل.", nameof(RiskRecord), "ضابط المخاطر",
                "مراجعة السجلات المتشابهة وتحديد ما إذا كانت مكررة."));
        }

        return new RiskDataQualityPayload(issues.Where(i => i.Count > 0).ToList(), now);
    }

    private static async Task<RiskDataQualityIssueDto> IssueAsync(
        string code, string severityAr, string impactAr, string sourceEntity, string responsibleRoleAr, string correctiveActionAr, Task<int> countTask)
    {
        var count = await countTask;
        return new RiskDataQualityIssueDto(code, severityAr, count, impactAr, sourceEntity, responsibleRoleAr, correctiveActionAr);
    }
}
