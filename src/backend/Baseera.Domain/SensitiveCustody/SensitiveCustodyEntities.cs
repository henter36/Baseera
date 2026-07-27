namespace Baseera.Domain.SensitiveCustody;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Domain.Workforce;

public enum WeaponStatus
{
    InArmory = 0,
    IssuedToMember = 1,
    IssuedToUnit = 2,
    InTransit = 3,
    UnderInspection = 4,
    UnderMaintenance = 5,
    AwaitingParts = 6,
    OutOfService = 7,
    Quarantined = 8,
    Missing = 9,
    UnderInvestigation = 10,
    Retired = 11,
    Destroyed = 12,
    Unknown = 13
}

public enum WeaponCondition
{
    Serviceable = 0,
    ServiceableWithRestrictions = 1,
    RequiresInspection = 2,
    RequiresMaintenance = 3,
    Unserviceable = 4,
    Unknown = 5
}

public enum WeaponCriticality
{
    Low = 0,
    Medium = 1,
    High = 2,
    MissionCritical = 3
}

public enum WeaponTypeCategory
{
    Individual = 0,
    Collective = 1,
    LessLethal = 2,
    Support = 3,
    Other = 4
}

public enum CustodyLocationType
{
    Armory = 0,
    FacilityUnit = 1,
    WorkforceMember = 2,
    Maintenance = 3,
    Transit = 4,
    Unknown = 5
}

public enum SensitiveCustodySourceType
{
    Manual = 0,
    Import = 1,
    ExternalSystem = 2,
    Audit = 3,
    Reconciliation = 4,
    Other = 5
}

public enum CustodyTransactionType
{
    IssueToMember = 0,
    IssueToUnit = 1,
    ReturnToArmory = 2,
    TransferBetweenArmories = 3,
    TemporaryTransfer = 4,
    SendToMaintenance = 5,
    ReturnFromMaintenance = 6,
    Quarantine = 7,
    ReleaseFromQuarantine = 8,
    ReportMissing = 9,
    RecoverMissing = 10,
    Retire = 11,
    Destroy = 12,
    Correction = 13
}

public enum CustodyTransactionStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    HandedOver = 3,
    Received = 4,
    Completed = 5,
    Rejected = 6,
    Cancelled = 7,
    Reversed = 8
}

public enum AmmunitionTransactionType
{
    Receipt = 0,
    Issue = 1,
    Return = 2,
    Consumption = 3,
    TransferOut = 4,
    TransferIn = 5,
    Damage = 6,
    Expiry = 7,
    Quarantine = 8,
    Release = 9,
    Adjustment = 10,
    Destruction = 11
}

public enum InventoryType
{
    Scheduled = 0,
    Surprise = 1,
    ShiftHandover = 2,
    Annual = 3,
    IncidentTriggered = 4,
    TransferTriggered = 5,
    Other = 6
}

