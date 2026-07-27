namespace Baseera.Application.RiskManagement;

using Baseera.Domain.Attachments;
using Baseera.Domain.RiskManagement;

// ---------- Categories ----------

public sealed record RiskCategoryDto(
    Guid Id,
    string Code,
    string NameAr,
    string? NameEn,
    Guid? ParentCategoryId,
    bool IsActive,
    int DisplayOrder,
    string RowVersion);

public sealed class RiskCategoryUpsertRequest
{
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public string? NameEn { get; init; }
    public Guid? ParentCategoryId { get; init; }
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public string? RowVersion { get; init; }
}

// ---------- Risk register ----------

public sealed record RiskListItemDto(
    Guid Id,
    string RiskCode,
    string Title,
    string CategoryNameAr,
    RiskType RiskType,
    string RiskTypeAr,
    RiskStatus Status,
    string StatusAr,
    string? InherentRatingCode,
    string? InherentRatingLabelAr,
    string? ResidualRatingCode,
    string? ResidualRatingLabelAr,
    decimal? CurrentScore,
    RiskTrend Trend,
    string TrendAr,
    string? OwnerDisplayName,
    TreatmentStrategy? TreatmentStrategy,
    string? TreatmentStrategyAr,
    DateTimeOffset FirstIdentifiedAtUtc,
    DateTimeOffset? NextReviewDueAtUtc,
    int AgeDays,
    int SourceCount,
    bool IsDataStale,
    string AllowedPrimaryAction);

public sealed record RiskListFilters
{
    public string? Search { get; init; }
    public RiskStatus? Status { get; init; }
    public RiskRatingSeverity? Severity { get; init; }
    public RiskTrend? Trend { get; init; }
    public Guid? OwnerWorkforceMemberId { get; init; }
    public bool? WithoutOwner { get; init; }
    public bool? WithoutTreatment { get; init; }
    public Guid? CategoryId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record RiskPagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record RiskScoreExplanationDto(
    string MatrixCode,
    int MatrixVersion,
    string FormulaAr,
    string LikelihoodLabelAr,
    int LikelihoodValue,
    IReadOnlyList<RiskImpactBreakdownDto> ImpactBreakdown,
    decimal CalculatedScore,
    string RatingBandCode,
    string RatingBandLabelAr);

public sealed record RiskImpactBreakdownDto(string DimensionNameAr, string ImpactLevelNameAr, int NumericValue, string? RationaleAr);

public sealed record RiskDetailDto(
    Guid Id,
    string RiskCode,
    string Title,
    string? Description,
    Guid RiskCategoryId,
    string CategoryNameAr,
    RiskType RiskType,
    string RiskTypeAr,
    RiskStatus Status,
    string StatusAr,
    TreatmentStrategy? TreatmentStrategy,
    string? TreatmentStrategyAr,
    ClassificationLevel ConfidentialityLevel,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerWorkforceMemberId,
    string? OwnerDisplayName,
    DateTimeOffset FirstIdentifiedAtUtc,
    DateTimeOffset? LastReviewedAtUtc,
    DateTimeOffset? NextReviewDueAtUtc,
    DateTimeOffset? AcceptedUntilUtc,
    DateTimeOffset? ClosedAtUtc,
    string? ClosureReason,
    int ReopenedCount,
    RiskScoreExplanationDto? InherentAssessment,
    RiskScoreExplanationDto? CurrentAssessment,
    RiskScoreExplanationDto? ResidualAssessment,
    RiskTrend Trend,
    string TrendAr,
    string TrendReasonAr,
    RecurrencePatternKind RecurrencePattern,
    int SourceCount,
    int OpenControlCount,
    int OpenTreatmentPlanCount,
    int OverdueTreatmentActionCount,
    bool IsDataStale,
    IReadOnlyList<string> AllowedActions,
    string RowVersion);

public sealed class RiskCreateRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required Guid RiskCategoryId { get; init; }
    public required RiskType RiskType { get; init; }
    public ClassificationLevel ConfidentialityLevel { get; init; } = ClassificationLevel.Internal;
    public Guid? FacilityUnitId { get; init; }
    public Guid? OwnerWorkforceMemberId { get; init; }
    public RiskOriginType SourceType { get; init; } = RiskOriginType.Manual;
    public string? SourceReference { get; init; }
}

public sealed class RiskUpdateRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required Guid RiskCategoryId { get; init; }
    public required RiskType RiskType { get; init; }
    public ClassificationLevel ConfidentialityLevel { get; init; } = ClassificationLevel.Internal;
    public Guid? FacilityUnitId { get; init; }
    public required string RowVersion { get; init; }
}

