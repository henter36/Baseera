namespace Baseera.Domain.Notes;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum NoteStatus
{
    Draft = 0,
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    PendingVerification = 4,
    Closed = 5,
    Reopened = 6,
    Cancelled = 7
}

public enum NoteSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum NoteSourceType
{
    Manual = 0,
    Inspection = 1,
    Report = 2,
    Incident = 3,
    Form = 4
}

public enum NoteCategory
{
    Security = 0,
    Technical = 1,
    Operational = 2,
    HealthAndSafety = 3,
    Administrative = 4,
    Other = 5
}

public enum NoteIntakeLockType
{
    None = 0,
    Region = 1,
    Facility = 2
}

public enum NoteRoutingProcessingTargetType
{
    Department = 0,
    Role = 1
}

public enum NoteRoutingResultStatus
{
    AssignedToDepartment = 0,
    AssignedToUser = 1,
    NoMatchingRule = 2,
    NoEligibleUser = 3,
    InvalidTarget = 4,
    SkippedExistingAssignment = 5,
    ManuallyRouted = 6,
    ManuallyOverridden = 7,
    Failed = 8
}

public enum NoteRoutingTrigger
{
    Submit = 0,
    Reopen = 1,
    ManualRun = 2,
    ManualOverride = 3
}

public enum NoteRoutingRuleChangeType
{
    Created = 0,
    Updated = 1,
    Activated = 2,
    Deactivated = 3,
    Archived = 4,
    Restored = 5
}

public enum NoteTypeAccessPrincipalType
{
    Role = 0,
    User = 1
}

public enum NoteTypeAccessChangeType
{
    BaselineImported = 0,
    Granted = 1,
    Updated = 2,
    Revoked = 3,
    DirectAllowAdded = 4,
    DirectDenyAdded = 5,
    OverrideRemoved = 6
}

public static class NoteDisplay
{
    public static string StatusAr(NoteStatus status) => status switch
    {
        NoteStatus.Draft => "مسودة",
        NoteStatus.Open => "مفتوحة",
        NoteStatus.Assigned => "مكلّفة",
        NoteStatus.InProgress => "قيد المعالجة",
        NoteStatus.PendingVerification => "بانتظار التحقق",
        NoteStatus.Closed => "مغلقة",
        NoteStatus.Reopened => "معاد فتحها",
        NoteStatus.Cancelled => "ملغاة",
        _ => status.ToString()
    };

    public static string SeverityAr(NoteSeverity severity) => severity switch
    {
        NoteSeverity.Low => "منخفضة",
        NoteSeverity.Medium => "متوسطة",
        NoteSeverity.High => "عالية",
        NoteSeverity.Critical => "حرجة",
        _ => severity.ToString()
    };

    public static string CategoryAr(NoteCategory category) => category switch
    {
        NoteCategory.Security => "أمنية",
        NoteCategory.Technical => "فنية",
        NoteCategory.Operational => "تشغيلية",
        NoteCategory.HealthAndSafety => "صحة وسلامة",
        NoteCategory.Administrative => "إدارية",
        NoteCategory.Other => "أخرى",
        _ => category.ToString()
    };

    public static string SourceAr(NoteSourceType source) => source switch
    {
        NoteSourceType.Manual => "يدوي",
        NoteSourceType.Inspection => "تفتيش",
        NoteSourceType.Report => "تقرير",
        NoteSourceType.Incident => "واقعة",
        NoteSourceType.Form => "نموذج",
        _ => source.ToString()
    };
}

public sealed class NoteType : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }
    public string? EntryInstructionsAr { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public NoteSeverity DefaultSeverity { get; set; } = NoteSeverity.Medium;
    public int? DefaultDueDays { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public ICollection<OperationalNote> OperationalNotes { get; set; } = new List<OperationalNote>();
    public ICollection<RoleNoteTypeGrant> RoleGrants { get; set; } = new List<RoleNoteTypeGrant>();
    public ICollection<UserNoteTypeOverride> UserOverrides { get; set; } = new List<UserNoteTypeOverride>();
}

public sealed class RoleNoteTypeGrant : EntityBase
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid NoteTypeId { get; set; }
    public NoteType NoteType { get; set; } = null!;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanAssign { get; set; }
    public bool CanProcess { get; set; }
    public bool CanSubmitForVerification { get; set; }
    public bool CanReview { get; set; }
    public bool CanCancel { get; set; }
    public bool CanReopen { get; set; }
    public bool CanArchive { get; set; }
    public bool CanRestore { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}

public sealed class UserNoteTypeOverride : EntityBase
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid NoteTypeId { get; set; }
    public NoteType NoteType { get; set; } = null!;
    public bool? CanViewOverride { get; set; }
    public bool? CanCreateOverride { get; set; }
    public bool? CanAssignOverride { get; set; }
    public bool? CanProcessOverride { get; set; }
    public bool? CanSubmitForVerificationOverride { get; set; }
    public bool? CanReviewOverride { get; set; }
    public bool? CanCancelOverride { get; set; }
    public bool? CanReopenOverride { get; set; }
    public bool? CanArchiveOverride { get; set; }
    public bool? CanRestoreOverride { get; set; }
    public bool IsActive { get; set; } = true;
    public string Reason { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}

public sealed class UserNoteIntakeProfile : EntityBase
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public NoteIntakeLockType LockType { get; set; } = NoteIntakeLockType.None;
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}

