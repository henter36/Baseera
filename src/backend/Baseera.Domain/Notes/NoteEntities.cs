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
