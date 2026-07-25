namespace Baseera.Domain.Workforce;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum EmploymentStatus
{
    Active = 0,
    SecondedIn = 1,
    SecondedOut = 2,
    Suspended = 3,
    LongLeave = 4,
    Retired = 5,
    Terminated = 6,
    Unknown = 7
}

public enum WorkforceRoleCategory
{
    Command = 0,
    Security = 1,
    Control = 2,
    Escort = 3,
    Medical = 4,
    Social = 5,
    Technical = 6,
    Logistics = 7,
    Administrative = 8,
    Other = 9
}

public enum WorkforceRoleCriticality
{
    Low = 0,
    Medium = 1,
    High = 2,
    MissionCritical = 3
}

public enum QualificationType
{
    RoleCertification = 0,
    Skill = 1,
    License = 2,
    SecurityClearance = 3,
    FitnessClearance = 4,
    Other = 5
}

public enum QualificationStatus
{
    Valid = 0,
    ExpiringSoon = 1,
    Expired = 2,
    Suspended = 3,
    PendingVerification = 4,
    Unknown = 5
}

public enum AssignmentType
{
    Permanent = 0,
    Temporary = 1,
    Acting = 2,
    EmergencySupport = 3,
    Secondment = 4,
    TrainingCoverage = 5,
    Other = 6
}

public enum AvailabilityType
{
    Available = 0,
    AnnualLeave = 1,
    SickLeave = 2,
    Training = 3,
    ExternalAssignment = 4,
    InternalAssignment = 5,
    Suspended = 6,
    RestrictedDuty = 7,
    EmergencyLeave = 8,
    UnexcusedAbsence = 9,
    Other = 10
}

public enum OperationalRestrictionCode
{
    CannotDrive = 0,
    CannotCarryWeapon = 1,
    CannotWorkNightShift = 2,
    CannotPerformEscort = 3,
    AdministrativeDutyOnly = 4
}

public enum RosterStatus
{
    Draft = 0,
    Published = 1
}

public enum RosterAssignmentStatus
{
    Planned = 0,
    Confirmed = 1,
    Present = 2,
    Late = 3,
    Absent = 4,
    Excused = 5,
    Replaced = 6,
    Completed = 7,
    Cancelled = 8,
    Unknown = 9
}

public enum WorkforceSourceType
{
    Manual = 0,
    Import = 1,
    ExternalSystem = 2,
    Audit = 3,
    Other = 4
}

public enum WorkforceCoverageStatus
{
    Ready = 0,
    Attention = 1,
    Critical = 2,
    Unsafe = 3,
    Unknown = 4
}

public static class WorkforceImportBatchStatuses
{
    public const string Previewed = "Previewed";
    public const string Confirmed = "Confirmed";
}

public static class DutyRosterStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
}

public class WorkforceMember : SoftDeletableEntity, IScopedEntity
{
    private Organization? organization;
    private Organization? administrativeOrganization;

    public Guid OrganizationId { get; set; }
    public Organization Organization
    {
        get => organization ?? throw new InvalidOperationException("Organization navigation has not been loaded.");
        set => organization = value;
    }