public static class RiskCommandTypes
{
    public const string AssignOwner = "AssignOwner";
    public const string StartMonitoring = "StartMonitoring";
    public const string Escalate = "Escalate";
    public const string Reopen = "Reopen";
    public const string Archive = "Archive";
}

public sealed class RiskCommandRequest
{
    public required string Command { get; init; }
    public Guid? OwnerWorkforceMemberId { get; init; }
    public Guid? OwnerUserId { get; init; }
    public string? Reason { get; init; }
    public required string RowVersion { get; init; }
}

// ---------- Matrices ----------

public sealed record RiskRatingBandDto(
    Guid Id,
    string Code,
    string LabelAr,
    decimal MinimumScore,
    decimal MaximumScore,
    RiskRatingSeverity Severity,
    int? ResponseTimeHours,
    bool EscalationRequired,
    int? ReviewFrequencyDays,
    string ColorToken);

public sealed record RiskLikelihoodLevelDto(Guid Id, string Code, string Name, int NumericValue, string? Description);

public sealed record RiskImpactLevelDto(Guid Id, Guid ImpactDimensionId, string DimensionNameAr, string Code, string Name, int NumericValue);

public sealed record RiskMatrixDto(
    Guid Id,
    string Code,
    string Name,
    int Version,
    MatrixStatus Status,
    ScoreFormulaType ScoreFormula,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    bool IsDefault,
    IReadOnlyList<RiskLikelihoodLevelDto> LikelihoodLevels,
    IReadOnlyList<RiskImpactLevelDto> ImpactLevels,
    IReadOnlyList<RiskRatingBandDto> RatingBands,
    string RowVersion);

public class RiskMatrixLevelRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required int NumericValue { get; init; }
    public string? Description { get; init; }
    public string? Criteria { get; init; }
}

public sealed class RiskMatrixImpactLevelRequest : RiskMatrixLevelRequest
{
    public required Guid ImpactDimensionId { get; init; }
}

public sealed class RiskMatrixRatingBandRequest
{
    public required string Code { get; init; }
    public required string LabelAr { get; init; }
    public required decimal MinimumScore { get; init; }
    public required decimal MaximumScore { get; init; }
    public required RiskRatingSeverity Severity { get; init; }
    public int? ResponseTimeHours { get; init; }
    public bool EscalationRequired { get; init; }
    public int? ReviewFrequencyDays { get; init; }
    public required string ColorToken { get; init; }
}

public sealed class RiskMatrixCreateRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required ScoreFormulaType ScoreFormula { get; init; }
    public IReadOnlyDictionary<Guid, decimal>? ImpactDimensionWeights { get; init; }
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public bool IsDefault { get; init; }
    public required IReadOnlyList<RiskMatrixLevelRequest> LikelihoodLevels { get; init; }
    public required IReadOnlyList<RiskMatrixImpactLevelRequest> ImpactLevels { get; init; }
    public required IReadOnlyList<RiskMatrixRatingBandRequest> RatingBands { get; init; }

    /// <summary>When set, this new Draft matrix is versioned from the given (typically Active) matrix.</summary>
    public Guid? PreviousVersionMatrixId { get; init; }
}

// ---------- Assessments ----------

public sealed class RiskAssessmentImpactRequest
{
    public required Guid ImpactDimensionId { get; init; }
    public required Guid ImpactLevelId { get; init; }
    public string? RationaleAr { get; init; }
    public string? EvidenceReference { get; init; }
}

public sealed class RiskAssessmentCreateRequest
{
    public required AssessmentType AssessmentType { get; init; }
    public Guid? MatrixId { get; init; }
    public required Guid LikelihoodLevelId { get; init; }
    public required IReadOnlyList<RiskAssessmentImpactRequest> Impacts { get; init; }
    public string? Rationale { get; init; }
    public string? ClosureChangeSummary { get; init; }
}

public sealed record RiskAssessmentDto(
    Guid Id,
    AssessmentType AssessmentType,
    string AssessmentTypeAr,
    AssessmentStatus Status,
    string StatusAr,
    decimal CalculatedScore,
    string RatingBandCode,
    string RatingBandLabelAr,
    string? Rationale,
    DateTimeOffset AssessedAtUtc,
    string AssessedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewedBy,
    DateTimeOffset? ApprovedAtUtc,
    string? ApprovedBy,
    string? RejectionReason,
    string RowVersion);

public sealed class RiskAssessmentReviewRequest
{
    public required bool Approve { get; init; }
    public string? Comments { get; init; }
    public required string RowVersion { get; init; }
}

public sealed class RiskAssessmentApproveRequest
{
    public string? Comments { get; init; }
    public required string RowVersion { get; init; }
}

