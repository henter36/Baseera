namespace Baseera.Domain.Escalations;

using Baseera.Domain.Common;
using Baseera.Domain.Attachments;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum EscalationTargetType
{
    OperationalNote = 0,
    CorrectiveAction = 1
}

public enum EscalationTriggerType
{
    DueSoon = 0,
    Overdue = 1
}

public enum EscalationRecipientStrategy
{
    CurrentAssignedUser = 0,
    SpecificUser = 1,
    SpecificRoleInTargetScope = 2,
    FacilityDirector = 3,
    RegionalDirector = 4,
    HeadquartersExecutive = 5
}

public enum EscalationOccurrenceStatus
{
    Created = 0,
    NotificationsCreated = 1,
    Suppressed = 2,
    Failed = 3
}

public enum NotificationStatus
{
    Unread = 0,
    Read = 1,
    Archived = 2
}

public enum NotificationChannel
{
    InApp = 0
}

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,
    DeadLetter = 3,
    Suppressed = 4
}

public static class EscalationDisplay
{
    public static string TargetTypeAr(EscalationTargetType targetType) => targetType switch
    {
        EscalationTargetType.OperationalNote => "ملاحظة تشغيلية",
        EscalationTargetType.CorrectiveAction => "إجراء تصحيحي",
        _ => targetType.ToString()
    };

    public static string TriggerTypeAr(EscalationTriggerType triggerType) => triggerType switch
    {
        EscalationTriggerType.DueSoon => "قريب الاستحقاق",
        EscalationTriggerType.Overdue => "متأخر",
        _ => triggerType.ToString()
    };

    public static string NotificationStatusAr(NotificationStatus status) => status switch
    {
        NotificationStatus.Unread => "غير مقروء",
        NotificationStatus.Read => "مقروء",
        NotificationStatus.Archived => "مؤرشف",
        _ => status.ToString()
    };
}

public class EscalationPolicy : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EscalationTargetType TargetType { get; set; }
    public bool IsEnabled { get; set; }
    public ScopeType ScopeType { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTimeOffset? ActivatedAtUtc { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public User? ActivatedByUser { get; set; }
    public DateTimeOffset? DeactivatedAtUtc { get; set; }
    public Guid? DeactivatedByUserId { get; set; }
    public User? DeactivatedByUser { get; set; }
    public ICollection<EscalationRule> Rules { get; set; } = new List<EscalationRule>();
}

public class EscalationRule : SoftDeletableEntity
{
    public Guid EscalationPolicyId { get; set; }
    public EscalationPolicy EscalationPolicy { get; set; } = null!;
    public int Level { get; set; }
    public int Priority { get; set; }
    public EscalationTriggerType TriggerType { get; set; }
    public int ThresholdDays { get; set; }
    public int? RepeatEveryDays { get; set; }
    public int? MaximumOccurrences { get; set; }
    public EscalationRecipientStrategy RecipientStrategy { get; set; }
    public string? RecipientRoleCode { get; set; }
    public Guid? SpecificRecipientUserId { get; set; }
    public User? SpecificRecipientUser { get; set; }
    public string TitleTemplateAr { get; set; } = string.Empty;
    public string MessageTemplateAr { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>Append-only record of an escalation occurrence.</summary>
public class EscalationOccurrence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PolicyId { get; set; }
    public EscalationPolicy Policy { get; set; } = null!;
    public Guid RuleId { get; set; }
    public EscalationRule Rule { get; set; } = null!;
    public EscalationTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string TargetReferenceNumber { get; set; } = string.Empty;
    public int EscalationLevel { get; set; }
    public EscalationTriggerType TriggerType { get; set; }
    public int OccurrenceNumber { get; set; }
    public string OccurrenceKey { get; set; } = string.Empty;
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset DetectedAtUtc { get; set; }
    public int RecipientCount { get; set; }
    public EscalationOccurrenceStatus Status { get; set; }
    public string? SuppressionReason { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class Notification : EntityBase
{
    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;
    public Guid? EscalationOccurrenceId { get; set; }
    public EscalationOccurrence? EscalationOccurrence { get; set; }
    public EscalationTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string TargetReferenceNumber { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string MessageAr { get; set; } = string.Empty;
    public int Priority { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;
    public DateTimeOffset? ReadAtUtc { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public ClassificationLevel Classification { get; set; } = ClassificationLevel.Internal;
}

/// <summary>Append-only delivery attempt record. In B.2.2 only InApp is implemented.</summary>
public class NotificationDeliveryAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public int AttemptNumber { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? NextRetryAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessageSafe { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? CorrelationId { get; set; }
}

public class BackgroundJobLease
{
    public string JobName { get; set; } = string.Empty;
    public string LeaseOwner { get; set; } = string.Empty;
    public DateTimeOffset LeaseAcquiredAtUtc { get; set; }
    public DateTimeOffset LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset HeartbeatAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
