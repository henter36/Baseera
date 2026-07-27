namespace Baseera.Application.RiskManagement;

using System.Text.Json;
using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Draft -> PendingReview -> Reviewed -> Approved, with Draft/Rejected as the "returned" landing states.
/// The score, rating band, and overall impact level are always computed here from the matrix in effect —
/// never accepted from the client. Approval is the single place that updates RiskRecord's cached "current
/// view" pointers (CurrentAssessmentId/CurrentScore/CurrentRatingBandId/CurrentTrend) and, where applicable,
/// advances the risk's own lifecycle status.
/// </summary>
public sealed class RiskAssessmentService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskAssessmentService
{
    private static readonly HashSet<AssessmentType> CurrentFamily = [AssessmentType.Current, AssessmentType.PostIncident, AssessmentType.PeriodicReview];

    public async Task<IReadOnlyList<RiskAssessmentDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);

        var assessments = await Db.RiskAssessments.AsNoTracking()
            .Include(a => a.RatingBand)
            .Where(a => a.RiskRecordId == riskId)
            .OrderByDescending(a => a.AssessedAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);

        return assessments.Select(Map).ToList();
    }

    private static RiskAssessmentDto Map(RiskAssessment a) => new(
        a.Id, a.AssessmentType, RiskManagementDisplay.AssessmentTypeAr(a.AssessmentType), a.Status, RiskManagementDisplay.AssessmentStatusAr(a.Status),
        a.CalculatedScore, a.RatingBand.Code, a.RatingBand.LabelAr, a.Rationale, a.AssessedAtUtc, a.AssessedBy,
        a.ReviewedAtUtc, a.ReviewedBy, a.ApprovedAtUtc, a.ApprovedBy, a.RejectionReason, Convert.ToBase64String(a.RowVersion));

    public async Task<Guid> CreateAsync(Guid facilityId, Guid riskId, RiskAssessmentCreateRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksAssess);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        await EnsureRiskAcceptsNewAssessmentAsync(risk, riskId, request.AssessmentType, cancellationToken);

        var matrix = await ResolveMatrixAsync(risk.OrganizationId, request.MatrixId, cancellationToken);
        var likelihood = ResolveLikelihood(matrix, request.LikelihoodLevelId);
        ValidateImpactRequest(request.Impacts);
        var impactLevels = ResolveImpactLevels(matrix, request.Impacts);
        var weights = ResolveWeights(matrix);

        var score = RiskScoringEngine.CalculateScore(
            matrix.ScoreFormula,
            likelihood.NumericValue,
            impactLevels.Select(i => new RiskImpactInput(i.Level.ImpactDimensionId, i.Level.NumericValue)).ToList(),
            weights);
        var band = RiskScoringEngine.SelectRatingBand(matrix.RatingBands.ToList(), score);
        ValidateScoreDependentRequirements(band, request);

        var assessment = BuildAssessment(risk, riskId, request, matrix, likelihood, impactLevels, score, band);
        Db.Add(assessment);
        TransitionRiskToUnderAssessmentIfNeeded(risk);

        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskAssessmentCreated, nameof(RiskAssessment), assessment.Id, new { AssessmentType = assessment.AssessmentType.ToString(), assessment.CalculatedScore }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return assessment.Id;
    }

    private async Task EnsureRiskAcceptsNewAssessmentAsync(RiskRecord risk, Guid riskId, AssessmentType assessmentType, CancellationToken cancellationToken)
    {
        if (risk.Status is RiskStatus.Closed or RiskStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن إجراء تقييم على خطر مغلق أو مؤرشف.");
        }

        var inProgress = await Db.RiskAssessments.AnyAsync(a => a.RiskRecordId == riskId
            && a.AssessmentType == assessmentType
            && (a.Status == AssessmentStatus.Draft || a.Status == AssessmentStatus.PendingReview || a.Status == AssessmentStatus.Reviewed), cancellationToken);
        if (inProgress)
        {
            throw new InvalidOperationException("يوجد تقييم آخر من نفس النوع قيد المعالجة بالفعل لهذا الخطر.");
        }
    }

    private static LikelihoodLevel ResolveLikelihood(RiskAssessmentMatrix matrix, Guid likelihoodLevelId) =>
        matrix.LikelihoodLevels.FirstOrDefault(l => l.Id == likelihoodLevelId)
            ?? throw new InvalidOperationException("مستوى الاحتمالية غير موجود ضمن المصفوفة النشطة.");

    private static void ValidateImpactRequest(IReadOnlyList<RiskAssessmentImpactRequest> impacts)
    {
        if (impacts.Count == 0)
        {
            throw new InvalidOperationException("يلزم تقييم بُعد أثر واحد على الأقل.");
        }

        if (impacts.Select(i => i.ImpactDimensionId).Distinct().Count() != impacts.Count)
        {
            throw new InvalidOperationException("لا يمكن تكرار نفس بُعد الأثر أكثر من مرة في التقييم نفسه.");
        }
    }

    private static List<(RiskAssessmentImpactRequest Request, ImpactLevel Level)> ResolveImpactLevels(RiskAssessmentMatrix matrix, IReadOnlyList<RiskAssessmentImpactRequest> impacts)
    {
        var impactLevels = new List<(RiskAssessmentImpactRequest Request, ImpactLevel Level)>();
        foreach (var impact in impacts)
        {
            var level = matrix.ImpactLevels.FirstOrDefault(l => l.Id == impact.ImpactLevelId && l.ImpactDimensionId == impact.ImpactDimensionId)
                ?? throw new InvalidOperationException("أحد مستويات الأثر غير موجود ضمن المصفوفة النشطة أو لا يطابق البُعد المحدد.");
            impactLevels.Add((impact, level));
        }

        return impactLevels;
    }

    private static Dictionary<Guid, decimal>? ResolveWeights(RiskAssessmentMatrix matrix)
    {
        if (matrix.ScoreFormula != ScoreFormulaType.LikelihoodTimesWeightedImpact)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(matrix.ImpactWeightingJson))
        {
            throw new InvalidOperationException("المصفوفة تستخدم صيغة الأثر الموزون لكن لا تحمل أوزان أبعاد محفوظة.");
        }

        return JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(matrix.ImpactWeightingJson);
    }

    private static void ValidateScoreDependentRequirements(RiskRatingBand band, RiskAssessmentCreateRequest request)
    {
        if (band.Severity is RiskRatingSeverity.High or RiskRatingSeverity.Critical && string.IsNullOrWhiteSpace(request.Rationale))
        {
            throw new InvalidOperationException("المبرر مطلوب للتقييمات ذات الدرجة العالية أو الحرجة.");
        }

        if (request.AssessmentType == AssessmentType.Closure && string.IsNullOrWhiteSpace(request.ClosureChangeSummary))
        {
            throw new InvalidOperationException("يجب توضيح ما تغيّر في تقييم الإغلاق.");
        }
    }

    private RiskAssessment BuildAssessment(
        RiskRecord risk, Guid riskId, RiskAssessmentCreateRequest request, RiskAssessmentMatrix matrix, LikelihoodLevel likelihood,
        List<(RiskAssessmentImpactRequest Request, ImpactLevel Level)> impactLevels, decimal score, RiskRatingBand band)
    {
        Guid? overallImpactLevelId = matrix.ScoreFormula == ScoreFormulaType.LikelihoodTimesMaximumImpact
            ? impactLevels.OrderByDescending(i => i.Level.NumericValue).First().Level.Id
            : null;

        var now = DateTimeOffset.UtcNow;
        var assessment = new RiskAssessment
        {
            RiskRecordId = riskId,
            AssessmentType = request.AssessmentType,
            MatrixId = matrix.Id,
            MatrixVersion = matrix.Version,
            LikelihoodLevelId = likelihood.Id,
            OverallImpactLevelId = overallImpactLevelId,
            CalculatedScore = score,
            RatingBandId = band.Id,
            Rationale = request.Rationale,
            AssessedAtUtc = now,
            AssessedBy = ActorReference(),
            Status = AssessmentStatus.Draft,
            SupersedesAssessmentId = SupersedesPointerFor(risk, request.AssessmentType),
            ClosureChangeSummary = request.ClosureChangeSummary,
            CreatedBy = ActorReference()
        };

        foreach (var (impactRequest, level) in impactLevels)
        {
            assessment.ImpactBreakdown.Add(new RiskAssessmentImpact
            {
                ImpactDimensionId = level.ImpactDimensionId,
                ImpactLevelId = level.Id,
                RationaleAr = impactRequest.RationaleAr,
                EvidenceReference = impactRequest.EvidenceReference,
                CreatedBy = ActorReference()
            });
        }

        return assessment;
    }

    private void TransitionRiskToUnderAssessmentIfNeeded(RiskRecord risk)
    {
        if (risk.Status != RiskStatus.Draft)
        {
            return;
        }

        RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.Draft, RiskStatus.UnderAssessment);
        risk.Status = RiskStatus.UnderAssessment;
        Db.Add(new RiskStatusHistory { RiskRecordId = risk.Id, FromStatus = RiskStatus.Draft, ToStatus = RiskStatus.UnderAssessment, ChangedBy = ActorReference(), Reason = "بدء التقييم." });
        Db.Update(risk);
    }

    private static Guid? SupersedesPointerFor(RiskRecord risk, AssessmentType type) => type switch
    {
        AssessmentType.Inherent => risk.CurrentInherentAssessmentId,
        AssessmentType.Residual or AssessmentType.Closure => risk.CurrentResidualAssessmentId,
        _ => risk.CurrentAssessmentId
    };

    private async Task<RiskAssessmentMatrix> ResolveMatrixAsync(Guid organizationId, Guid? matrixId, CancellationToken cancellationToken)
    {
        var query = Db.RiskAssessmentMatrices
            .Include(m => m.LikelihoodLevels)
            .Include(m => m.ImpactLevels)
            .Include(m => m.RatingBands)
            .Where(m => m.OrganizationId == organizationId && m.Status == MatrixStatus.Active);

        var matrix = matrixId is Guid id
            ? await query.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(m => m.IsDefault, cancellationToken);

        return matrix ?? throw new InvalidOperationException("لا توجد مصفوفة تقييم نشطة يمكن استخدامها. يجب اعتماد وتفعيل مصفوفة أولًا.");
    }

    public async Task SubmitAsync(Guid facilityId, Guid riskId, Guid assessmentId, RiskRowVersionRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksAssess);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var assessment = await LoadAssessmentAsync(riskId, assessmentId, cancellationToken);
        EnsureCurrentRowVersion(assessment, request.RowVersion);

        if (assessment.Status != AssessmentStatus.Draft)
        {
            throw new InvalidOperationException("لا يمكن إرسال إلا تقييم في حالة مسودة.");
        }

        assessment.Status = AssessmentStatus.PendingReview;
        Db.Update(assessment);

        if (risk.Status == RiskStatus.UnderAssessment && assessment.AssessmentType is AssessmentType.Inherent or AssessmentType.Current)
        {
            RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.UnderAssessment, RiskStatus.PendingReview);
            risk.Status = RiskStatus.PendingReview;
            Db.Add(new RiskStatusHistory { RiskRecordId = risk.Id, FromStatus = RiskStatus.UnderAssessment, ToStatus = RiskStatus.PendingReview, ChangedBy = ActorReference(), Reason = "إرسال التقييم للمراجعة." });
            Db.Update(risk);
        }

        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskAssessmentSubmitted, nameof(RiskAssessment), assessment.Id, null, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReviewAsync(Guid facilityId, Guid riskId, Guid assessmentId, RiskAssessmentReviewRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksReviewAssessment);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var assessment = await LoadAssessmentAsync(riskId, assessmentId, cancellationToken);
        EnsureCurrentRowVersion(assessment, request.RowVersion);
        EnforceFourEyes(assessment.AssessedBy);

        if (assessment.Status != AssessmentStatus.PendingReview)
        {
            throw new InvalidOperationException("لا يمكن مراجعة إلا تقييم بانتظار المراجعة.");
        }

        assessment.ReviewedAtUtc = DateTimeOffset.UtcNow;
        assessment.ReviewedBy = ActorReference();

        if (request.Approve)
        {
            assessment.Status = AssessmentStatus.Reviewed;
        }
        else
        {
            assessment.Status = AssessmentStatus.Draft;
            assessment.RejectionReason = request.Comments;

            if (risk.Status == RiskStatus.PendingReview)
            {
                RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.PendingReview, RiskStatus.UnderAssessment);
                risk.Status = RiskStatus.UnderAssessment;
                Db.Add(new RiskStatusHistory { RiskRecordId = risk.Id, FromStatus = RiskStatus.PendingReview, ToStatus = RiskStatus.UnderAssessment, ChangedBy = ActorReference(), Reason = "إعادة التقييم بعد المراجعة." });
                Db.Update(risk);
            }
        }

        Db.Update(assessment);
        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskAssessmentReviewed, nameof(RiskAssessment), assessment.Id, new { request.Approve }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid facilityId, Guid riskId, Guid assessmentId, RiskAssessmentApproveRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksApproveAssessment);
        var risk = await EnsureRiskVisibleAsync(facilityId, riskId, cancellationToken);
        var assessment = await LoadAssessmentAsync(riskId, assessmentId, cancellationToken);
        EnsureCurrentRowVersion(assessment, request.RowVersion);
        EnforceFourEyes(assessment.AssessedBy);

        if (assessment.Status != AssessmentStatus.Reviewed)
        {
            throw new InvalidOperationException("لا يمكن اعتماد إلا تقييم روجع مسبقًا.");
        }

        var now = DateTimeOffset.UtcNow;
        assessment.Status = AssessmentStatus.Approved;
        assessment.ApprovedAtUtc = now;
        assessment.ApprovedBy = ActorReference();
        Db.Update(assessment);

        if (assessment.SupersedesAssessmentId is Guid supersededId)
        {
            var superseded = await Db.RiskAssessments.FirstOrDefaultAsync(a => a.Id == supersededId, cancellationToken);
            if (superseded is not null)
            {
                superseded.Status = AssessmentStatus.Superseded;
                Db.Update(superseded);
            }
        }

        await ApplyApprovedAssessmentToRiskAsync(risk, assessment, cancellationToken);

        await Db.SaveChangesAsync(cancellationToken);
        await AuditAsync(RiskAuditActions.RiskAssessmentApproved, nameof(RiskAssessment), assessment.Id, new { assessment.CalculatedScore }, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyApprovedAssessmentToRiskAsync(RiskRecord risk, RiskAssessment assessment, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var band = await Db.RiskRatingBands.AsNoTracking().FirstAsync(b => b.Id == assessment.RatingBandId, cancellationToken);

        UpdateCachedAssessmentPointers(risk, assessment);

        if (ShouldUpdateCurrentView(risk, assessment))
        {
            await ApplyCurrentViewUpdateAsync(risk, assessment, band, cancellationToken);
        }

        RefreshReviewScheduleAndFreshness(risk, band, now);
        TransitionRiskToActiveIfPendingReview(risk, assessment);

        Db.Update(risk);
    }

    private static void UpdateCachedAssessmentPointers(RiskRecord risk, RiskAssessment assessment)
    {
        if (assessment.AssessmentType == AssessmentType.Inherent)
        {
            risk.CurrentInherentAssessmentId = assessment.Id;
        }

        if (assessment.AssessmentType == AssessmentType.Residual || assessment.AssessmentType == AssessmentType.Closure)
        {
            risk.CurrentResidualAssessmentId = assessment.Id;
        }
    }

    private bool ShouldUpdateCurrentView(RiskRecord risk, RiskAssessment assessment) =>
        (assessment.AssessmentType == AssessmentType.Inherent && risk.CurrentAssessmentId is null)
        || CurrentFamily.Contains(assessment.AssessmentType);

    private async Task ApplyCurrentViewUpdateAsync(RiskRecord risk, RiskAssessment assessment, RiskRatingBand band, CancellationToken cancellationToken)
    {
        var previous = await LoadPreviousComparisonAsync(risk.CurrentAssessmentId, cancellationToken);
        var current = await BuildCurrentComparisonAsync(assessment, band, cancellationToken);
        var hasNewSources = risk.CurrentAssessmentId is not null && await Db.RiskSourceLinks
            .AnyAsync(l => l.RiskRecordId == risk.Id && l.AddedAtUtc > (risk.DataFreshAsOfUtc ?? risk.FirstIdentifiedAtUtc), cancellationToken);

        var (trend, reason) = RiskTrendCalculator.Calculate(previous, current, hasNewSources);

        risk.CurrentAssessmentId = assessment.Id;
        risk.CurrentScore = assessment.CalculatedScore;
        risk.CurrentRatingBandId = assessment.RatingBandId;
        risk.CurrentTrend = trend;
        risk.CurrentTrendReasonAr = reason;
    }

    private async Task<RiskAssessmentComparisonInput?> LoadPreviousComparisonAsync(Guid? previousAssessmentId, CancellationToken cancellationToken)
    {
        if (previousAssessmentId is not Guid previousId)
        {
            return null;
        }

        var previousAssessment = await Db.RiskAssessments.AsNoTracking()
            .Include(a => a.LikelihoodLevel)
            .Include(a => a.ImpactBreakdown).ThenInclude(i => i.ImpactLevel)
            .Include(a => a.RatingBand)
            .FirstOrDefaultAsync(a => a.Id == previousId, cancellationToken);
        if (previousAssessment is null)
        {
            return null;
        }

        return new RiskAssessmentComparisonInput(
            previousAssessment.CalculatedScore,
            previousAssessment.LikelihoodLevel.NumericValue,
            previousAssessment.ImpactBreakdown.Count == 0 ? 0 : previousAssessment.ImpactBreakdown.Max(i => i.ImpactLevel.NumericValue),
            previousAssessment.RatingBand.Code);
    }

    private async Task<RiskAssessmentComparisonInput> BuildCurrentComparisonAsync(RiskAssessment assessment, RiskRatingBand band, CancellationToken cancellationToken)
    {
        var likelihood = await Db.LikelihoodLevels.AsNoTracking().FirstAsync(l => l.Id == assessment.LikelihoodLevelId, cancellationToken);
        var maxImpact = await Db.RiskAssessmentImpacts.AsNoTracking()
            .Where(i => i.RiskAssessmentId == assessment.Id)
            .Select(i => i.ImpactLevel.NumericValue)
            .ToListAsync(cancellationToken);

        return new RiskAssessmentComparisonInput(assessment.CalculatedScore, likelihood.NumericValue, maxImpact.Count == 0 ? 0 : maxImpact.Max(), band.Code);
    }

    private static void RefreshReviewScheduleAndFreshness(RiskRecord risk, RiskRatingBand band, DateTimeOffset now)
    {
        risk.LastReviewedAtUtc = now;
        risk.DataFreshAsOfUtc = now;
        if (band.ReviewFrequencyDays is int days)
        {
            risk.NextReviewDueAtUtc = now.AddDays(days);
        }
    }

    private void TransitionRiskToActiveIfPendingReview(RiskRecord risk, RiskAssessment assessment)
    {
        if (risk.Status != RiskStatus.PendingReview || assessment.AssessmentType is not (AssessmentType.Inherent or AssessmentType.Current))
        {
            return;
        }

        RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.PendingReview, RiskStatus.Active);
        var from = risk.Status;
        risk.Status = RiskStatus.Active;
        Db.Add(new RiskStatusHistory { RiskRecordId = risk.Id, FromStatus = from, ToStatus = RiskStatus.Active, ChangedBy = ActorReference(), Reason = "اعتماد التقييم." });
    }

    private async Task<RiskAssessment> LoadAssessmentAsync(Guid riskId, Guid assessmentId, CancellationToken cancellationToken) =>
        await Db.RiskAssessments
            .Include(a => a.ImpactBreakdown)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.RiskRecordId == riskId, cancellationToken)
        ?? throw new KeyNotFoundException("التقييم غير موجود.");
}