public sealed class RiskRowVersionRequest
{
    public required string RowVersion { get; init; }
}

// ---------- Controls ----------

public sealed record RiskControlDto(
    Guid Id,
    RiskControlType ControlType,
    string ControlTypeAr,
    string Title,
    string? Description,
    Guid? OwnerWorkforceMemberId,
    string? OwnerDisplayName,
    RiskControlStatus ControlStatus,
    ControlEffectiveness ControlEffectiveness,
    string ControlEffectivenessAr,
    DateTimeOffset? ImplementedAtUtc,
    DateTimeOffset? LastTestedAtUtc,
    DateTimeOffset? NextTestDueAtUtc,
    bool EvidenceRequired,
    string RowVersion);

public sealed class RiskControlCreateRequest
{
    public required RiskControlType ControlType { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Guid? OwnerWorkforceMemberId { get; init; }
    public bool EvidenceRequired { get; init; }
    public string? SourceReference { get; init; }
}

public sealed class RiskControlTestRequest
{
    public required ControlEffectiveness ControlEffectiveness { get; init; }
    public DateTimeOffset? NextTestDueAtUtc { get; init; }
    public required string RowVersion { get; init; }
}

// ---------- Treatment ----------

public sealed record RiskTreatmentActionDto(
    Guid Id,
    string Title,
    string? Description,
    RiskTreatmentActionStatus Status,
    string StatusAr,
    RiskPriority Priority,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool IsOverdue,
    bool CompletionEvidenceRequired,
    string? CompletionSummary,
    string? BlockedReason,
    Guid? AssignedToWorkforceMemberId,
    string? AssignedToDisplayName,
    string RowVersion);

public sealed record RiskTreatmentPlanDto(
    Guid Id,
    string Title,
    string Objective,
    TreatmentStrategy Strategy,
    string StrategyAr,
    TreatmentPlanStatus Status,
    string StatusAr,
    bool IsOverdue,
    RiskPriority Priority,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? CompletedAtUtc,
    decimal? TargetScore,
    Guid? OwnerWorkforceMemberId,
    string? OwnerDisplayName,
    RiskApprovalStatus ApprovalStatus,
    IReadOnlyList<RiskTreatmentActionDto> Actions,
    string RowVersion);

public sealed class RiskTreatmentPlanCreateRequest
{
    public required TreatmentStrategy Strategy { get; init; }
    public required string Title { get; init; }
    public required string Objective { get; init; }
    public Guid? OwnerWorkforceMemberId { get; init; }
    public RiskPriority Priority { get; init; } = RiskPriority.Medium;
    public DateTimeOffset? PlannedStartAtUtc { get; init; }
    public required DateTimeOffset DueAtUtc { get; init; }
    public Guid? TargetLikelihoodLevelId { get; init; }
    public Guid? TargetImpactLevelId { get; init; }
    public decimal? TargetScore { get; init; }
}

public static class RiskTreatmentPlanCommandTypes
{
    public const string Submit = "Submit";
    public const string Approve = "Approve";
    public const string Reject = "Reject";
    public const string Start = "Start";
    public const string Block = "Block";
    public const string Unblock = "Unblock";
    public const string Complete = "Complete";
    public const string Cancel = "Cancel";
}

public sealed class RiskTreatmentPlanCommandRequest
{
    public required string Command { get; init; }
    public string? Reason { get; init; }
    public required string RowVersion { get; init; }
}

public sealed class RiskTreatmentActionCreateRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Guid? AssignedToWorkforceMemberId { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public RiskPriority Priority { get; init; } = RiskPriority.Medium;
    public DateTimeOffset? StartAtUtc { get; init; }
    public required DateTimeOffset DueAtUtc { get; init; }
    public bool CompletionEvidenceRequired { get; init; }
    public Guid? DependencyActionId { get; init; }
}

public static class RiskTreatmentActionCommandTypes
{
    public const string Assign = "Assign";
    public const string Start = "Start";
    public const string Block = "Block";
    public const string Unblock = "Unblock";
    public const string SubmitForVerification = "SubmitForVerification";
    public const string Verify = "Verify";
    public const string ReturnForRework = "ReturnForRework";
    public const string Cancel = "Cancel";
}

public sealed class RiskTreatmentActionCommandRequest
{
    public required string Command { get; init; }
    public string? Reason { get; init; }
    public string? CompletionSummary { get; init; }
    public required string RowVersion { get; init; }
}

// ---------- Reviews ----------

public sealed record RiskReviewDto(
    Guid Id,
    RiskReviewType ReviewType,
    string ReviewTypeAr,
    string SubjectReferenceType,
    Guid? SubjectReferenceId,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc,
    RiskReviewStatus Status,
    RiskReviewDecision? Decision,
    string? Comments,
    DateTimeOffset? CompletedAtUtc,
    string RowVersion);

public sealed class RiskReviewRequestDto
{
    public required RiskReviewType ReviewType { get; init; }
    public string? SubjectReferenceType { get; init; }
    public Guid? SubjectReferenceId { get; init; }
    public string? Comments { get; init; }

