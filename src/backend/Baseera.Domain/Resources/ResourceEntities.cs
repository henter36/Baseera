namespace Baseera.Domain.Resources;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum ResourceType
{
    Vehicle = 0,
    CommunicationDevice = 1,
    OperationalEquipment = 2,
    SecurityEquipment = 3,
    FacilityAsset = 4
}

public enum ResourceStatus
{
    Available = 0,
    InUse = 1,
    Standby = 2,
    Reserved = 3,
    UnderInspection = 4,
    UnderMaintenance = 5,
    OutOfService = 6,
    AwaitingParts = 7,
    Lost = 8,
    Transferred = 9,
    Retired = 10,
    Unknown = 11
}

public enum ResourceCondition
{
    Excellent = 0,
    Good = 1,
    Fair = 2,
    Poor = 3,
    Unserviceable = 4,
    Unknown = 5
}

public enum ResourceCriticality
{
    Low = 0,
    Medium = 1,
    High = 2,
    MissionCritical = 3
}

public enum ResourceSourceType
{
    Manual = 0,
    Import = 1,
    ExternalSystem = 2,
    Audit = 3,
    Other = 4
}

public enum ResourceAssignmentType
{
    Permanent = 0,
    Temporary = 1,
    Loan = 2,
    EmergencyDeployment = 3,
    MaintenanceTransfer = 4,
    Storage = 5,
    Other = 6
}

public enum MaintenanceType
{
    Preventive = 0,
    Corrective = 1,
    Inspection = 2,
    Calibration = 3,
    Emergency = 4,
    Warranty = 5,
    Recall = 6,
    Other = 7
}

public enum MaintenanceStatus
{
    Open = 0,
    Assigned = 1,
    InProgress = 2,
    AwaitingParts = 3,
    AwaitingVendor = 4,
    Completed = 5,
    Cancelled = 6,
    Rejected = 7
}

public enum MaintenancePriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum VehicleCategory
{
    Patrol = 0,
    PrisonerTransport = 1,
    Ambulance = 2,
    Logistics = 3,
    Utility = 4,
    Other = 5
}

public enum FuelType
{
    Gasoline = 0,
    Diesel = 1,
    Hybrid = 2,
    Electric = 3,
    Other = 4
}

public enum CommunicationDeviceCategory
{
    HandheldRadio = 0,
    VehicleRadio = 1,
    BaseStation = 2,
    MobilePhone = 3,
    SatellitePhone = 4,
    Other = 5
}

public enum EquipmentCategory
{
    Screening = 0,
    Surveillance = 1,
    Safety = 2,
    MedicalSupport = 3,
    Kitchen = 4,
    Workshop = 5,
    Other = 6
}

public enum FacilityAssetCategory
{
    Gate = 0,
    Door = 1,
    Camera = 2,
    Generator = 3,
    Pump = 4,
    FireSystem = 5,
    CellInfrastructure = 6,
    Other = 7
}

public class ResourceAsset : SoftDeletableEntity, IScopedEntity
{
    private Organization? organization;
    private Organization? ownershipOrganization;

    public Guid OrganizationId { get; set; }
    public Organization Organization
    {
        get => organization ?? throw new InvalidOperationException("Organization navigation has not been loaded.");
        set => organization = value;
    }

