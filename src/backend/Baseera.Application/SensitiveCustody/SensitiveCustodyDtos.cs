namespace Baseera.Application.SensitiveCustody;

using Baseera.Domain.SensitiveCustody;

public sealed record SensitiveCustodyWorkspacePayload
{
    public required SensitiveCustodySummaryDto Summary { get; init; }
    public required IReadOnlyList<SensitiveCustodyInterventionDto> Interventions { get; init; }
    public required IReadOnlyList<SensitiveCustodyDataQualityIssueDto> DataQuality { get; init; }
    public required IReadOnlyList<SensitiveCustodyTimelineItemDto> Timeline { get; init; }
    public required IReadOnlyList<SensitiveCustodyAllowedActionDto> AllowedActions { get; init; }
}

public sealed record SensitiveCustodySummaryDto
{
    public required Guid FacilityId { get; init; }
    public required int TotalWeapons { get; init; }
    public required int OperationallyReady { get; init; }
    public required int Issued { get; init; }
    public required int InArmory { get; init; }
    public required int WithUnits { get; init; }
    public required int WithMembers { get; init; }
    public required int UnderMaintenance { get; init; }
    public required int OutOfService { get; init; }
    public required int MissingOrDiscrepant { get; init; }
    public required int OverdueReturns { get; init; }
    public required int DueInspections { get; init; }
    public required int OpenDiscrepancies { get; init; }
    public required int PendingApprovals { get; init; }
    public required int AvailableAmmunition { get; init; }
    public required int MinimumAmmunition { get; init; }
    public required int AmmunitionGap { get; init; }
    public required int StaleRecords { get; init; }
    public required decimal? ReadinessRate { get; init; }
    public required decimal? VerificationCoverage { get; init; }
    public required string FreshnessStatus { get; init; }
    public required string ConfidenceLevel { get; init; }
    public DateTimeOffset? LastInventoryAtUtc { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
}

public sealed record WeaponAssetListItemDto
{
    public required Guid Id { get; init; }
    public required string InternalAssetCode { get; init; }
    public required string MaskedSerial { get; init; }
    public string? FullSerial { get; init; }
    public required string TypeNameAr { get; init; }
    public required string Caliber { get; init; }
    public required WeaponStatus CurrentStatus { get; init; }
    public required WeaponCondition Condition { get; init; }
    public required WeaponCriticality Criticality { get; init; }
    public required CustodyLocationType CustodyType { get; init; }
    public string? FacilityUnitNameAr { get; init; }
    public string? ArmoryLocationName { get; init; }
    public DateTimeOffset? LastInspectionAtUtc { get; init; }
    public DateTimeOffset? NextInspectionDueAtUtc { get; init; }
    public DateTimeOffset? LastVerifiedAtUtc { get; init; }
}

public sealed record WeaponAssetDetailDto
{
    public required WeaponAssetListItemDto Weapon { get; init; }
    public required IReadOnlyList<CustodyTransactionDto> RecentTransactions { get; init; }
    public required IReadOnlyList<WeaponInspectionDto> Inspections { get; init; }
    public required IReadOnlyList<SensitiveCustodyAllowedActionDto> AllowedActions { get; init; }
}

public sealed record CustodyTransactionDto
{
    public required Guid Id { get; init; }
    public required Guid WeaponAssetId { get; init; }
    public required CustodyTransactionType TransactionType { get; init; }
    public required CustodyTransactionStatus Status { get; init; }
    public required DateTimeOffset IssuedAtUtc { get; init; }
    public DateTimeOffset? ExpectedReturnAtUtc { get; init; }
    public DateTimeOffset? ReturnedAtUtc { get; init; }
    public required string PurposeCode { get; init; }
    public required string Reason { get; init; }
    public required string CreatedBy { get; init; }
    public string? ApprovedBy { get; init; }
    public string? ReceivedBy { get; init; }
    public required string RowVersion { get; init; }
}

public sealed record AmmunitionLotDto
{
    public required Guid Id { get; init; }
    public required string TypeNameAr { get; init; }
    public required string Caliber { get; init; }
    public required string MaskedLotNumber { get; init; }
    public DateTimeOffset? ExpiryDateUtc { get; init; }
    public required int CurrentQuantity { get; init; }
    public required int ReservedQuantity { get; init; }
    public required int QuarantinedQuantity { get; init; }
    public required int DamagedQuantity { get; init; }
    public required int AvailableQuantity { get; init; }
    public required string UnitOfMeasure { get; init; }
}

public sealed record AmmunitionTransactionDto
{
    public required Guid Id { get; init; }
    public required Guid AmmunitionLotId { get; init; }
    public required AmmunitionTransactionType TransactionType { get; init; }
    public required int Quantity { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string Reason { get; init; }
    public required string CreatedBy { get; init; }
}

public sealed record InventorySessionDto
{
    public required Guid Id { get; init; }
    public required InventoryType InventoryType { get; init; }
    public required InventoryStatus Status { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public required int ExpectedWeaponCount { get; init; }
    public required int CountedWeaponCount { get; init; }
    public required int ExpectedAmmunitionQuantity { get; init; }
    public required int CountedAmmunitionQuantity { get; init; }
    public required InventoryDifferenceStatus DifferenceStatus { get; init; }
    public required string RowVersion { get; init; }
}

public sealed record InventoryEntryDto(
    Guid Id,
    InventoryEntityType EntityType,
    Guid? ExpectedReferenceId,
    InventoryCountedStatus CountedStatus,
    InventoryDiscrepancyType DiscrepancyType,
    int? ExpectedQuantity,
    int? CountedQuantity,
    string? Notes);

public sealed record WeaponInspectionDto
{
    public required Guid Id { get; init; }
    public required Guid WeaponAssetId { get; init; }
    public required WeaponInspectionType InspectionType { get; init; }
    public required WeaponInspectionResult Result { get; init; }
    public required WeaponCondition Condition { get; init; }
    public string? Restrictions { get; init; }
    public required DateTimeOffset InspectedAtUtc { get; init; }
    public DateTimeOffset? NextDueAtUtc { get; init; }
}

public sealed record SensitiveCustodyInterventionDto
{
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required string ReasonAr { get; init; }
    public required string SourceEntityType { get; init; }
    public required Guid? SourceEntityId { get; init; }
    public Guid? FacilityUnitId { get; init; }
    public string? OwnerRole { get; init; }
    public DateTimeOffset? DueAtUtc { get; init; }
    public required string PrimaryAction { get; init; }
    public required string DrillDown { get; init; }
}

public sealed record SensitiveCustodyDataQualityIssueDto
{
    public required string Code { get; init; }
    public required int Count { get; init; }
    public required string Severity { get; init; }
    public required string ImpactAr { get; init; }
    public required string Source { get; init; }
    public string? OwnerRole { get; init; }
    public required string CorrectiveActionAr { get; init; }
    public required string DrillDown { get; init; }
}

public sealed record SensitiveCustodyTimelineItemDto
{
    public required string EventType { get; init; }
    public required string TitleAr { get; init; }
    public string? DescriptionAr { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string EntityReference { get; init; }
    public required string Tone { get; init; }
}

public sealed record SensitiveCustodyAllowedActionDto(string Code, string LabelAr, bool Enabled, string? DisabledReasonAr);

public sealed record WeaponAssetCreateRequest
{
    public required Guid WeaponTypeId { get; init; }
    public required string InternalAssetCode { get; init; }
    public required string SerialNumber { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public required string Caliber { get; init; }
    public Guid? CurrentFacilityUnitId { get; init; }
    public Guid? CurrentArmoryLocationId { get; init; }
    public WeaponStatus CurrentStatus { get; init; } = WeaponStatus.InArmory;
    public WeaponCondition Condition { get; init; } = WeaponCondition.Serviceable;
    public WeaponCriticality Criticality { get; init; } = WeaponCriticality.Medium;
    public string? SourceReference { get; init; }
}

public sealed record WeaponAssetUpdateRequest
{
    public required string RowVersion { get; init; }
    public WeaponCondition? Condition { get; init; }
    public DateTimeOffset? LastVerifiedAtUtc { get; init; }
    public string? SourceReference { get; init; }
}

public sealed record CustodyTransactionCreateRequest
{
    public required Guid WeaponAssetId { get; init; }
    public required CustodyTransactionType TransactionType { get; init; }
    public CustodyLocationType ToCustodyType { get; init; } = CustodyLocationType.Unknown;
    public Guid? ToCustodyReferenceId { get; init; }
    public DateTimeOffset? ExpectedReturnAtUtc { get; init; }
    public required string PurposeCode { get; init; }
    public required string Reason { get; init; }
}

public sealed record SensitiveCustodyTransitionRequest
{
    public required string RowVersion { get; init; }
    public required string Reason { get; init; }
}

public sealed record AmmunitionTransactionRequest
{
    public required Guid AmmunitionLotId { get; init; }
    public required AmmunitionTransactionType TransactionType { get; init; }
    public required int Quantity { get; init; }
    public required string Reason { get; init; }
    public string? Reference { get; init; }
}

public sealed record InventorySessionCreateRequest
{
    public Guid? ArmoryLocationId { get; init; }
    public InventoryType InventoryType { get; init; } = InventoryType.Scheduled;
    public string? Notes { get; init; }
}

public sealed record InventoryEntryRequest
{
    public required InventoryEntityType EntityType { get; init; }
    public Guid? ExpectedReferenceId { get; init; }
    public required InventoryCountedStatus CountedStatus { get; init; }
    public required InventoryDiscrepancyType DiscrepancyType { get; init; }
    public int? ExpectedQuantity { get; init; }
    public int? CountedQuantity { get; init; }
    public string? Notes { get; init; }
}

public sealed record WeaponInspectionRequest
{
    public required Guid WeaponAssetId { get; init; }
    public required Guid InspectorWorkforceMemberId { get; init; }
    public WeaponInspectionType InspectionType { get; init; } = WeaponInspectionType.Scheduled;
    public required WeaponInspectionResult Result { get; init; }
    public required WeaponCondition Condition { get; init; }
    public string? Restrictions { get; init; }
    public DateTimeOffset? NextDueAtUtc { get; init; }
}

public sealed record SensitiveCustodyImportPreviewRequest
{
    public required SensitiveCustodyImportKind ImportKind { get; init; }
    public required string SourceSystem { get; init; }
    public required string SourceReference { get; init; }
    public required string FileHash { get; init; }
    public required IReadOnlyList<SensitiveCustodyImportRow> Rows { get; init; }
}

public sealed record SensitiveCustodyImportRow
{
    public string? AssetCode { get; init; }
    public string? SerialNumber { get; init; }
    public string? TypeCode { get; init; }
    public string? Quantity { get; init; }
}

public sealed record SensitiveCustodyImportResult(
    int TotalRows,
    int ValidRows,
    int RejectedRows,
    int DuplicateRows,
    int AppliedRows,
    IReadOnlyList<string> Errors);
