namespace Baseera.Application.RiskManagement;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.RiskManagement;
using Microsoft.EntityFrameworkCore;

public sealed class RiskRegisterQueryService(IBaseeraDbContext db, ICurrentUser currentUser, IOrganizationalScopeService scope, IAuditService audit)
    : RiskServiceBase(db, currentUser, scope, audit), IRiskRegisterQueryService
{
    private static readonly HashSet<RiskStatus> OpenStatuses =
    [
        RiskStatus.Draft, RiskStatus.UnderAssessment, RiskStatus.PendingReview, RiskStatus.Active,
        RiskStatus.UnderTreatment, RiskStatus.Monitoring, RiskStatus.PendingAcceptance,
        RiskStatus.Accepted, RiskStatus.PendingClosure, RiskStatus.Reopened
    ];

    public async Task<IReadOnlyList<RiskCategoryDto>> ListCategoriesAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksViewSummary);
        return await Db.RiskCategories
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.NameAr)
            .Select(c => new RiskCategoryDto(c.Id, c.Code, c.NameAr, c.NameEn, c.ParentCategoryId, c.IsActive, c.DisplayOrder, Convert.ToBase64String(c.RowVersion)))
            .ToListAsync(cancellationToken);
    }

    public async Task<RiskWorkspaceSummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksViewSummary);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        return await BuildSummaryAsync(facilityId, cancellationToken);
    }

    /// <summary>
    /// Deliberately a single grouped-aggregate query (plus a handful of small unavoidable follow-ups) rather
    /// than one round trip per metric — this feeds the Facility Workspace widget on every page load, and the
    /// workspace's own query-count budget test caps total round trips regardless of how many risk metrics
    /// are surfaced (see docs/phase-d6-risk-performance.md).
    /// </summary>
    internal Task<RiskWorkspaceSummaryDto> BuildSummaryAsync(Guid facilityId, CancellationToken cancellationToken) =>
        BuildSummaryAsync(facilityId, precomputedOverdueTreatmentActions: null, cancellationToken);

    /// <summary>
    /// <paramref name="precomputedOverdueTreatmentActions"/> lets callers that already fetched the
    /// treatment-action rows for another purpose (the Intervention Queue feed) pass the count in and skip
    /// a redundant round trip — see RiskReadinessService.GetWorkspacePayloadAsync.
    /// </summary>
    internal async Task<RiskWorkspaceSummaryDto> BuildSummaryAsync(Guid facilityId, int? precomputedOverdueTreatmentActions, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var openQuery = Db.RiskRecords.AsNoTracking().Where(r => r.FacilityId == facilityId && OpenStatuses.Contains(r.Status));

        var aggregate = await openQuery
            .GroupBy(r => 1)
            .Select(g => new
            {
                OpenRisks = g.Count(),
                CriticalRisks = g.Count(r => r.CurrentRatingBand != null && r.CurrentRatingBand.Severity == RiskRatingSeverity.Critical),
                HighRisks = g.Count(r => r.CurrentRatingBand != null && r.CurrentRatingBand.Severity == RiskRatingSeverity.High),
                Increasing = g.Count(r => r.CurrentTrend == RiskTrend.Increasing),
                OverdueReview = g.Count(r => r.NextReviewDueAtUtc != null && r.NextReviewDueAtUtc < now),
                WithoutOwner = g.Count(r => r.OwnerWorkforceMemberId == null && r.OwnerUserId == null),
                WithoutTreatment = g.Count(r => (r.Status == RiskStatus.Active || r.Status == RiskStatus.UnderTreatment) && !r.TreatmentPlans.Any()),
                AcceptedNearingReview = g.Count(r => r.Status == RiskStatus.Accepted && r.AcceptedUntilUtc != null && r.AcceptedUntilUtc < now.AddDays(14)),
                StaleData = g.Count(r => r.DataFreshAsOfUtc == null || r.DataFreshAsOfUtc < now.AddDays(-90)),
                LastUpdated = g.Max(r => (DateTimeOffset?)(r.UpdatedAtUtc ?? r.CreatedAtUtc))
            })
            .FirstOrDefaultAsync(cancellationToken);

        var overdueActions = precomputedOverdueTreatmentActions ?? await Db.RiskTreatmentActions.AsNoTracking()
            .CountAsync(a => a.TreatmentPlan.RiskRecord.FacilityId == facilityId
                && a.DueAtUtc < now
                && a.Status != RiskTreatmentActionStatus.Completed
                && a.Status != RiskTreatmentActionStatus.Cancelled, cancellationToken);

        var recurringCount = await openQuery
            .GroupBy(r => r.RecurrenceKey)
            .CountAsync(g => g.Count() > 1, cancellationToken);

        // Folding this into the GroupBy(1) aggregate above via SUM(ticks) was tried per review feedback, but
        // EF Core's SQL Server provider cannot translate DateTimeOffset.UtcTicks inside an aggregate — it
        // throws at query-execution time. This single-column projection (already the lightest form: one
        // scalar column, not full RiskRecord rows) is the safe fallback.
        var openRiskAges = await openQuery.Select(r => r.FirstIdentifiedAtUtc).ToListAsync(cancellationToken);
        var averageAge = openRiskAges.Count == 0
            ? 0
            : openRiskAges.Average(firstIdentifiedAtUtc => (now - firstIdentifiedAtUtc).TotalDays);

        return new RiskWorkspaceSummaryDto(
            aggregate?.OpenRisks ?? 0,
            aggregate?.CriticalRisks ?? 0,
            aggregate?.HighRisks ?? 0,
            aggregate?.Increasing ?? 0,
            recurringCount,
            aggregate?.OverdueReview ?? 0,
            aggregate?.WithoutOwner ?? 0,
            aggregate?.WithoutTreatment ?? 0,
            overdueActions,
            aggregate?.AcceptedNearingReview ?? 0,
            aggregate?.StaleData ?? 0,
            Math.Round(averageAge, 1),
            aggregate?.LastUpdated);
    }

    private sealed record RiskListRow(
        Guid Id,
        string RiskCode,
        string Title,
        string CategoryNameAr,
        RiskType RiskType,
        RiskStatus Status,
        string? InherentCode,
        string? InherentLabel,
        string? ResidualCode,
        string? ResidualLabel,
        decimal? CurrentScore,
        RiskTrend CurrentTrend,
        string? OwnerName,
        TreatmentStrategy? TreatmentStrategy,
        DateTimeOffset FirstIdentifiedAtUtc,
        DateTimeOffset? NextReviewDueAtUtc,
        DateTimeOffset? DataFreshAsOfUtc,
        int SourceCount);

    public async Task<RiskPagedResult<RiskListItemDto>> ListAsync(Guid facilityId, RiskListFilters filters, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);
        var canSeeSensitive = User.HasPermission(PermissionCodes.RisksViewSensitive);

        var query = ApplyScope(facilityId);
        query = ApplyFilters(query, filters, canSeeSensitive);

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(filters.Page, 1);
        var pageSize = Math.Clamp(filters.PageSize, 1, 50);
        var now = DateTimeOffset.UtcNow;

        var rows = await ProjectListItems(query, page, pageSize).ToListAsync(cancellationToken);
        var mapped = rows.Select(row => MapListItem(row, now)).ToList();

        return new RiskPagedResult<RiskListItemDto>(mapped, page, pageSize, totalCount);
    }

    private IQueryable<RiskRecord> ApplyScope(Guid facilityId) =>
        Db.RiskRecords.AsNoTracking()
            .Include(r => r.RiskCategory)
            .Include(r => r.CurrentRatingBand)
            .Include(r => r.OwnerWorkforceMember)
            .Where(r => r.FacilityId == facilityId);

    private static IQueryable<RiskRecord> ApplyFilters(IQueryable<RiskRecord> query, RiskListFilters filters, bool canSeeSensitive)
    {
        if (!canSeeSensitive)
        {
            query = query.Where(r => r.ConfidentialityLevel == Domain.Attachments.ClassificationLevel.Internal);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(r => r.Title.Contains(term) || r.RiskCode.Contains(term));
        }

        if (filters.Status is RiskStatus status)
        {
            query = query.Where(r => r.Status == status);
        }

        if (filters.Severity is RiskRatingSeverity severity)
        {
            query = query.Where(r => r.CurrentRatingBand != null && r.CurrentRatingBand.Severity == severity);
        }

        if (filters.Trend is RiskTrend trend)
        {
            query = query.Where(r => r.CurrentTrend == trend);
        }

        if (filters.CategoryId is Guid categoryId)
        {
            query = query.Where(r => r.RiskCategoryId == categoryId);
        }

        query = ApplyOwnerFilter(query, filters);

        if (filters.WithoutTreatment == true)
        {
            query = query.Where(r => !r.TreatmentPlans.Any());
        }

        return query;
    }

    private static IQueryable<RiskRecord> ApplyOwnerFilter(IQueryable<RiskRecord> query, RiskListFilters filters)
    {
        if (filters.WithoutOwner == true)
        {
            return query.Where(r => r.OwnerWorkforceMemberId == null && r.OwnerUserId == null);
        }

        if (filters.OwnerWorkforceMemberId is Guid ownerId)
        {
            return query.Where(r => r.OwnerWorkforceMemberId == ownerId);
        }

        return query;
    }

    private static IQueryable<RiskListRow> ProjectListItems(IQueryable<RiskRecord> query, int page, int pageSize) =>
        query
            .OrderByDescending(r => r.CurrentScore ?? -1)
            .ThenByDescending(r => r.FirstIdentifiedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RiskListRow(
                r.Id,
                r.RiskCode,
                r.Title,
                r.RiskCategory.NameAr,
                r.RiskType,
                r.Status,
                r.CurrentInherentAssessment != null ? r.CurrentInherentAssessment.RatingBand.Code : null,
                r.CurrentInherentAssessment != null ? r.CurrentInherentAssessment.RatingBand.LabelAr : null,
                r.CurrentResidualAssessment != null ? r.CurrentResidualAssessment.RatingBand.Code : null,
                r.CurrentResidualAssessment != null ? r.CurrentResidualAssessment.RatingBand.LabelAr : null,
                r.CurrentScore,
                r.CurrentTrend,
                r.OwnerWorkforceMember != null ? r.OwnerWorkforceMember.DisplayName : null,
                r.TreatmentStrategy,
                r.FirstIdentifiedAtUtc,
                r.NextReviewDueAtUtc,
                r.DataFreshAsOfUtc,
                r.SourceLinks.Count(l => !l.IsDeleted)));

    private static RiskListItemDto MapListItem(RiskListRow row, DateTimeOffset now) => new(
        row.Id,
        row.RiskCode,
        row.Title,
        row.CategoryNameAr,
        row.RiskType,
        RiskManagementDisplay.RiskTypeAr(row.RiskType),
        row.Status,
        RiskManagementDisplay.StatusAr(row.Status),
        row.InherentCode,
        row.InherentLabel,
        row.ResidualCode,
        row.ResidualLabel,
        row.CurrentScore,
        row.CurrentTrend,
        RiskManagementDisplay.TrendAr(row.CurrentTrend),
        row.OwnerName,
        row.TreatmentStrategy,
        row.TreatmentStrategy.HasValue ? RiskManagementDisplay.TreatmentStrategyAr(row.TreatmentStrategy.Value) : null,
        row.FirstIdentifiedAtUtc,
        row.NextReviewDueAtUtc,
        (int)(now - row.FirstIdentifiedAtUtc).TotalDays,
        row.SourceCount,
        row.DataFreshAsOfUtc is null || row.DataFreshAsOfUtc < now.AddDays(-90),
        PrimaryActionFor(row.Status));

    private static string PrimaryActionFor(RiskStatus status) => status switch
    {
        RiskStatus.Draft => "StartAssessment",
        RiskStatus.UnderAssessment => "ContinueAssessment",
        RiskStatus.PendingReview => "ReviewAssessment",
        RiskStatus.Active => "PlanTreatment",
        RiskStatus.UnderTreatment => "TrackTreatment",
        RiskStatus.Monitoring => "PeriodicReview",
        RiskStatus.PendingAcceptance => "DecideAcceptance",
        RiskStatus.Accepted => "MonitorAcceptance",
        RiskStatus.PendingClosure => "DecideClosure",
        RiskStatus.Closed => "ViewOnly",
        RiskStatus.Reopened => "StartAssessment",
        _ => "ViewOnly"
    };

    public async Task<RiskDetailDto?> GetAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.RisksView);
        await EnsureFacilityVisibleAsync(facilityId, cancellationToken);

        var risk = await Db.RiskRecords.AsNoTracking()
            .Include(r => r.RiskCategory)
            .Include(r => r.OwnerWorkforceMember)
            .FirstOrDefaultAsync(r => r.Id == riskId && r.FacilityId == facilityId, cancellationToken);
        if (risk is null)
        {
            return null;
        }

        if (risk.ConfidentialityLevel != Domain.Attachments.ClassificationLevel.Internal && !User.HasPermission(PermissionCodes.RisksViewSensitive))
        {
            throw new KeyNotFoundException(RiskNotFoundMessage);
        }

        var inherent = await BuildScoreExplanationAsync(risk.CurrentInherentAssessmentId, cancellationToken);
        var current = await BuildScoreExplanationAsync(risk.CurrentAssessmentId, cancellationToken);
        var residual = await BuildScoreExplanationAsync(risk.CurrentResidualAssessmentId, cancellationToken);

        var sourceCount = await Db.RiskSourceLinks.CountAsync(l => l.RiskRecordId == riskId, cancellationToken);
        var openControls = await Db.RiskControls.CountAsync(c => c.RiskRecordId == riskId && c.ControlStatus != RiskControlStatus.Retired, cancellationToken);
        var openPlans = await Db.RiskTreatmentPlans.CountAsync(p => p.RiskRecordId == riskId
            && p.Status != TreatmentPlanStatus.Completed && p.Status != TreatmentPlanStatus.Cancelled && p.Status != TreatmentPlanStatus.Rejected, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var overdueActions = await Db.RiskTreatmentActions.CountAsync(a => a.TreatmentPlan.RiskRecordId == riskId
            && a.DueAtUtc < now && a.Status != RiskTreatmentActionStatus.Completed && a.Status != RiskTreatmentActionStatus.Cancelled, cancellationToken);

        var otherStatuses = await Db.RiskRecords.AsNoTracking()
            .Where(r => r.FacilityId == facilityId && r.RecurrenceKey == risk.RecurrenceKey && r.Id != riskId)
            .Select(r => r.Status)
            .ToListAsync(cancellationToken);
        var recurrence = RiskRecurrenceDetector.Detect(otherStatuses);

        return new RiskDetailDto(
            risk.Id,
            risk.RiskCode,
            risk.Title,
            risk.Description,
            risk.RiskCategoryId,
            risk.RiskCategory.NameAr,
            risk.RiskType,
            RiskManagementDisplay.RiskTypeAr(risk.RiskType),
            risk.Status,
            RiskManagementDisplay.StatusAr(risk.Status),
            risk.TreatmentStrategy,
            risk.TreatmentStrategy.HasValue ? RiskManagementDisplay.TreatmentStrategyAr(risk.TreatmentStrategy.Value) : null,
            risk.ConfidentialityLevel,
            risk.FacilityId,
            risk.FacilityUnitId,
            risk.OwnerWorkforceMemberId,
            risk.OwnerWorkforceMember?.DisplayName,
            risk.FirstIdentifiedAtUtc,
            risk.LastReviewedAtUtc,
            risk.NextReviewDueAtUtc,
            risk.AcceptedUntilUtc,
            risk.ClosedAtUtc,
            risk.ClosureReason,
            risk.ReopenedCount,
            inherent,
            current,
            residual,
            risk.CurrentTrend,
            RiskManagementDisplay.TrendAr(risk.CurrentTrend),
            risk.CurrentTrendReasonAr ?? "لا يوجد تقييم كافٍ لتحديد الاتجاه.",
            recurrence,
            sourceCount,
            openControls,
            openPlans,
            overdueActions,
            risk.DataFreshAsOfUtc is null || risk.DataFreshAsOfUtc < now.AddDays(-90),
            ComputeAllowedActions(risk),
            Convert.ToBase64String(risk.RowVersion));
    }

    private async Task<RiskScoreExplanationDto?> BuildScoreExplanationAsync(Guid? assessmentId, CancellationToken cancellationToken)
    {
        if (assessmentId is null)
        {
            return null;
        }

        var assessment = await Db.RiskAssessments.AsNoTracking()
            .Include(a => a.Matrix)
            .Include(a => a.LikelihoodLevel)
            .Include(a => a.RatingBand)
            .Include(a => a.ImpactBreakdown).ThenInclude(i => i.ImpactDimension)
            .Include(a => a.ImpactBreakdown).ThenInclude(i => i.ImpactLevel)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return null;
        }

        var formulaAr = assessment.Matrix.ScoreFormula == ScoreFormulaType.LikelihoodTimesMaximumImpact
            ? "الاحتمالية × أعلى قيمة أثر"
            : "الاحتمالية × متوسط الأثر الموزون";

        return new RiskScoreExplanationDto(
            assessment.Matrix.Code,
            assessment.MatrixVersion,
            formulaAr,
            assessment.LikelihoodLevel.Name,
            assessment.LikelihoodLevel.NumericValue,
            assessment.ImpactBreakdown.Select(i => new RiskImpactBreakdownDto(
                i.ImpactDimension.NameAr, i.ImpactLevel.Name, i.ImpactLevel.NumericValue, i.RationaleAr)).ToList(),
            assessment.CalculatedScore,
            assessment.RatingBand.Code,
            assessment.RatingBand.LabelAr);
    }

    private List<string> ComputeAllowedActions(RiskRecord risk)
    {
        var actions = new List<string>();
        actions.AddRange(BuildActiveWorkActions(risk));
        actions.AddRange(BuildLifecycleTransitionActions(risk));
        actions.AddRange(BuildDecisionActions(risk));

        if (User.HasPermission(PermissionCodes.RisksUpdate))
        {
            actions.Add("Update");
        }

        return actions;
    }

    /// <summary>Actions available only while the risk is neither Closed nor Archived.</summary>
    private IEnumerable<string> BuildActiveWorkActions(RiskRecord risk)
    {
        if (risk.Status is RiskStatus.Closed or RiskStatus.Archived)
        {
            yield break;
        }

        if (User.HasPermission(PermissionCodes.RisksAssess)) yield return "Assess";
        if (User.HasPermission(PermissionCodes.RisksManageControls)) yield return "AddControl";
        if (User.HasPermission(PermissionCodes.RisksManageTreatments)) yield return "CreateTreatmentPlan";
        if (User.HasPermission(PermissionCodes.RisksLinkSources)) yield return "LinkSource";
        if (User.HasPermission(PermissionCodes.RisksAssignOwner)) yield return "AssignOwner";
        if (User.HasPermission(PermissionCodes.RisksEscalate)) yield return "Escalate";
    }

    /// <summary>Actions that request a lifecycle transition the state machine itself allows from the current status.</summary>
    private IEnumerable<string> BuildLifecycleTransitionActions(RiskRecord risk)
    {
        if (RiskLifecycleStateMachine.CanTransition(risk.Status, RiskStatus.Monitoring) && User.HasPermission(PermissionCodes.RisksUpdate))
        {
            yield return "StartMonitoring";
        }

        if (RiskLifecycleStateMachine.CanTransition(risk.Status, RiskStatus.PendingAcceptance) && User.HasPermission(PermissionCodes.RisksRequestAcceptance))
        {
            yield return "RequestAcceptance";
        }

        if (RiskLifecycleStateMachine.CanTransition(risk.Status, RiskStatus.PendingClosure) && User.HasPermission(PermissionCodes.RisksRequestClosure))
        {
            yield return "RequestClosure";
        }

        if (RiskLifecycleStateMachine.CanTransition(risk.Status, RiskStatus.Reopened) && User.HasPermission(PermissionCodes.RisksReopen))
        {
            yield return "Reopen";
        }

        if (RiskLifecycleStateMachine.CanTransition(risk.Status, RiskStatus.Archived) && User.HasPermission(PermissionCodes.RisksUpdate))
        {
            yield return "Archive";
        }
    }

    /// <summary>Four-eyes decision actions available only while a pending review sits in the matching status.</summary>
    private IEnumerable<string> BuildDecisionActions(RiskRecord risk)
    {
        if (risk.Status == RiskStatus.PendingAcceptance && User.HasPermission(PermissionCodes.RisksApproveAcceptance))
        {
            yield return "DecideAcceptance";
        }

        if (risk.Status == RiskStatus.PendingClosure && User.HasPermission(PermissionCodes.RisksApproveClosure))
        {
            yield return "DecideClosure";
        }
    }
}