    public string? ExternalPersonnelId { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Unknown;
    public string? RankOrGrade { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string PrimarySpecialty { get; set; } = string.Empty;

    public Guid AdministrativeOrganizationId { get; set; }
    public Organization AdministrativeOrganization
    {
        get => administrativeOrganization ?? throw new InvalidOperationException("Administrative organization navigation has not been loaded.");
        set => administrativeOrganization = value;
    }

    public Guid? HomeFacilityId { get; set; }
    public Facility? HomeFacility { get; set; }

    public Guid? CurrentOperationalFacilityId { get; set; }
    public Facility? CurrentOperationalFacility { get; set; }

    public Guid? CurrentOperationalUnitId { get; set; }
    public FacilityUnit? CurrentOperationalUnit { get; set; }

    public Guid? SupervisorWorkforceMemberId { get; set; }
    public WorkforceMember? SupervisorWorkforceMember { get; set; }

    public DateTimeOffset? HireDateUtc { get; set; }
    public DateTimeOffset? ServiceStartDateUtc { get; set; }
    public bool IsOperational { get; set; } = true;
    public bool IsSensitiveRole { get; set; }
    public WorkforceSourceType SourceType { get; set; } = WorkforceSourceType.Manual;
    public string? SourceReference { get; set; }
    public DateTimeOffset? LastVerifiedAtUtc { get; set; }

    /// <summary>
    /// Facility-scoped when posted to an operational facility; otherwise organization (headquarters) level.
    /// </summary>
    public ScopeType ScopeType => CurrentOperationalFacilityId.HasValue
        ? ScopeType.Facility
        : ScopeType.Headquarters;

    Guid? IScopedEntity.RegionId => CurrentOperationalFacility?.RegionId ?? HomeFacility?.RegionId;
    Guid? IScopedEntity.FacilityId => CurrentOperationalFacilityId ?? HomeFacilityId;
    Guid? IScopedEntity.FacilityUnitId => CurrentOperationalUnitId;

    public ICollection<WorkforceQualification> Qualifications { get; set; } = new List<WorkforceQualification>();
    public ICollection<WorkforceAssignment> Assignments { get; set; } = new List<WorkforceAssignment>();
    public ICollection<WorkforceAvailabilityEvent> AvailabilityEvents { get; set; } = new List<WorkforceAvailabilityEvent>();
}

public class WorkforceRoleDefinition : SoftDeletableEntity
{
    private Organization? organization;

    public Guid OrganizationId { get; set; }
    public Organization Organization
    {
        get => organization ?? throw new InvalidOperationException("Organization navigation has not been loaded.");
        set => organization = value;
    }

    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public WorkforceRoleCategory Category { get; set; }
    public WorkforceRoleCriticality Criticality { get; set; } = WorkforceRoleCriticality.Medium;
    public bool RequiresCertification { get; set; }
    public bool RequiresActiveFitness { get; set; }
    public bool RequiresSecurityClearance { get; set; }
    public bool CanCoverMultipleUnits { get; set; }
    public bool IsShiftBased { get; set; } = true;
    public bool IsSensitive { get; set; }
}

public class WorkforceQualification : SoftDeletableEntity
{
    private WorkforceMember? workforceMember;

    public Guid WorkforceMemberId { get; set; }
    public WorkforceMember WorkforceMember
    {
        get => workforceMember ?? throw new InvalidOperationException("Workforce member navigation has not been loaded.");
        set => workforceMember = value;
    }

    public QualificationType QualificationType { get; set; }
    public Guid? RoleDefinitionId { get; set; }
    public WorkforceRoleDefinition? RoleDefinition { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateTimeOffset? IssuedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string? Issuer { get; set; }
    public string? Reference { get; set; }
    public QualificationStatus Status { get; set; } = QualificationStatus.Unknown;
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public string? VerifiedBy { get; set; }
    public Guid? AttachmentId { get; set; }
}

public class WorkforceAssignment : SoftDeletableEntity
{
    private WorkforceMember? workforceMember;
    private Facility? facility;
    private WorkforceRoleDefinition? roleDefinition;

    public Guid WorkforceMemberId { get; set; }
    public WorkforceMember WorkforceMember
    {
        get => workforceMember ?? throw new InvalidOperationException("Workforce member navigation has not been loaded.");
        set => workforceMember = value;
    }

    public Guid FacilityId { get; set; }
    public Facility Facility
    {
        get => facility ?? throw new InvalidOperationException("Facility navigation has not been loaded.");
        set => facility = value;
    }

    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }

    public Guid RoleDefinitionId { get; set; }
    public WorkforceRoleDefinition RoleDefinition
    {
        get => roleDefinition ?? throw new InvalidOperationException("Role definition navigation has not been loaded.");
        set => roleDefinition = value;
    }

    public AssignmentType AssignmentType { get; set; } = AssignmentType.Permanent;
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public bool IsPrimary { get; set; } = true;
    public string? SourceReference { get; set; }
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
}

public class StaffingRequirement : SoftDeletableEntity
{
    private Organization? organization;
    private Facility? facility;
    private WorkforceRoleDefinition? roleDefinition;

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

    public Guid RoleDefinitionId { get; set; }
    public WorkforceRoleDefinition RoleDefinition
    {
        get => roleDefinition ?? throw new InvalidOperationException("Role definition navigation has not been loaded.");
        set => roleDefinition = value;
    }

    public Guid? ShiftDefinitionId { get; set; }
    public ShiftDefinition? ShiftDefinition { get; set; }

    public int RequiredHeadcount { get; set; }
    public int MinimumSafeHeadcount { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string? ApprovalReference { get; set; }
    public string? Notes { get; set; }
}

public class ShiftDefinition : SoftDeletableEntity
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

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartLocalTime { get; set; }
    public TimeOnly EndLocalTime { get; set; }
    public bool CrossesMidnight { get; set; }
    public string Timezone { get; set; } = "Asia/Riyadh";
    public bool IsActive { get; set; } = true;
}

public class DutyRoster : SoftDeletableEntity
{
    private Facility? facility;
    private ShiftDefinition? shiftDefinition;

    public Guid FacilityId { get; set; }
    public Facility Facility
    {
        get => facility ?? throw new InvalidOperationException("Facility navigation has not been loaded.");
        set => facility = value;
    }

    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }

    public Guid ShiftDefinitionId { get; set; }
    public ShiftDefinition ShiftDefinition
    {
        get => shiftDefinition ?? throw new InvalidOperationException("Shift definition navigation has not been loaded.");
        set => shiftDefinition = value;
    }

    public DateOnly DutyDate { get; set; }
    public string Status { get; set; } = DutyRosterStatuses.Draft;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string? PublishedBy { get; set; }

    public ICollection<DutyRosterAssignment> Assignments { get; set; } = new List<DutyRosterAssignment>();
}

public class DutyRosterAssignment : SoftDeletableEntity
{
    private DutyRoster? dutyRoster;
    private WorkforceMember? workforceMember;
    private WorkforceRoleDefinition? roleDefinition;