public sealed class NoteRoutingRule : SoftDeletableEntity, IScopedEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }
    public Guid NoteTypeId { get; set; }
    public NoteType NoteType { get; set; } = null!;
    public ScopeType ScopeType { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }
    public int Priority { get; set; }
    public NoteRoutingProcessingTargetType ProcessingTargetType { get; set; }
    public Guid? ProcessingDepartmentId { get; set; }
    public Department? ProcessingDepartment { get; set; }
    public Guid? ProcessingRoleId { get; set; }
    public Role? ProcessingRole { get; set; }
    public Guid? ReviewerRoleId { get; set; }
    public Role? ReviewerRole { get; set; }
    public int? DefaultDueDays { get; set; }
    public bool AutoAssignOnSubmit { get; set; } = true;
    public bool AutoReassignOnReopen { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public User? ActivatedByUser { get; set; }
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public Guid? DeactivatedByUserId { get; set; }
    public User? DeactivatedByUser { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public ICollection<NoteRoutingDecision> Decisions { get; set; } = new List<NoteRoutingDecision>();
    public ICollection<NoteRoutingRuleHistory> History { get; set; } = new List<NoteRoutingRuleHistory>();
}

public sealed class NoteRoutingDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperationalNoteId { get; set; }
    public OperationalNote OperationalNote { get; set; } = null!;
    public NoteRoutingTrigger Trigger { get; set; }
    public int AttemptNumber { get; set; }
    public string DecisionKey { get; set; } = string.Empty;
    public Guid? RoutingRuleId { get; set; }
    public NoteRoutingRule? RoutingRule { get; set; }
    public NoteRoutingResultStatus ResultStatus { get; set; }
    public Guid? ResolvedDepartmentId { get; set; }
    public Department? ResolvedDepartment { get; set; }
    public Guid? ResolvedUserId { get; set; }
    public User? ResolvedUser { get; set; }
    public Guid? ResolvedProcessingRoleId { get; set; }
    public Role? ResolvedProcessingRole { get; set; }
    public Guid? ResolvedReviewerRoleId { get; set; }
    public Role? ResolvedReviewerRole { get; set; }
    public Guid? CreatedAssignmentId { get; set; }
    public DateTimeOffset? DueAtBeforeUtc { get; set; }
    public DateTimeOffset? DueAtAfterUtc { get; set; }
    public string DueAtSource { get; set; } = "None";
    public DateTimeOffset DecidedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
    public string? CorrelationId { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessageSafe { get; set; }
    public string? MetadataJson { get; set; }
}

public sealed class NoteRoutingRuleHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoutingRuleId { get; set; }
    public NoteRoutingRule RoutingRule { get; set; } = null!;
    public NoteRoutingRuleChangeType ChangeType { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public sealed class NoteTypeAccessChangeHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NoteTypeAccessPrincipalType PrincipalType { get; set; }
    public Guid PrincipalId { get; set; }
    public Guid NoteTypeId { get; set; }
    public NoteType NoteType { get; set; } = null!;
    public NoteTypeAccessChangeType ChangeType { get; set; }
    public string? PreviousCapabilitiesJson { get; set; }
    public string? NewCapabilitiesJson { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public class OperationalNote : SoftDeletableEntity, IScopedEntity
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid NoteTypeId { get; set; }
    public NoteType NoteType { get; set; } = null!;
    public NoteSeverity Severity { get; set; }
    public NoteStatus Status { get; set; } = NoteStatus.Draft;
    public NoteSourceType SourceType { get; set; } = NoteSourceType.Manual;
    public string? SourceReference { get; set; }
    public ClassificationLevel Classification { get; set; } = ClassificationLevel.Internal;

    public ScopeType ScopeType { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }

    public Guid? OwnerDepartmentId { get; set; }
    public Department? OwnerDepartment { get; set; }

    public Guid ReportedByUserId { get; set; }
    public User ReportedByUser { get; set; } = null!;
    public DateTimeOffset ReportedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? WorkStartedAtUtc { get; set; }
    public DateTimeOffset? SubmittedForVerificationAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    public string? ClosureSummary { get; set; }
    public Guid? LastProcessedByUserId { get; set; }

    public DateTimeOffset? ReopenedAtUtc { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public User? ReopenedByUser { get; set; }
    public string? ReopenReason { get; set; }

    public ICollection<NoteAssignment> Assignments { get; set; } = new List<NoteAssignment>();
    public ICollection<NoteStatusHistory> StatusHistory { get; set; } = new List<NoteStatusHistory>();
    public ICollection<CorrectiveAction> CorrectiveActions { get; set; } = new List<CorrectiveAction>();
    public ICollection<NoteRoutingDecision> RoutingDecisions { get; set; } = new List<NoteRoutingDecision>();
}

public class NoteAssignment : EntityBase
{
    public Guid OperationalNoteId { get; set; }
    public OperationalNote OperationalNote { get; set; } = null!;

    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }
    public Guid? AssignedToDepartmentId { get; set; }
    public Department? AssignedToDepartment { get; set; }

    public Guid AssignedByUserId { get; set; }
    public User AssignedByUser { get; set; } = null!;
    public DateTimeOffset AssignedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public string? EndReason { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? RoutingDecisionId { get; set; }
    public NoteRoutingDecision? RoutingDecision { get; set; }
}

/// <summary>
/// Append-only workflow timeline visible to users (distinct from system AuditLog).
/// </summary>
public class NoteStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperationalNoteId { get; set; }
    public OperationalNote OperationalNote { get; set; } = null!;
    public NoteStatus? FromStatus { get; set; }
    public NoteStatus ToStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public Guid? AssignmentId { get; set; }
    public NoteAssignment? Assignment { get; set; }
    public string? MetadataJson { get; set; }
}
