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
    public required string SourceSystem { get; init; }
    public required string SourceReference { get; init; }
    public required string FileHash { get; init; }
    public required IReadOnlyList<WorkforceImportRow> Rows { get; init; }
}

public sealed record WorkforceImportRow
{
    public required string EmployeeNumber { get; init; }
    public required string DisplayName { get; init; }
    public string? ExternalPersonnelId { get; init; }
    public EmploymentStatus EmploymentStatus { get; init; } = EmploymentStatus.Active;
    public required string JobTitle { get; init; }
    public required string PrimarySpecialty { get; init; }
    public Guid? CurrentOperationalUnitId { get; init; }
    public bool IsOperational { get; init; } = true;
}

public sealed record WorkforceImportResult(
    int TotalRows,
    int ValidRows,
    int RejectedRows,
    int DuplicateRows,
    int AppliedRows,
    IReadOnlyList<string> Errors);

public sealed record WorkforceReconciliationResult(int OpenIssues, bool MarkedReconciled);
