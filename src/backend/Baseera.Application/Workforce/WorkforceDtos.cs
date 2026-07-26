namespace Baseera.Application.Workforce;

using Baseera.Domain.Workforce;

public sealed record WorkforceSummaryDto
{
    public required Guid FacilityId { get; init; }
    public required int TotalMembers { get; init; }
    public required int OperationallyEligible { get; init; }
    public required int Required { get; init; }
    public required int MinimumSafe { get; init; }
    public required int Scheduled { get; init; }
    public required int Present { get; init; }
    public required int OperationallyAvailable { get; init; }
    public required int OnLeave { get; init; }
    public required int InTraining { get; init; }
    public required int Restricted { get; init; }
    public required int Gap { get; init; }
    public required int SafeGap { get; init; }
    public required decimal? CoverageRate { get; init; }
    public required decimal? QualificationCoverage { get; init; }
    public required WorkforceCoverageStatus CoverageStatus { get; init; }
    public required int CriticalPositionsAtRisk { get; init; }
    public required int StaleRecords { get; init; }
    public required int MissingDataRecords { get; init; }
    public required string FreshnessStatus { get; init; }
    public required string ConfidenceLevel { get; init; }
    public required bool IsPartial { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<string> FatigueIndicators { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public DateTimeOffset? DataEffectiveAtUtc { get; init; }
}

public sealed record WorkforceCoverageRowDto
{
    public required Guid RoleDefinitionId { get; init; }
    public required string RoleCode { get; init; }
    public required string RoleNameAr { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public string? UnitNameAr { get; init; }
    public Guid? ShiftDefinitionId { get; init; }
    public string? ShiftCode { get; init; }
    public required int Required { get; init; }
    public required int MinimumSafe { get; init; }
    public required int Scheduled { get; init; }
    public required int Present { get; init; }
    public required int OperationallyAvailable { get; init; }
    public required int Gap { get; init; }
    public required int SafeGap { get; init; }
    public required decimal? CoverageRate { get; init; }
    public required WorkforceCoverageStatus CoverageStatus { get; init; }
}

public sealed record WorkforceUnitCoverageDto
{
    public required Guid? FacilityUnitId { get; init; }
    public required string UnitNameAr { get; init; }
    public required int Required { get; init; }
    public required int OperationallyAvailable { get; init; }
    public required int Gap { get; init; }
    public required decimal? CoverageRate { get; init; }
    public required WorkforceCoverageStatus CoverageStatus { get; init; }
}

public sealed record WorkforceRoleDefinitionDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public string? NameEn { get; init; }
    public required WorkforceRoleCategory Category { get; init; }
    public required WorkforceRoleCriticality Criticality { get; init; }
    public required bool RequiresCertification { get; init; }
    public required bool IsShiftBased { get; init; }
    public required bool IsSensitive { get; init; }
}

public sealed record WorkforceMemberListItemDto
{
    public required Guid Id { get; init; }
    public required string EmployeeNumber { get; init; }
    public required string DisplayName { get; init; }
    public required EmploymentStatus EmploymentStatus { get; init; }
    public required string JobTitle { get; init; }
    public required string PrimarySpecialty { get; init; }
    public Guid? CurrentOperationalUnitId { get; init; }
    public string? CurrentOperationalUnitNameAr { get; init; }
    public required bool IsOperational { get; init; }
    public DateTimeOffset? LastVerifiedAtUtc { get; init; }
    public string? RowVersion { get; init; }
    public required IReadOnlyList<string> DataQualityIssues { get; init; }
}

public sealed record WorkforceMemberDetailDto
{
    public required WorkforceMemberListItemDto Member { get; init; }
    public required IReadOnlyList<WorkforceAssignmentDto> Assignments { get; init; }
    public required IReadOnlyList<WorkforceQualificationDto> Qualifications { get; init; }
    public required IReadOnlyList<WorkforceAvailabilityDto> Availability { get; init; }
    public required IReadOnlyList<string>? RestrictionCodes { get; init; }
}

public sealed record WorkforceAssignmentDto
{
    public required Guid Id { get; init; }
    public required Guid RoleDefinitionId { get; init; }
    public required string RoleCode { get; init; }
    public required string RoleNameAr { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public required AssignmentType AssignmentType { get; init; }
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public required bool IsPrimary { get; init; }
}

public sealed record WorkforceQualificationDto
{
    public required Guid Id { get; init; }
    public required QualificationType QualificationType { get; init; }
    public Guid? RoleDefinitionId { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public required QualificationStatus Status { get; init; }
}

public sealed record WorkforceAvailabilityDto
{
    public required Guid Id { get; init; }
    public required AvailabilityType AvailabilityType { get; init; }
    public required DateTimeOffset StartsAtUtc { get; init; }
    public DateTimeOffset? EndsAtUtc { get; init; }
    public required bool AffectsOperationalAvailability { get; init; }
    public IReadOnlyList<string>? RestrictionCodes { get; init; }
}

public sealed record WorkforceDataQualityDto
{
    public required int TotalMembers { get; init; }
    public required int MissingEmployeeNumber { get; init; }
    public required int UnknownEmploymentStatus { get; init; }
    public required int MissingHomeOrOperationalFacility { get; init; }
    public required int StaleVerification { get; init; }
    public required int OpenImportIssues { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public IReadOnlyList<WorkforceDataQualityIssueDto> Issues { get; init; } = Array.Empty<WorkforceDataQualityIssueDto>();
}

public sealed record WorkforceDataQualityIssueDto
{
    public required string Code { get; init; }
    public required string TitleAr { get; init; }
    public required int Count { get; init; }
    public required string Severity { get; init; }
    public required string ImpactAr { get; init; }
    public required string SuggestedActionAr { get; init; }
    public string? UnitNameAr { get; init; }
    public string? OwnerAr { get; init; }
    public string? DrillDownHint { get; init; }
}

public sealed record WorkforceWorkspacePayload
{
    public required WorkforceSummaryDto Summary { get; init; }
    public required IReadOnlyList<WorkforceCoverageRowDto> Coverage { get; init; }
    public required IReadOnlyList<WorkforceUnitCoverageDto> Units { get; init; }
    public required IReadOnlyList<WorkforceRoleDefinitionDto> Roles { get; init; }
    public required WorkforceDataQualityDto DataQuality { get; init; }
}

public sealed record WorkforceMemberCreateRequest
{
    public required string DisplayName { get; init; }
    public required string EmployeeNumber { get; init; }
    public string? ExternalPersonnelId { get; init; }
    public EmploymentStatus EmploymentStatus { get; init; } = EmploymentStatus.Active;
    public string? RankOrGrade { get; init; }
    public required string JobTitle { get; init; }
    public required string PrimarySpecialty { get; init; }
    public Guid? HomeFacilityId { get; init; }
    public Guid? CurrentOperationalUnitId { get; init; }
    public Guid? SupervisorWorkforceMemberId { get; init; }
    public bool IsOperational { get; init; } = true;
    public bool IsSensitiveRole { get; init; }
    public WorkforceSourceType SourceType { get; init; } = WorkforceSourceType.Manual;
    public string? SourceReference { get; init; }
}

public sealed record WorkforceMemberUpdateRequest
{
    public required string DisplayName { get; init; }
    public EmploymentStatus EmploymentStatus { get; init; }
    public string? RankOrGrade { get; init; }
    public required string JobTitle { get; init; }
    public required string PrimarySpecialty { get; init; }
    public Guid? CurrentOperationalUnitId { get; init; }
    public Guid? SupervisorWorkforceMemberId { get; init; }
    public bool IsOperational { get; init; }
    public bool IsSensitiveRole { get; init; }
    /// <summary>Optional concurrency token; when set, mismatched values yield conflict.</summary>
    public byte[]? RowVersion { get; init; }
}

public sealed record WorkforceAssignmentRequest
{
    public required Guid WorkforceMemberId { get; init; }
    public required Guid RoleDefinitionId { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public AssignmentType AssignmentType { get; init; } = AssignmentType.Permanent;
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public bool IsPrimary { get; init; } = true;
    public string? SourceReference { get; init; }
    public string? Reason { get; init; }
}

public sealed record WorkforceQualificationRequest
{
    public required Guid WorkforceMemberId { get; init; }
    public required QualificationType QualificationType { get; init; }
    public Guid? RoleDefinitionId { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset? IssuedAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public string? Issuer { get; init; }
    public string? Reference { get; init; }
    public QualificationStatus Status { get; init; } = QualificationStatus.Valid;
}

public sealed record StaffingRequirementRequest
{
    public Guid? FacilityUnitId { get; init; }
    public required Guid RoleDefinitionId { get; init; }
    public Guid? ShiftDefinitionId { get; init; }
    public required int RequiredHeadcount { get; init; }
    public required int MinimumSafeHeadcount { get; init; }
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public required string SourceReference { get; init; }
    public string? ApprovalReference { get; init; }
    public string? Notes { get; init; }
}

public sealed record StaffingRequirementDto
{
    public required Guid Id { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public required Guid RoleDefinitionId { get; init; }
    public string? RoleCode { get; init; }
    public Guid? ShiftDefinitionId { get; init; }
    public required int RequiredHeadcount { get; init; }
    public required int MinimumSafeHeadcount { get; init; }
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public required string SourceReference { get; init; }
}

public sealed record DutyRosterCreateRequest
{
    public Guid? FacilityUnitId { get; init; }
    public required Guid ShiftDefinitionId { get; init; }
    public required DateOnly DutyDate { get; init; }
}

public sealed record DutyRosterAssignmentRequest
{
    public required Guid WorkforceMemberId { get; init; }
    public required Guid RoleDefinitionId { get; init; }
    public RosterAssignmentStatus Status { get; init; } = RosterAssignmentStatus.Planned;
    public Guid? ReplacementForAssignmentId { get; init; }
    public string? Notes { get; init; }
}

public sealed record DutyRosterDto
{
    public required Guid Id { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public required Guid ShiftDefinitionId { get; init; }
    public required DateOnly DutyDate { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? PublishedAtUtc { get; init; }
    public required int AssignmentCount { get; init; }
}

public sealed record WorkforceAvailabilityRequest
{
    public required Guid WorkforceMemberId { get; init; }
    public required AvailabilityType AvailabilityType { get; init; }
    public required DateTimeOffset StartsAtUtc { get; init; }
    public DateTimeOffset? EndsAtUtc { get; init; }
    public bool AffectsOperationalAvailability { get; init; } = true;
    public WorkforceSourceType SourceType { get; init; } = WorkforceSourceType.Manual;
    public string? SourceReference { get; init; }
    public string? ReasonCode { get; init; }
    public IReadOnlyList<OperationalRestrictionCode>? RestrictionCodes { get; init; }
}

public sealed record WorkforceImportPreviewRequest
{
    public WorkforceImportKind ImportKind { get; init; } = WorkforceImportKind.PersonnelMaster;
    public required string SourceSystem { get; init; }
    public required string SourceReference { get; init; }
    public required string FileHash { get; init; }
    public required IReadOnlyList<WorkforceImportRow> Rows { get; init; }
}

public sealed record WorkforceImportRow
{
    public string? EmployeeNumber { get; init; }
    public string? DisplayName { get; init; }
    public string? ExternalPersonnelId { get; init; }
    public EmploymentStatus EmploymentStatus { get; init; } = EmploymentStatus.Active;
    public string? JobTitle { get; init; }
    public string? PrimarySpecialty { get; init; }
    public Guid? CurrentOperationalUnitId { get; init; }
    public bool IsOperational { get; init; } = true;
    public Guid? RoleDefinitionId { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public Guid? ShiftDefinitionId { get; init; }
    public DateOnly? DutyDate { get; init; }
    public AssignmentType AssignmentType { get; init; } = AssignmentType.Permanent;
    public DateTimeOffset? EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public QualificationType QualificationType { get; init; } = QualificationType.RoleCertification;
    public string? QualificationName { get; init; }
    public DateTimeOffset? QualificationExpiresAtUtc { get; init; }
    public AvailabilityType AvailabilityType { get; init; } = AvailabilityType.Available;
    public DateTimeOffset? AvailabilityStartsAtUtc { get; init; }
    public DateTimeOffset? AvailabilityEndsAtUtc { get; init; }
    public int? AttendancePresentCount { get; init; }
    public int? AttendanceAbsentCount { get; init; }
}

public sealed record WorkforceImportResult(
    int TotalRows,
    int ValidRows,
    int RejectedRows,
    int DuplicateRows,
    int AppliedRows,
    IReadOnlyList<string> Errors);

public sealed record WorkforceReconciliationItemDto
{
    public required string Id { get; init; }
    public required string IssueType { get; init; }
    public required string Severity { get; init; }
    public required string TitleAr { get; init; }
    public required string DetailAr { get; init; }
    public required string EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? SourceSystem { get; init; }
    public required string SuggestedActionAr { get; init; }
    public required string ResponsibleHintAr { get; init; }
    public required DateTimeOffset DetectedAtUtc { get; init; }
}

public sealed record WorkforceReconciliationListDto
{
    public required IReadOnlyList<WorkforceReconciliationItemDto> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

public sealed record WorkforceReconciliationResolveRequest
{
    public required string ResolutionAction { get; init; }
    public string? Notes { get; init; }
}

public sealed record WorkforceCriticalPositionDto
{
    public required Guid Id { get; init; }
    public required Guid RoleDefinitionId { get; init; }
    public required string RoleCode { get; init; }
    public required string RoleNameAr { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public Guid? ShiftDefinitionId { get; init; }
    public required int RequiredPrimaryCount { get; init; }
    public required int RequiredAlternateCount { get; init; }
    public required int PrimaryFilled { get; init; }
    public required int AlternateFilled { get; init; }
    public required int VacantPrimary { get; init; }
    public required int VacantAlternate { get; init; }
    public required int ActingCount { get; init; }
    public required bool SinglePointOfFailure { get; init; }
    public required WorkforceRoleCriticality Criticality { get; init; }
    public required string StatusAr { get; init; }
}

public sealed record WorkforceReconciliationResult(int OpenIssues, bool MarkedReconciled);

public static class WorkforceExportOptions
{
    public const int DefaultLimit = 500;
    public const int MaxLimit = 1000;
}

public sealed class WorkforceValidationException(string message) : Exception(message);

