namespace Baseera.Domain.CorrectiveActions;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;

public enum CorrectiveActionStatus
{
    Draft = 0,
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    PendingVerification = 4,
    Completed = 5,
    Reopened = 6,
    Cancelled = 7
}

public enum CorrectiveActionPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public static class CorrectiveActionDisplay
{
    public static string StatusAr(CorrectiveActionStatus status) => status switch
    {
        CorrectiveActionStatus.Draft => "مسودة",
        CorrectiveActionStatus.Open => "مفتوح",
        CorrectiveActionStatus.Assigned => "مكلّف",
        CorrectiveActionStatus.InProgress => "قيد المعالجة",
        CorrectiveActionStatus.PendingVerification => "بانتظار التحقق",
        CorrectiveActionStatus.Completed => "مكتمل",
        CorrectiveActionStatus.Reopened => "معاد فتحه",
        CorrectiveActionStatus.Cancelled => "ملغى",
        _ => status.ToString()
    };

    public static string PriorityAr(CorrectiveActionPriority priority) => priority switch
    {
        CorrectiveActionPriority.Low => "منخفضة",
        CorrectiveActionPriority.Medium => "متوسطة",
        CorrectiveActionPriority.High => "عالية",
        CorrectiveActionPriority.Critical => "حرجة",
        _ => priority.ToString()
    };
}

public class CorrectiveAction : SoftDeletableEntity
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public Guid OperationalNoteId { get; set; }
    public OperationalNote OperationalNote { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CorrectiveActionPriority Priority { get; set; } = CorrectiveActionPriority.Medium;
    public CorrectiveActionStatus Status { get; set; } = CorrectiveActionStatus.Draft;
    public ClassificationLevel Classification { get; set; } = ClassificationLevel.Internal;

    public Guid? OwnerDepartmentId { get; set; }
    public Department? OwnerDepartment { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? WorkStartedAtUtc { get; set; }
    public DateTimeOffset? SubmittedForVerificationAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
    public string? CompletionSummary { get; set; }
    public DateTimeOffset? ReopenedAtUtc { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public User? ReopenedByUser { get; set; }
    public string? ReopenReason { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public Guid? LastProcessedByUserId { get; set; }
    public User? LastProcessedByUser { get; set; }

    public ICollection<CorrectiveActionAssignment> Assignments { get; set; } = new List<CorrectiveActionAssignment>();
    public ICollection<CorrectiveActionStatusHistory> StatusHistory { get; set; } = new List<CorrectiveActionStatusHistory>();
}

public class CorrectiveActionAssignment : EntityBase
{
    public Guid CorrectiveActionId { get; set; }
    public CorrectiveAction CorrectiveAction { get; set; } = null!;

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

public class CorrectiveActionStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CorrectiveActionId { get; set; }
    public CorrectiveAction CorrectiveAction { get; set; } = null!;
    public CorrectiveActionStatus? FromStatus { get; set; }
    public CorrectiveActionStatus ToStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public Guid? AssignmentId { get; set; }
    public CorrectiveActionAssignment? Assignment { get; set; }
    public string? MetadataJson { get; set; }
}