public enum InventoryStatus
{
    Draft = 0,
    InProgress = 1,
    Completed = 2,
    PendingApproval = 3,
    Approved = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum InventoryDifferenceStatus
{
    None = 0,
    Minor = 1,
    Major = 2,
    Critical = 3,
    Unknown = 4
}

public enum InventoryEntityType
{
    WeaponAsset = 0,
    AmmunitionLot = 1
}

public enum InventoryCountedStatus
{
    Found = 0,
    Missing = 1,
    Unexpected = 2,
    Damaged = 3,
    Expired = 4,
    Unverified = 5
}

public enum InventoryDiscrepancyType
{
    None = 0,
    Missing = 1,
    Unexpected = 2,
    WrongLocation = 3,
    WrongCustodian = 4,
    StatusMismatch = 5,
    SerialMismatch = 6,
    QuantityShortage = 7,
    QuantitySurplus = 8,
    Expired = 9,
    Damaged = 10,
    Unverified = 11
}

public enum WeaponInspectionType
{
    Scheduled = 0,
    PreIssue = 1,
    ReturnAcceptance = 2,
    MaintenanceAcceptance = 3,
    Incident = 4,
    Security = 5
}

public enum WeaponInspectionResult
{
    Passed = 0,
    PassedWithRestrictions = 1,
    FailedQuarantine = 2,
    FailedMaintenanceRequired = 3,
    Unverified = 4
}

public enum SensitiveCustodyImportKind
{
    WeaponMaster = 0,
    ArmoryLocations = 1,
    CurrentCustody = 2,
    AmmunitionLots = 3,
    AmmunitionBalances = 4,
    Requirements = 5
}

public static class SensitiveCustodyImportStatuses
{
    public const string Previewed = "Previewed";
    public const string Confirmed = "Confirmed";
}

public class WeaponTypeDefinition : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public WeaponTypeCategory Category { get; set; } = WeaponTypeCategory.Other;
    public string Caliber { get; set; } = string.Empty;
    public bool IsIndividualWeapon { get; set; }
    public bool RequiresQualifiedCustodian { get; set; } = true;
    public int InspectionIntervalDays { get; set; } = 30;
    public int? MaintenanceIntervalDays { get; set; }
    public WeaponCondition MinimumSafeCondition { get; set; } = WeaponCondition.Serviceable;
    public bool IsSensitive { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class ArmoryLocation : SoftDeletableEntity, IScopedEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationClassification { get; set; } = "Sensitive";
    public bool IsActive { get; set; } = true;
    public Guid? ResponsibleWorkforceMemberId { get; set; }
    public WorkforceMember? ResponsibleWorkforceMember { get; set; }
    public Guid? AlternateResponsibleWorkforceMemberId { get; set; }
    public WorkforceMember? AlternateResponsibleWorkforceMember { get; set; }
    public int? Capacity { get; set; }
    public DateTimeOffset? LastSecurityInspectionAtUtc { get; set; }
    public DateTimeOffset? NextSecurityInspectionDueAtUtc { get; set; }

    public ScopeType ScopeType => ScopeType.Facility;
    Guid? IScopedEntity.RegionId => Facility?.RegionId;
    Guid? IScopedEntity.FacilityId => FacilityId;
    Guid? IScopedEntity.FacilityUnitId => FacilityUnitId;
}

public class WeaponAsset : SoftDeletableEntity, IScopedEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid WeaponTypeId { get; set; }
    public WeaponTypeDefinition WeaponType { get; set; } = null!;
    public string InternalAssetCode { get; set; } = string.Empty;
    public string SerialNumberEncrypted { get; set; } = string.Empty;
    public string SerialNumberHash { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string Caliber { get; set; } = string.Empty;
    public string? AcquisitionReference { get; set; }
    public DateTimeOffset? CommissionedAtUtc { get; set; }
    public WeaponStatus CurrentStatus { get; set; } = WeaponStatus.Unknown;
    public WeaponCondition Condition { get; set; } = WeaponCondition.Unknown;
    public WeaponCriticality Criticality { get; set; } = WeaponCriticality.Medium;
    public CustodyLocationType CurrentCustodyLocationType { get; set; } = CustodyLocationType.Unknown;
    public Guid CurrentFacilityId { get; set; }
    public Facility CurrentFacility { get; set; } = null!;
    public Guid? CurrentFacilityUnitId { get; set; }
    public FacilityUnit? CurrentFacilityUnit { get; set; }
    public Guid? CurrentArmoryLocationId { get; set; }
    public ArmoryLocation? CurrentArmoryLocation { get; set; }
    public Guid? CurrentCustodyTransactionId { get; set; }
    public CustodyTransaction? CurrentCustodyTransaction { get; set; }
    public DateTimeOffset? LastInspectionAtUtc { get; set; }
    public DateTimeOffset? NextInspectionDueAtUtc { get; set; }
    public DateTimeOffset? LastVerifiedAtUtc { get; set; }
    public SensitiveCustodySourceType SourceType { get; set; } = SensitiveCustodySourceType.Manual;
    public string? SourceReference { get; set; }
    public ICollection<CustodyTransaction> CustodyTransactions { get; set; } = [];
    public ICollection<WeaponInspection> Inspections { get; set; } = [];

    public ScopeType ScopeType => ScopeType.Facility;
    Guid? IScopedEntity.RegionId => CurrentFacility?.RegionId;
    Guid? IScopedEntity.FacilityId => CurrentFacilityId;
    Guid? IScopedEntity.FacilityUnitId => CurrentFacilityUnitId;
}

public class CustodyTransaction : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid WeaponAssetId { get; set; }
    public WeaponAsset WeaponAsset { get; set; } = null!;
    public CustodyTransactionType TransactionType { get; set; }
    public CustodyLocationType FromCustodyType { get; set; } = CustodyLocationType.Unknown;
    public Guid? FromCustodyReferenceId { get; set; }
    public CustodyLocationType ToCustodyType { get; set; } = CustodyLocationType.Unknown;
    public Guid? ToCustodyReferenceId { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset? ExpectedReturnAtUtc { get; set; }
    public DateTimeOffset? ReturnedAtUtc { get; set; }
    public string PurposeCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public string? WitnessedBy { get; set; }
    public CustodyTransactionStatus Status { get; set; } = CustodyTransactionStatus.Draft;
    public string? SourceReference { get; set; }
    public Guid? PreviousTransactionId { get; set; }
    public CustodyTransaction? PreviousTransaction { get; set; }
    public WeaponStatus PreviousWeaponStatus { get; set; } = WeaponStatus.Unknown;
    public CustodyLocationType PreviousCustodyLocationType { get; set; } = CustodyLocationType.Unknown;
    public Guid? PreviousFacilityUnitId { get; set; }
    public Guid? PreviousArmoryLocationId { get; set; }
    public Guid? CorrectionOfTransactionId { get; set; }
    public CustodyTransaction? CorrectionOfTransaction { get; set; }
    public bool IsCurrent { get; set; }
}

public class AmmunitionType : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Caliber { get; set; } = string.Empty;
    public bool RequiresExpiry { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class AmmunitionLot : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid ArmoryLocationId { get; set; }
    public ArmoryLocation ArmoryLocation { get; set; } = null!;
    public Guid AmmunitionTypeId { get; set; }
    public AmmunitionType AmmunitionType { get; set; } = null!;
    public string? LotNumberEncrypted { get; set; }
    public string? LotNumberHash { get; set; }
    public DateTimeOffset? ManufactureDateUtc { get; set; }
    public DateTimeOffset? ExpiryDateUtc { get; set; }
    public int ReceivedQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int QuarantinedQuantity { get; set; }
    public int DamagedQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = "round";
    public string SourceReference { get; set; } = string.Empty;
    public ICollection<AmmunitionTransaction> Transactions { get; set; } = [];
}

public class AmmunitionTransaction : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid AmmunitionLotId { get; set; }
    public AmmunitionLot AmmunitionLot { get; set; } = null!;
    public AmmunitionTransactionType TransactionType { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? Reference { get; set; }
}

public class SensitiveResourceRequirement : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }
    public Guid? WeaponTypeId { get; set; }
    public WeaponTypeDefinition? WeaponType { get; set; }
    public Guid? AmmunitionTypeId { get; set; }
    public AmmunitionType? AmmunitionType { get; set; }
    public string? OperationalRole { get; set; }
    public int RequiredQuantity { get; set; }
    public int MinimumOperationalQuantity { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public string ApprovalReference { get; set; } = string.Empty;
}