    public Guid DutyRosterId { get; set; }
    public DutyRoster DutyRoster
    {
        get => dutyRoster ?? throw new InvalidOperationException("Duty roster navigation has not been loaded.");
        set => dutyRoster = value;
    }

    public Guid WorkforceMemberId { get; set; }
    public WorkforceMember WorkforceMember
    {
        get => workforceMember ?? throw new InvalidOperationException("Workforce member navigation has not been loaded.");
        set => workforceMember = value;
    }

    public Guid RoleDefinitionId { get; set; }
    public WorkforceRoleDefinition RoleDefinition
    {
        get => roleDefinition ?? throw new InvalidOperationException("Role definition navigation has not been loaded.");
        set => roleDefinition = value;
    }

    public RosterAssignmentStatus Status { get; set; } = RosterAssignmentStatus.Planned;
    public DateTimeOffset? CheckInAtUtc { get; set; }
    public DateTimeOffset? CheckOutAtUtc { get; set; }
    public Guid? ReplacementForAssignmentId { get; set; }
    public DutyRosterAssignment? ReplacementForAssignment { get; set; }
    public string? Notes { get; set; }
}

public class WorkforceAvailabilityEvent : SoftDeletableEntity
{
    private WorkforceMember? workforceMember;

    public Guid WorkforceMemberId { get; set; }
    public WorkforceMember WorkforceMember
    {
        get => workforceMember ?? throw new InvalidOperationException("Workforce member navigation has not been loaded.");
        set => workforceMember = value;
    }

    public AvailabilityType AvailabilityType { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool AffectsOperationalAvailability { get; set; } = true;
    public WorkforceSourceType SourceType { get; set; } = WorkforceSourceType.Manual;
    public string? SourceReference { get; set; }
    public string? ReasonCode { get; set; }

    /// <summary>
    /// Comma-separated <see cref="OperationalRestrictionCode"/> names when type is RestrictedDuty.
    /// Never stores medical diagnosis text.
    /// </summary>
    public string? RestrictionCodesCsv { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? RecordedBy { get; set; }
}

public class CriticalPositionRequirement : SoftDeletableEntity
{
    private Facility? facility;
    private WorkforceRoleDefinition? roleDefinition;

    public Guid FacilityId { get; set; }
    public Facility Facility
    {
        get => facility ?? throw new InvalidOperationException("Facility navigation has not been loaded.");
        set => facility = value;
    }

    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }

    public Guid RoleDefinitionId { get; set; }
    public WorkforceRoleDefinition RoleDefinition
    {
        get => roleDefinition ?? throw new InvalidOperationException("Role definition navigation has not been loaded.");
        set => roleDefinition = value;
    }

    public Guid? ShiftDefinitionId { get; set; }
    public ShiftDefinition? ShiftDefinition { get; set; }

    public int RequiredPrimaryCount { get; set; } = 1;
    public int RequiredAlternateCount { get; set; }
    public WorkforceRoleCriticality Criticality { get; set; } = WorkforceRoleCriticality.MissionCritical;
    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
}

public class WorkforceReadinessSnapshot : EntityBase
{
    private Facility? facility;

    public Guid FacilityId { get; set; }
    public Facility Facility
    {
        get => facility ?? throw new InvalidOperationException("Facility navigation has not been loaded.");
        set => facility = value;
    }

    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }

    public Guid? ShiftDefinitionId { get; set; }
    public ShiftDefinition? ShiftDefinition { get; set; }

    public Guid? RoleDefinitionId { get; set; }
    public WorkforceRoleDefinition? RoleDefinition { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Required { get; set; }
    public int MinimumSafe { get; set; }
    public int Assigned { get; set; }
    public int Scheduled { get; set; }
    public int Present { get; set; }
    public int OperationallyAvailable { get; set; }
    public int Qualified { get; set; }
    public int Unqualified { get; set; }
    public int Absent { get; set; }
    public int OnLeave { get; set; }
    public int InTraining { get; set; }
    public int Restricted { get; set; }
    public int Overtime { get; set; }
    public int Gap { get; set; }
    public int SafeGap { get; set; }
    public decimal? CoverageRate { get; set; }
    public decimal? QualificationCoverage { get; set; }
    public string Freshness { get; set; } = "unknown";
    public string Confidence { get; set; } = "unknown";
    public string SourceStatus { get; set; } = "unknown";
    public WorkforceCoverageStatus CoverageStatus { get; set; } = WorkforceCoverageStatus.Unknown;
}

public class WorkforceImportBatch : EntityBase
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
    public string Status { get; set; } = WorkforceImportBatchStatuses.Previewed;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RejectedRows { get; set; }
    public int DuplicateRows { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public int AppliedRows { get; set; }
}