    public ResourceType ResourceType { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public int? ManufactureYear { get; set; }
    public DateTimeOffset? AcquisitionDateUtc { get; set; }
    public DateTimeOffset? CommissionedAtUtc { get; set; }
    public DateTimeOffset? ExpectedEndOfLifeUtc { get; set; }
    public Guid OwnershipOrganizationId { get; set; }
    public Organization OwnershipOrganization
    {
        get => ownershipOrganization ?? throw new InvalidOperationException("Ownership organization navigation has not been loaded.");
        set => ownershipOrganization = value;
    }

    public Guid? OperationalFacilityId { get; set; }
    public Facility? OperationalFacility { get; set; }

    public Guid? OperationalFacilityUnitId { get; set; }
    public FacilityUnit? OperationalFacilityUnit { get; set; }

    public Guid? CustodianUserId { get; set; }
    public User? CustodianUser { get; set; }

    public ResourceStatus CurrentStatus { get; set; } = ResourceStatus.Unknown;
    public ResourceCondition Condition { get; set; } = ResourceCondition.Unknown;
    public ResourceCriticality Criticality { get; set; } = ResourceCriticality.Medium;
    public ResourceSourceType SourceType { get; set; } = ResourceSourceType.Manual;
    public string? SourceReference { get; set; }
    public DateTimeOffset? LastVerifiedAtUtc { get; set; }
    public string? LastVerifiedBy { get; set; }

    public VehicleProfile? VehicleProfile { get; set; }
    public CommunicationDeviceProfile? CommunicationDeviceProfile { get; set; }
    public EquipmentProfile? EquipmentProfile { get; set; }
    public FacilityAssetProfile? FacilityAssetProfile { get; set; }
    public ICollection<ResourceStatusEvent> StatusEvents { get; set; } = new List<ResourceStatusEvent>();
    public ICollection<ResourcePlacement> Placements { get; set; } = new List<ResourcePlacement>();
    public ICollection<MaintenanceWorkOrder> MaintenanceWorkOrders { get; set; } = new List<MaintenanceWorkOrder>();

    public ScopeType ScopeType
    {
        get
        {
            if (OperationalFacilityUnitId.HasValue)
            {
                return ScopeType.FacilityUnit;
            }

            if (OperationalFacilityId.HasValue)
            {
                return ScopeType.Facility;
            }

            return ScopeType.Headquarters;
        }
    }
    Guid? IScopedEntity.RegionId => OperationalFacility?.RegionId;
    Guid? IScopedEntity.FacilityId => OperationalFacilityId;
    Guid? IScopedEntity.FacilityUnitId => OperationalFacilityUnitId;
}

public class VehicleProfile
{
    private ResourceAsset? resourceAsset;

    public Guid ResourceAssetId { get; set; }
    public ResourceAsset ResourceAsset
    {
        get => resourceAsset ?? throw new InvalidOperationException("Resource asset navigation has not been loaded.");
        set => resourceAsset = value;
    }

    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleIdentificationNumber { get; set; }
    public VehicleCategory VehicleCategory { get; set; }
    public FuelType? FuelType { get; set; }
    public decimal? Odometer { get; set; }
    public DateTimeOffset? OdometerRecordedAtUtc { get; set; }
    public DateTimeOffset? RegistrationExpiresAtUtc { get; set; }
    public DateTimeOffset? InsuranceExpiresAtUtc { get; set; }
    public DateTimeOffset? InspectionExpiresAtUtc { get; set; }
    public bool TrackerInstalled { get; set; }
    public string? TrackerExternalId { get; set; }
    public string OperationalRole { get; set; } = string.Empty;
    public int? PassengerCapacity { get; set; }
    public int? PrisonerTransportCapacity { get; set; }
}

public class CommunicationDeviceProfile
{
    private ResourceAsset? resourceAsset;

    public Guid ResourceAssetId { get; set; }
    public ResourceAsset ResourceAsset
    {
        get => resourceAsset ?? throw new InvalidOperationException("Resource asset navigation has not been loaded.");
        set => resourceAsset = value;
    }

    public CommunicationDeviceCategory DeviceCategory { get; set; }
    public string? NetworkType { get; set; }
    public string? CallSign { get; set; }
    public string? SimOrLineReference { get; set; }
    public string? FrequencyGroup { get; set; }
    public string? BatteryCondition { get; set; }
    public string? CoverageStatus { get; set; }
    public bool? EncryptionCapability { get; set; }
    public Guid? AssignedUnitId { get; set; }
    public FacilityUnit? AssignedUnit { get; set; }
}

public class EquipmentProfile
{
    private ResourceAsset? resourceAsset;

    public Guid ResourceAssetId { get; set; }
    public ResourceAsset ResourceAsset
    {
        get => resourceAsset ?? throw new InvalidOperationException("Resource asset navigation has not been loaded.");
        set => resourceAsset = value;
    }

    public EquipmentCategory EquipmentCategory { get; set; }
    public string? Specification { get; set; }
    public string? QuantityUnit { get; set; }
    public bool CalibrationRequired { get; set; }
    public DateTimeOffset? CalibrationDueAtUtc { get; set; }
    public bool InspectionRequired { get; set; }
    public DateTimeOffset? InspectionDueAtUtc { get; set; }
    public bool Portable { get; set; }
    public bool SafetyCritical { get; set; }
}

public class FacilityAssetProfile
{
    private ResourceAsset? resourceAsset;

    public Guid ResourceAssetId { get; set; }
    public ResourceAsset ResourceAsset
    {
        get => resourceAsset ?? throw new InvalidOperationException("Resource asset navigation has not been loaded.");
        set => resourceAsset = value;
    }

    public FacilityAssetCategory AssetCategory { get; set; }
    public Guid? BuildingId { get; set; }
    public Building? Building { get; set; }

    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }

