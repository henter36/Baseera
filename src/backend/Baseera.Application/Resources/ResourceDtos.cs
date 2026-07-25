namespace Baseera.Application.Resources;

using Baseera.Domain.Resources;

public sealed record ResourceSummaryDto
{
    public required Guid FacilityId { get; init; }
    public required int TotalRegistered { get; init; }
    public required int Operational { get; init; }
    public required int Available { get; init; }
    public required int Standby { get; init; }
    public required int InUse { get; init; }
    public required int UnderMaintenance { get; init; }
    public required int OutOfService { get; init; }
    public required int AwaitingParts { get; init; }
    public required int Unknown { get; init; }
    public required int Retired { get; init; }
    public required int Required { get; init; }
    public required int Gap { get; init; }
    public required int Surplus { get; init; }
    public required decimal? ReadinessRate { get; init; }
    public required decimal? AvailabilityRate { get; init; }
    public required decimal DataCompletenessRate { get; init; }
    public required int MissionCriticalUnavailable { get; init; }
    public required int StaleRecords { get; init; }
    public required int MissingDataRecords { get; init; }
    public required string FreshnessStatus { get; init; }
    public required string ConfidenceLevel { get; init; }
    public required bool IsPartial { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public DateTimeOffset? DataEffectiveAtUtc { get; init; }
}

public sealed record ResourceCategoryReadinessDto
{
    public required ResourceType ResourceType { get; init; }
    public required string ResourceTypeCode { get; init; }
    public required string LabelAr { get; init; }
    public required int Total { get; init; }
    public required int Operational { get; init; }
    public required int Available { get; init; }
    public required int UnderMaintenance { get; init; }
    public required int OutOfService { get; init; }
    public required int AwaitingParts { get; init; }
    public required int Required { get; init; }
    public required int Gap { get; init; }
    public required decimal? ReadinessRate { get; init; }
    public required string FreshnessStatus { get; init; }
    public required string ConfidenceLevel { get; init; }
}

public sealed record ResourceExceptionDto
{
    public required string Type { get; init; }
    public required Guid? ResourceAssetId { get; init; }
    public required ResourceType? ResourceType { get; init; }
    public required string Reference { get; init; }
    public required string TitleAr { get; init; }
    public required string SeverityAr { get; init; }
    public required int PriorityRank { get; init; }
    public required string ReasonAr { get; init; }
    public string? OwnerAr { get; init; }
    public DateTimeOffset? DueAtUtc { get; init; }
    public required string ActionLabelAr { get; init; }
}

public sealed record ResourceUnitDistributionDto
{
    public required Guid? FacilityUnitId { get; init; }
    public required string UnitNameAr { get; init; }
    public required int Vehicles { get; init; }
    public required int CommunicationDevices { get; init; }
    public required int Equipment { get; init; }
    public required int FacilityAssets { get; init; }
    public required decimal? ReadinessRate { get; init; }
    public required int Gap { get; init; }
    public required int CriticalExceptions { get; init; }
}

public sealed record ResourceActivityDto
{
    public required string EventType { get; init; }
    public required string TitleAr { get; init; }
    public string? DescriptionAr { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string EntityReference { get; init; }
    public required string Tone { get; init; }
    public Guid? ResourceAssetId { get; init; }
}

public sealed record ResourceWorkspacePayload
{
    public required ResourceSummaryDto Summary { get; init; }
    public required IReadOnlyList<ResourceCategoryReadinessDto> Categories { get; init; }
    public required IReadOnlyList<ResourceExceptionDto> Exceptions { get; init; }
    public required IReadOnlyList<ResourceUnitDistributionDto> UnitDistribution { get; init; }
    public required IReadOnlyList<ResourceActivityDto> Timeline { get; init; }
}

public sealed record ResourceAssetListItemDto
{
    public required Guid Id { get; init; }
    public required ResourceType ResourceType { get; init; }
    public required string AssetCode { get; init; }
    public required string DisplayName { get; init; }
    public string? SerialNumber { get; init; }
    public string? PlateNumber { get; init; }
    public required ResourceStatus CurrentStatus { get; init; }
    public required ResourceCondition Condition { get; init; }
    public required ResourceCriticality Criticality { get; init; }
    public string? OperationalFacilityUnitNameAr { get; init; }
    public string? CustodianNameAr { get; init; }
    public DateTimeOffset? LastVerifiedAtUtc { get; init; }
    public required bool HasOpenMaintenance { get; init; }
    public required IReadOnlyList<string> DataQualityIssues { get; init; }
}

public sealed record ResourceAssetDetailDto
{
    public required ResourceAssetListItemDto Asset { get; init; }
    public required IReadOnlyList<ResourceMaintenanceDto> Maintenance { get; init; }
    public required IReadOnlyList<ResourceActivityDto> Timeline { get; init; }
    public required IReadOnlyList<ResourceAllowedActionDto> AllowedActions { get; init; }
}

public sealed record ResourceMaintenanceDto
{
    public required Guid Id { get; init; }
    public required string WorkOrderNumber { get; init; }
    public required MaintenanceType MaintenanceType { get; init; }
    public required MaintenancePriority Priority { get; init; }
    public required MaintenanceStatus Status { get; init; }
    public required DateTimeOffset ReportedAtUtc { get; init; }
    public DateTimeOffset? ExpectedCompletionAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public required bool IsOverdue { get; init; }
    public required string ProblemDescription { get; init; }
}

public sealed record ResourceAllowedActionDto(string Code, string LabelAr, bool Enabled, string? DisabledReasonAr);

public sealed record ResourceAssetCreateRequest
{
    public required ResourceType ResourceType { get; init; }
    public required string AssetCode { get; init; }
    public required string DisplayName { get; init; }
    public string? SerialNumber { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public int? ManufactureYear { get; init; }
    public required Guid OwnershipOrganizationId { get; init; }
    public Guid? OperationalFacilityUnitId { get; init; }
    public Guid? CustodianUserId { get; init; }
    public ResourceStatus CurrentStatus { get; init; } = ResourceStatus.Unknown;
    public ResourceCondition Condition { get; init; } = ResourceCondition.Unknown;
    public ResourceCriticality Criticality { get; init; } = ResourceCriticality.Medium;
    public ResourceSourceType SourceType { get; init; } = ResourceSourceType.Manual;
    public string? SourceReference { get; init; }
}

public sealed record ResourceStatusChangeRequest
{
    public required ResourceStatus NewStatus { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string Reason { get; init; }
    public string? ReasonCode { get; init; }
    public ResourceSourceType SourceType { get; init; } = ResourceSourceType.Manual;
    public string? SourceReference { get; init; }
}

public sealed record ResourcePlacementRequest
{
    public required Guid OwnershipOrganizationId { get; init; }
    public Guid? OperationalFacilityUnitId { get; init; }
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public ResourceAssignmentType AssignmentType { get; init; } = ResourceAssignmentType.Permanent;
    public string? SourceReference { get; init; }
    public string? Reason { get; init; }
}

public sealed record MaintenanceWorkOrderRequest
{
    public required Guid ResourceAssetId { get; init; }
    public required MaintenanceType MaintenanceType { get; init; }
    public MaintenancePriority Priority { get; init; } = MaintenancePriority.Medium;
    public required DateTimeOffset ReportedAtUtc { get; init; }
    public required string ProblemDescription { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public DateTimeOffset? ExpectedCompletionAtUtc { get; init; }
    public bool PartsRequired { get; init; }
}

public sealed record ResourceRequirementRequest
{
    public Guid? FacilityUnitId { get; init; }
    public required ResourceType ResourceType { get; init; }
    public required string ResourceCategory { get; init; }
    public required int RequiredQuantity { get; init; }
    public required int MinimumOperationalQuantity { get; init; }
    public required DateTimeOffset EffectiveFromUtc { get; init; }
    public DateTimeOffset? EffectiveToUtc { get; init; }
    public required string SourceReference { get; init; }
    public string? ApprovalReference { get; init; }
    public string? Notes { get; init; }
}

public sealed record ResourceImportPreviewRequest
{
    public required string SourceSystem { get; init; }
    public required string SourceReference { get; init; }
    public required string FileHash { get; init; }
    public required IReadOnlyList<ResourceImportRow> Rows { get; init; }
}

public sealed record ResourceImportRow
{
    public required ResourceType ResourceType { get; init; }
    public required string AssetCode { get; init; }
    public required string DisplayName { get; init; }
    public string? SerialNumber { get; init; }
    public ResourceStatus CurrentStatus { get; init; } = ResourceStatus.Unknown;
    public ResourceCondition Condition { get; init; } = ResourceCondition.Unknown;
    public ResourceCriticality Criticality { get; init; } = ResourceCriticality.Medium;
}

public sealed record ResourceImportResult(int TotalRows, int ValidRows, int RejectedRows, int DuplicateRows, int AppliedRows, IReadOnlyList<string> Errors);
