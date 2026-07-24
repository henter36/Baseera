namespace Baseera.Domain.Occupancy;

using Baseera.Domain.Common;
using Baseera.Domain.Organization;

public enum CapacityType
{
    ApprovedOperational = 0,
    Emergency = 1,
    Temporary = 2,
    MedicalIsolation = 3,
    SecurityIsolation = 4,
    Other = 99
}

public enum OccupancySourceType
{
    Manual = 0,
    ExternalSystem = 1,
    Import = 2,
    Reconciliation = 3
}

public enum CensusQualityStatus
{
    Complete = 0,
    Partial = 1,
    Stale = 2,
    Missing = 3,
    Conflicting = 4
}

public enum MovementType
{
    Admission = 0,
    Release = 1,
    TransferIn = 2,
    TransferOut = 3,
    InternalTransfer = 4,
    TemporaryLeave = 5,
    ReturnFromLeave = 6,
    HospitalTransfer = 7,
    CourtTransfer = 8,
    Death = 9,
    Correction = 10,
    Other = 99
}

public class FacilityCapacityBaseline : SoftDeletableEntity, IScopedEntity
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
    public CapacityType CapacityType { get; set; } = CapacityType.ApprovedOperational;
    public int ApprovedCapacity { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public string? ApprovalReference { get; set; }
    public DateTimeOffset? ApprovalDateUtc { get; set; }
    public OccupancySourceType SourceType { get; set; } = OccupancySourceType.Manual;
    public string SourceReference { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public ScopeType ScopeType => FacilityUnitId.HasValue ? ScopeType.FacilityUnit : ScopeType.Facility;
    Guid? IScopedEntity.RegionId => facility?.RegionId;
    Guid? IScopedEntity.FacilityId => FacilityId;
    Guid? IScopedEntity.FacilityUnitId => FacilityUnitId;
}

public class InmateCensusSnapshot : SoftDeletableEntity, IScopedEntity
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
    public DateTimeOffset CapturedAtUtc { get; set; }
    public int InmateCount { get; set; }
    public int? MaleCount { get; set; }
    public int? FemaleCount { get; set; }
    public int? AdultCount { get; set; }
    public int? JuvenileCount { get; set; }
    public int? MedicalCount { get; set; }
    public int? IsolationCount { get; set; }
    public OccupancySourceType SourceType { get; set; } = OccupancySourceType.Manual;
    public string SourceReference { get; set; } = string.Empty;
    public string? SourceVersion { get; set; }
    public DateTimeOffset? ImportedAtUtc { get; set; }
    public bool IsAuthoritative { get; set; }
    public CensusQualityStatus QualityStatus { get; set; } = CensusQualityStatus.Complete;
    public string? QualityNotes { get; set; }

    public ScopeType ScopeType => FacilityUnitId.HasValue ? ScopeType.FacilityUnit : ScopeType.Facility;
    Guid? IScopedEntity.RegionId => facility?.RegionId;
    Guid? IScopedEntity.FacilityId => FacilityId;
    Guid? IScopedEntity.FacilityUnitId => FacilityUnitId;
}

public class InmateMovementEvent : SoftDeletableEntity, IScopedEntity
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

    public string InmateReferenceHash { get; set; } = string.Empty;
    public MovementType MovementType { get; set; }
    public Guid? FromFacilityId { get; set; }
    public Facility? FromFacility { get; set; }
    public Guid? ToFacilityId { get; set; }
    public Facility? ToFacility { get; set; }
    public Guid? FromFacilityUnitId { get; set; }
    public FacilityUnit? FromFacilityUnit { get; set; }
    public Guid? ToFacilityUnitId { get; set; }
    public FacilityUnit? ToFacilityUnit { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public OccupancySourceType SourceType { get; set; } = OccupancySourceType.Manual;
    public string SourceReference { get; set; } = string.Empty;
    public string? ExternalEventId { get; set; }
    public string? ReasonCode { get; set; }
    public bool IsReversed { get; set; }
    public Guid? ReversedByEventId { get; set; }
    public InmateMovementEvent? ReversedByEvent { get; set; }

    public ScopeType ScopeType => ScopeType.Facility;
    Guid? IScopedEntity.RegionId => facility?.RegionId;
    Guid? IScopedEntity.FacilityId => FacilityId;
    Guid? IScopedEntity.FacilityUnitId => null;
}