    /// <summary>Required only when ReviewType = RiskAcceptance.</summary>
    public DateTimeOffset? AcceptedUntilUtc { get; init; }
    public int? ReviewFrequencyDays { get; init; }

    /// <summary>Required only when ReviewType = ClosureApproval.</summary>
    public string? ClosureReason { get; init; }
}

public sealed class RiskReviewDecisionRequest
{
    public required RiskReviewDecision Decision { get; init; }
    public string? Comments { get; init; }
    public required string RowVersion { get; init; }
}

// ---------- Source links ----------

public sealed record RiskSourceLinkDto(
    Guid Id,
    RiskSourceEntityType SourceEntityType,
    Guid SourceEntityId,
    RiskSourceRelationshipType RelationshipType,
    DateTimeOffset AddedAtUtc,
    string AddedBy,
    string? Rationale);

public sealed class RiskSourceLinkCreateRequest
{
    public required RiskSourceEntityType SourceEntityType { get; init; }
    public required Guid SourceEntityId { get; init; }
    public required RiskSourceRelationshipType RelationshipType { get; init; }
    public string? Rationale { get; init; }
}

public sealed class RiskSourceLinkRemoveRequest
{
    public required string RemovalReason { get; init; }
}

// ---------- Readiness / summary / interventions ----------

public sealed record RiskWorkspaceSummaryDto(
    int OpenRisks,
    int CriticalRisks,
    int HighRisks,
    int IncreasingTrendRisks,
    int RecurringRisks,
    int OverdueReviewRisks,
    int RisksWithoutOwner,
    int RisksWithoutTreatment,
    int OverdueTreatmentActions,
    int AcceptedRisksNearingReview,
    int StaleDataRisks,
    double AverageOpenRiskAgeDays,
    DateTimeOffset? LastUpdatedAtUtc);

public sealed record RiskInterventionItemDto(
    string InterventionType,
    string SeverityAr,
    int PriorityRank,
    Guid RiskRecordId,
    string RiskCode,
    string TitleAr,
    string ReasonAr,
    DateTimeOffset? DueAtUtc,
    string? OwnerAr,
    string PrimaryActionAr);

// ---------- Data quality ----------

public sealed record RiskDataQualityIssueDto(
    string Code,
    string SeverityAr,
    int Count,
    string ImpactAr,
    string SourceEntity,
    string ResponsibleRoleAr,
    string CorrectiveActionAr);

public sealed record RiskDataQualityPayload(IReadOnlyList<RiskDataQualityIssueDto> Issues, DateTimeOffset GeneratedAtUtc);

// ---------- Import ----------

public sealed class RiskImportRow
{
    public required string RowKey { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string CategoryCode { get; init; }
    public required RiskType RiskType { get; init; }
    public string? OwnerWorkforceMemberCode { get; init; }
    public required Guid MatrixId { get; init; }
    public required string LikelihoodCode { get; init; }
    public required IReadOnlyDictionary<string, string> ImpactCodesByDimensionCode { get; init; }
}

public sealed class RiskImportPreviewRequest
{
    public required string SourceSystem { get; init; }
    public required string SourceReference { get; init; }
    public required string FileHash { get; init; }
    public required IReadOnlyList<RiskImportRow> Rows { get; init; }
}

public sealed record RiskImportRowResult(string RowKey, bool IsValid, bool IsDuplicate, IReadOnlyList<string> Errors);

public sealed record RiskImportResult(
    Guid BatchId,
    int TotalRows,
    int ValidRows,
    int RejectedRows,
    int DuplicateRows,
    int AppliedRows,
    IReadOnlyList<RiskImportRowResult> RowResults);

public sealed record RiskReconciliationItemDto(string ItemKey, string DescriptionAr, string SeverityAr);

/// <summary>Facility Workspace integration payload — mirrors SensitiveCustodyWorkspacePayload's shape.</summary>
public sealed record RiskWorkspacePayload
{
    public required RiskWorkspaceSummaryDto Summary { get; init; }
    public required IReadOnlyList<RiskInterventionItemDto> Interventions { get; init; }
}

public sealed class RiskReconciliationResolveRequest
{
    public required string ItemKey { get; init; }
    public required string Action { get; init; }
    public required string Reason { get; init; }
}
