namespace Baseera.Domain.Forms;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum FormCampaignStatus
{
    Draft = 0,
    Scheduled = 1,
    Active = 2,
    Paused = 3,
    Completed = 4,
    Cancelled = 5
}

public enum FormCycleStatus
{
    Scheduled = 0,
    Open = 1,
    Grace = 2,
    Closed = 3,
    Cancelled = 4
}

public enum FormRecurrenceKind
{
    Once = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    CustomDates = 4
}

public enum FormTargetRuleType
{
    AllFacilities = 0,
    Regions = 1,
    Facilities = 2,
    DynamicCriteria = 3
}

public enum FormCampaignPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum BusinessDayAdjustment
{
    None = 0,
    NextBusinessDay = 1,
    PreviousBusinessDay = 2
}

public enum MonthlyMissingDayPolicy
{
    ClampToLastDay = 0,
    SkipOccurrence = 1
}

public class FormCampaign : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FormDefinitionId { get; set; }
    public FormDefinition FormDefinition { get; set; } = null!;
    public Guid FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;
    public Guid FormSchemaSnapshotId { get; set; }
    public FormSchemaSnapshot FormSchemaSnapshot { get; set; } = null!;
    public string SchemaHash { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public FormCampaignStatus Status { get; set; } = FormCampaignStatus.Draft;
    public FormCampaignPriority Priority { get; set; } = FormCampaignPriority.Normal;
    public string TimeZoneId { get; set; } = "Asia/Riyadh";
    public FormRecurrenceKind RecurrenceKind { get; set; } = FormRecurrenceKind.Once;
    public string RecurrenceConfigurationJson { get; set; } = "{}";
    public DateTimeOffset FirstOpenAtLocal { get; set; }
    public int ResponseWindowMinutes { get; set; } = 1440;
    public int GracePeriodMinutes { get; set; }
    public int CloseAfterMinutes { get; set; }
    public BusinessDayAdjustment BusinessDayAdjustment { get; set; } = BusinessDayAdjustment.None;
    public DateTimeOffset? NextOccurrenceUtc { get; set; }
    public DateTimeOffset? LastGeneratedOccurrenceUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public User? PublishedByUser { get; set; }
    public DateTimeOffset? PausedAtUtc { get; set; }
    public Guid? PausedByUserId { get; set; }
    public User? PausedByUser { get; set; }
    public string? PauseReason { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public User? DeletedByUser { get; set; }
    public ICollection<FormTargetRule> TargetRules { get; set; } = new List<FormTargetRule>();
    public ICollection<FormCampaignExclusion> Exclusions { get; set; } = new List<FormCampaignExclusion>();
    public ICollection<FormCycle> Cycles { get; set; } = new List<FormCycle>();
}

public class FormTargetRule : EntityBase
{
    public Guid CampaignId { get; set; }
    public FormCampaign Campaign { get; set; } = null!;
    public FormTargetRuleType RuleType { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}

public class FormCampaignExclusion : EntityBase
{
    public Guid CampaignId { get; set; }
    public FormCampaign Campaign { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}

public class FormCycle : EntityBase
{
    public Guid CampaignId { get; set; }
    public FormCampaign Campaign { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public string OccurrenceKey { get; set; } = string.Empty;
    public FormCycleStatus Status { get; set; } = FormCycleStatus.Scheduled;
    public DateTimeOffset ScheduledOccurrenceLocal { get; set; }
    public DateTimeOffset ScheduledOccurrenceUtc { get; set; }
    public DateTimeOffset OpenAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset GraceEndsAtUtc { get; set; }
    public DateTimeOffset CloseAtUtc { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Riyadh";
    public Guid FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;
    public Guid FormSchemaSnapshotId { get; set; }
    public FormSchemaSnapshot FormSchemaSnapshot { get; set; } = null!;
    public string SchemaHash { get; set; } = string.Empty;
    public string TargetSnapshotHash { get; set; } = string.Empty;
    public int AssignedFacilityCount { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public ICollection<FormFacilityAssignment> Assignments { get; set; } = new List<FormFacilityAssignment>();
}

public class FormFacilityAssignment : EntityBase
{
    public Guid CampaignId { get; set; }
    public FormCampaign Campaign { get; set; } = null!;
    public Guid CycleId { get; set; }
    public FormCycle Cycle { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid RegionIdAtAssignment { get; set; }
    public string FacilityCodeAtAssignment { get; set; } = string.Empty;
    public string FacilityNameArAtAssignment { get; set; } = string.Empty;
    public string RegionNameArAtAssignment { get; set; } = string.Empty;
    public string? FacilityTypeAtAssignment { get; set; }
    public FormTargetRuleType TargetRuleType { get; set; }
    public DateTimeOffset AssignedAtUtc { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? UnavailableReason { get; set; }
}

public class OrganizationBusinessCalendarDate : EntityBase
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public DateOnly LocalDate { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public bool IsWorkingDayOverride { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
