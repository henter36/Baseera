namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Drives the four-eyes gated transitions that a plain command can never perform alone: requesting and
/// deciding risk acceptance and closure. AssessmentReview/TreatmentApproval run inline inside
/// IRiskAssessmentService/IRiskTreatmentService (they need assessment/plan-specific fields a generic review
/// row can't hold cleanly); Reopen is a direct IRiskCommandService command since the lifecycle already allows
/// Closed -> Reopened without a separate approval gate. Both are intentionally out of scope here.
/// </summary>
public sealed class RiskReviewService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskReviewService
{
    public async Task<IReadOnlyList<RiskReviewDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        var reviews = await Db.RiskReviews.AsNoTracking()
            .Where(r => r.RiskRecordId == riskId)
            .OrderByDescending(r => r.RequestedAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);

        return reviews.Select(Map).ToList();
    }

    private static RiskReviewDto Map(RiskReview r) => new(
        r.Id, r.ReviewType, RiskManagementDisplay.ReviewTypeAr(r.ReviewType), r.SubjectReferenceType, r.SubjectReferenceId,
        r.RequestedBy, r.RequestedAtUtc, r.Status, r.Decision, r.Comments, r.CompletedAtUtc, Convert.ToBase64String(r.RowVersion));

    public async Task<Guid> RequestAsync(Guid facilityId, Guid riskId, RiskReviewRequestDto request, CancellationToken cancellationToken = default)
    {
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var review = new RiskReview
        {
            RiskRecordId = riskId,
            ReviewType = request.ReviewType,
            SubjectReferenceType = request.SubjectReferenceType ?? nameof(RiskRecord),
            SubjectReferenceId = request.SubjectReferenceId ?? riskId,
            RequestedBy = ActorReference(),
            RequestedAtUtc = now,
            Status = RiskReviewStatus.Requested,
            Comments = request.Comments,
            CreatedBy = ActorReference()
        };

        switch (request.ReviewType)
        {
            case RiskReviewType.RiskAcceptance:
                Require(PermissionCodes.RisksRequestAcceptance);
                if (request.AcceptedUntilUtc is null || request.AcceptedUntilUtc <= now)
                {
                    throw new InvalidOperationException("تاريخ نهاية القبول مطلوب ويجب أن يكون في المستقبل.");
                }

                if (request.ReviewFrequencyDays is null or <= 0)
                {
                    throw new InvalidOperationException("مدة المراجعة الدورية مطلوبة.");
                }

                if (string.IsNullOrWhiteSpace(request.Comments))
                {
                    throw new InvalidOperationException("مبرر قبول الخطر مطلوب.");
                }

                RiskLifecycleStateMachine.EnsureAllowed(risk.Status, RiskStatus.PendingAcceptance);
                review.RequestedAcceptedUntilUtc = request.AcceptedUntilUtc;
                review.RequestedReviewFrequencyDays = request.ReviewFrequencyDays;
                TransitionRisk(risk, RiskStatus.PendingAcceptance, "طلب قبول الخطر.");
                risk.AcceptedUntilUtc = request.AcceptedUntilUtc;
                await AuditAsync(RiskAuditActions.RiskAcceptanceRequested, nameof(RiskRecord), risk.Id, null, cancellationToken);
                break;

            case RiskReviewType.ClosureApproval:
                Require(PermissionCodes.RisksRequestClosure);
                if (string.IsNullOrWhiteSpace(request.ClosureReason))
                {
                    throw new InvalidOperationException("مبرر الإغلاق مطلوب.");
                }

                if (risk.CurrentResidualAssessmentId is null)
                {
                    throw new InvalidOperationException("لا يمكن طلب إغلاق الخطر دون تقييم متبقٍ معتمد.");
                }

                var residualApproved = await Db.RiskAssessments.AnyAsync(
                    a => a.Id == risk.CurrentResidualAssessmentId && a.Status == AssessmentStatus.Approved, cancellationToken);
                if (!residualApproved)
                {
                    throw new InvalidOperationException("التقييم المتبقي الحالي غير معتمد بعد.");
                }

                RiskLifecycleStateMachine.EnsureAllowed(risk.Status, RiskStatus.PendingClosure);
                TransitionRisk(risk, RiskStatus.PendingClosure, "طلب إغلاق الخطر.");
                risk.ClosureReason = request.ClosureReason;
                await AuditAsync(RiskAuditActions.RiskClosureRequested, nameof(RiskRecord), risk.Id, null, cancellationToken);
                break;

            case RiskReviewType.PeriodicReview:
                Require(PermissionCodes.RisksUpdate);
                break;

            default:
                throw new NotSupportedException(
                    "أنواع المراجعة الخاصة بالتقييم أو المعالجة تُدار داخليًا من خدماتها المختصة، وإعادة الفتح أمر مباشر.");
        }

        Db.Add(review);
        Db.Update(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return review.Id;
    }

    public async Task DecideAsync(Guid facilityId, Guid riskId, Guid reviewId, RiskReviewDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var review = await Db.RiskReviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.RiskRecordId == riskId, cancellationToken)
            ?? throw new KeyNotFoundException("طلب المراجعة غير موجود.");
        EnsureCurrentRowVersion(review, request.RowVersion);
        EnforceFourEyes(review.RequestedBy);

        if (review.Status != RiskReviewStatus.Requested)
        {
            throw new InvalidOperationException("تم اتخاذ قرار بشأن هذا الطلب مسبقًا.");
        }

        var approved = request.Decision is RiskReviewDecision.Approved or RiskReviewDecision.ApprovedWithConditions;
        var now = DateTimeOffset.UtcNow;

        switch (review.ReviewType)
        {
            case RiskReviewType.RiskAcceptance:
                Require(PermissionCodes.RisksApproveAcceptance);
                if (approved)
                {
                    RiskLifecycleStateMachine.EnsureAllowed(risk.Status, RiskStatus.Accepted);
                    TransitionRisk(risk, RiskStatus.Accepted, "اعتماد قبول الخطر.");
                    risk.AcceptedUntilUtc = review.RequestedAcceptedUntilUtc;
                    risk.NextReviewDueAtUtc = review.RequestedAcceptedUntilUtc;
                    await AuditAsync(RiskAuditActions.RiskAccepted, nameof(RiskRecord), risk.Id, null, cancellationToken);
                }
                else
                {
                    RiskLifecycleStateMachine.EnsureAllowed(risk.Status, RiskStatus.Active);
                    TransitionRisk(risk, RiskStatus.Active, "رفض/إعادة طلب قبول الخطر.");
                    risk.AcceptedUntilUtc = null;
                }

                break;

            case RiskReviewType.ClosureApproval:
                Require(PermissionCodes.RisksApproveClosure);
                if (approved)
                {
                    RiskLifecycleStateMachine.EnsureAllowed(risk.Status, RiskStatus.Closed);
                    TransitionRisk(risk, RiskStatus.Closed, "اعتماد إغلاق الخطر.");
                    risk.ClosedAtUtc = now;
                    risk.ClosedBy = ActorReference();
                    await AuditAsync(RiskAuditActions.RiskClosed, nameof(RiskRecord), risk.Id, null, cancellationToken);
                }
                else
                {
                    RiskLifecycleStateMachine.EnsureAllowed(risk.Status, RiskStatus.Monitoring);
                    TransitionRisk(risk, RiskStatus.Monitoring, "رفض/إعادة طلب إغلاق الخطر.");
                    risk.ClosureReason = null;
                }

                break;

            case RiskReviewType.PeriodicReview:
                Require(PermissionCodes.RisksReviewAssessment);
                risk.LastReviewedAtUtc = now;
                break;

            default:
                throw new NotSupportedException("لا يمكن اتخاذ قرار عبر هذه الواجهة لنوع المراجعة المحدد.");
        }

        review.Status = RiskReviewStatus.Completed;
        review.Decision = request.Decision;
        review.Comments = request.Comments ?? review.Comments;
        review.CompletedAtUtc = now;
        Db.Update(review);
        Db.Update(risk);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskReviewCompleted, nameof(RiskReview), review.Id, new { request.Decision }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private void TransitionRisk(RiskRecord risk, RiskStatus to, string reason)
    {
        var from = risk.Status;
        risk.Status = to;
        risk.UpdatedBy = ActorReference();
        risk.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Db.Add(new RiskStatusHistory { RiskRecordId = risk.Id, FromStatus = from, ToStatus = to, ChangedBy = ActorReference(), Reason = reason });
    }
}