    public string? InstalledAtLocation { get; set; }
    public bool FixedAsset { get; set; } = true;
    public decimal? CapacityValue { get; set; }
    public string? CapacityUnit { get; set; }
    public bool RequiresPeriodicInspection { get; set; }
    public DateTimeOffset? InspectionDueAtUtc { get; set; }
}

public class ResourceStatusEvent : EntityBase
{
    private ResourceAsset? resourceAsset;

    public Guid ResourceAssetId { get; set; }
    public ResourceAsset ResourceAsset
    {
        get => resourceAsset ?? throw new InvalidOperationException("Resource asset navigation has not been loaded.");
        set => resourceAsset = value;
    }

    public ResourceStatus? PreviousStatus { get; set; }
    public ResourceStatus NewStatus { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? ReasonCode { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ResourceSourceType SourceType { get; set; } = ResourceSourceType.Manual;
    public string? SourceReference { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? RelatedMaintenanceWorkOrderId { get; set; }
    public Guid? RelatedNoteId { get; set; }
}

public class ResourcePlacement : EntityBase
{
    private ResourceAsset? resourceAsset;
    private Organization? ownershipOrganization;
    private Facility? operationalFacility;

    public Guid ResourceAssetId { get; set; }
    public ResourceAsset ResourceAsset
    {
        get => resourceAsset ?? throw new InvalidOperationException("Resource asset navigation has not been loaded.");
        set => resourceAsset = value;
    }

    public Guid OwnershipOrganizationId { get; set; }
    public Organization OwnershipOrganization
    {
        get => ownershipOrganization ?? throw new InvalidOperationException("Ownership organization navigation has not been loaded.");
        set => ownershipOrganization = value;
    }

    public Guid OperationalFacilityId { get; set; }
    public Facility OperationalFacility
    {
        get => operationalFacility ?? throw new InvalidOperationException("Operational facility navigation has not been loaded.");
        set => operationalFacility = value;
    }

    public Guid? OperationalFacilityUnitId { get; set; }
    public FacilityUnit? OperationalFacilityUnit { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public ResourceAssignmentType AssignmentType { get; set; }
    public string? SourceReference { get; set; }
    public string? Reason { get; set; }
}

public class MaintenanceWorkOrder : SoftDeletableEntity
{
    private Organization? organization;
    private ResourceAsset? resourceAsset;

    public Guid OrganizationId { get; set; }
    public Organization Organization
    {
        get => organization ?? throw new InvalidOperationException("Organization navigation has not been loaded.");
        set => organization = value;
    }

    public Guid ResourceAssetId { get; set; }
    public ResourceAsset ResourceAsset
    {
        get => resourceAsset ?? throw new InvalidOperationException("Resource asset navigation has not been loaded.");
        set => resourceAsset = value;
    }

    public string WorkOrderNumber { get; set; } = string.Empty;
    public MaintenanceType MaintenanceType { get; set; }
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;
    public DateTimeOffset ReportedAtUtc { get; set; }
    public Guid? ReportedByUserId { get; set; }
    public User? ReportedByUser { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    public string? VendorReference { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? ExpectedCompletionAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CompletionSummary { get; set; }
    public bool PartsRequired { get; set; }
    public DateTimeOffset? WaitingForPartsSinceUtc { get; set; }
    public int? DowntimeMinutes { get; set; }
}

public class ResourceRequirement : SoftDeletableEntity
{
    private Organization? organization;
    private Facility? facility;

    public Guid OrganizationId { get; set; }
    public Organization Organization
    {
        get => organization ?? throw new InvalidOperationException("Organization navigation has not been loaded.");
        set => organization = value;
    }

    public Guid FacilityId { get; set; }
    public Facility Facility
    {
        get => facility ?? throw new InvalidOperationException("Facility navigation has not been loaded.");
        set => facility = value;
    }

    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }

    public ResourceType ResourceType { get; set; }
    public string ResourceCategory { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }
    public int MinimumOperationalQuantity { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string? ApprovalReference { get; set; }
    public string? Notes { get; set; }
}

public class ResourceImportBatch : EntityBase
{
    private Facility? facility;

    public Guid FacilityId { get; set; }
    public Facility Facility
    {
        get => facility ?? throw new InvalidOperationException("Facility navigation has not been loaded.");
        set => facility = value;
    }

    public string SourceSystem { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public Guid? SubmittedByUserId { get; set; }
    public User? SubmittedByUser { get; set; }

    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Previewed";
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RejectedRows { get; set; }
    public int DuplicateRows { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public int AppliedRows { get; set; }
}