public class InventorySession : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid? ArmoryLocationId { get; set; }
    public ArmoryLocation? ArmoryLocation { get; set; }
    public InventoryType InventoryType { get; set; }
    public InventoryStatus Status { get; set; } = InventoryStatus.Draft;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? WitnessedBy { get; set; }
    public int ExpectedWeaponCount { get; set; }
    public int CountedWeaponCount { get; set; }
    public int ExpectedAmmunitionQuantity { get; set; }
    public int CountedAmmunitionQuantity { get; set; }
    public InventoryDifferenceStatus DifferenceStatus { get; set; } = InventoryDifferenceStatus.Unknown;
    public string? Notes { get; set; }
    public ICollection<InventoryEntry> Entries { get; set; } = [];
}

public class InventoryEntry : SoftDeletableEntity
{
    public Guid InventorySessionId { get; set; }
    public InventorySession InventorySession { get; set; } = null!;
    public InventoryEntityType EntityType { get; set; }
    public Guid? ExpectedReferenceId { get; set; }
    public InventoryCountedStatus CountedStatus { get; set; }
    public InventoryDiscrepancyType DiscrepancyType { get; set; }
    public int? ExpectedQuantity { get; set; }
    public int? CountedQuantity { get; set; }
    public string? Notes { get; set; }
    public string VerifiedBy { get; set; } = string.Empty;
    public DateTimeOffset VerifiedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public class WeaponInspection : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid WeaponAssetId { get; set; }
    public WeaponAsset WeaponAsset { get; set; } = null!;
    public WeaponInspectionType InspectionType { get; set; }
    public WeaponInspectionResult Result { get; set; }
    public WeaponCondition Condition { get; set; }
    public string? Restrictions { get; set; }
    public Guid InspectorWorkforceMemberId { get; set; }
    public WorkforceMember InspectorWorkforceMember { get; set; } = null!;
    public DateTimeOffset InspectedAtUtc { get; set; }
    public DateTimeOffset? NextDueAtUtc { get; set; }
    public string StatusTransition { get; set; } = string.Empty;
    public string? AttachmentReference { get; set; }
}

public class SensitiveCustodyImportBatch : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public SensitiveCustodyImportKind ImportKind { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string Status { get; set; } = SensitiveCustodyImportStatuses.Previewed;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RejectedRows { get; set; }
    public int DuplicateRows { get; set; }
    public int AppliedRows { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
}

public class SensitiveCustodyReconciliationResolution : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public string ItemKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ResolvedBy { get; set; } = string.Empty;
    public DateTimeOffset ResolvedAtUtc { get; set; }
}
