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

/// <summary>قرار فرز الملاحظة — بوابة مستقلة عن نتيجة المعالجة (Phase 1B).</summary>
public enum NoteTriageOutcome
{
    Valid = 0,
    Invalid = 1,
    Duplicate = 2
}

/// <summary>نتيجة المعالجة — لا تظهر إلا بعد اعتماد "صحيحة".</summary>
public enum NoteTreatmentResultType
{
    Treated = 0,
    NoActionRequired = 1
}

/// <summary>نوع التنفيذ ضمن نتيجة "معالجة" — ظهور "تتطلب قطع" مشروط بـ NoteType.SupportsPartsWorkflow.</summary>
public enum NoteTreatmentExecutionType
{
    Direct = 0,
    RequiresParts = 1
}

/// <summary>
/// نوع طلب الاعتماد الموحّد (Four-eyes) — نموذج واحد لكل مسارات القرار التي تُغلق الملاحظة مباشرة
/// دون المرور بـPendingVerification. اعتماد نتيجة "معالجة" (TreatmentResultApproval) يُنجَز عبر خط
/// الأنابيب القائم SubmitForVerification→VerifyClosure نفسه (SoD موسَّع في NoteWorkflowService)، وليس
/// عبر هذا النوع — تجنّبًا لبناء منطق اعتماد مواز فوق الـState Machine القائمة
/// (راجع docs/ux-rescue/phase1b-observation-architecture.md).
/// </summary>
public enum NoteDecisionApprovalType
{
    Invalid = 0,
    Duplicate = 1,
    NoAction = 2
}

public enum NoteDecisionApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Returned = 2
}

/// <summary>سبب الإغلاق النهائي — لا يوسّع NoteStatus؛ يُعرض كـClosureReason منفصل فوق Status=Closed.</summary>
public enum NoteClosureReason
{
    Treated = 0,
    Invalid = 1,
    Duplicate = 2,
    NoActionRequired = 3
}

public enum NotePartsRequirementStatus
{
    Requested = 0,
    Sourcing = 1,
    Available = 2,
    Received = 3,
    Installed = 4,
    Cancelled = 5
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

    public static string TriageOutcomeAr(NoteTriageOutcome outcome) => outcome switch
    {
        NoteTriageOutcome.Valid => "صحيحة",
        NoteTriageOutcome.Invalid => "غير صحيحة",
        NoteTriageOutcome.Duplicate => "مكررة",
        _ => outcome.ToString()
    };

    public static string TreatmentResultTypeAr(NoteTreatmentResultType type) => type switch
    {
        NoteTreatmentResultType.Treated => "معالجة",
        NoteTreatmentResultType.NoActionRequired => "لا تتطلب إجراء",
        _ => type.ToString()
    };

    public static string TreatmentExecutionTypeAr(NoteTreatmentExecutionType type) => type switch
    {
        NoteTreatmentExecutionType.Direct => "معالجة مباشرة",
        NoteTreatmentExecutionType.RequiresParts => "تتطلب قطع أو مواد",
        _ => type.ToString()
    };

    public static string DecisionApprovalTypeAr(NoteDecisionApprovalType type) => type switch
    {
        NoteDecisionApprovalType.Invalid => "اعتماد غير صحيحة",
        NoteDecisionApprovalType.Duplicate => "اعتماد التكرار",
        NoteDecisionApprovalType.NoAction => "اعتماد لا تتطلب إجراء",
        _ => type.ToString()
    };

    public static string DecisionApprovalStatusAr(NoteDecisionApprovalStatus status) => status switch
    {
        NoteDecisionApprovalStatus.Pending => "بانتظار الاعتماد",
        NoteDecisionApprovalStatus.Approved => "معتمد",
        NoteDecisionApprovalStatus.Returned => "معاد",
        _ => status.ToString()
    };

    public static string ClosureReasonAr(NoteClosureReason reason) => reason switch
    {
        NoteClosureReason.Treated => "مغلقة — تمت المعالجة",
        NoteClosureReason.Invalid => "مغلقة — غير صحيحة",
        NoteClosureReason.Duplicate => "مغلقة — مكررة",
        NoteClosureReason.NoActionRequired => "مغلقة — لا تتطلب إجراء",
        _ => reason.ToString()
    };

    public static string PartsRequirementStatusAr(NotePartsRequirementStatus status) => status switch
    {
        NotePartsRequirementStatus.Requested => "مطلوبة",
        NotePartsRequirementStatus.Sourcing => "قيد التوريد",
        NotePartsRequirementStatus.Available => "متوفرة",
        NotePartsRequirementStatus.Received => "تم الاستلام",
        NotePartsRequirementStatus.Installed => "تم التركيب",
        NotePartsRequirementStatus.Cancelled => "ملغاة",
        _ => status.ToString()
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
    /// <summary>Server-authored gate for the "تتطلب قطع أو مواد" execution-type branch — never inferred from Code/NameAr on the client.</summary>
    public bool SupportsPartsWorkflow { get; set; }
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

    // --- Phase 1B: triage gate (Layer 1, independent of treatment result) ---
    public NoteTriageOutcome? TriageOutcome { get; set; }
    public DateTimeOffset? TriageDecidedAtUtc { get; set; }
    public Guid? TriageDecidedByUserId { get; set; }
    public User? TriageDecidedByUser { get; set; }

    /// <summary>Set only when TriageOutcome=Duplicate and the duplicate decision has been approved.</summary>
    public Guid? DuplicateOfNoteId { get; set; }
    public OperationalNote? DuplicateOfNote { get; set; }

    // --- Phase 1B: treatment result (Layer 2, only meaningful once TriageOutcome=Valid) ---
    public NoteTreatmentResultType? TreatmentResultType { get; set; }
    public NoteTreatmentExecutionType? TreatmentExecutionType { get; set; }
    public string? TreatmentResultText { get; set; }
    public string? NoActionJustificationAr { get; set; }

    /// <summary>Final closure reason — orthogonal to Status; Status stays Closed for all four outcomes.</summary>
    public NoteClosureReason? ClosureReason { get; set; }

    public ICollection<NoteAssignment> Assignments { get; set; } = new List<NoteAssignment>();
    public ICollection<NoteStatusHistory> StatusHistory { get; set; } = new List<NoteStatusHistory>();
    public ICollection<CorrectiveAction> CorrectiveActions { get; set; } = new List<CorrectiveAction>();
    public ICollection<NoteRoutingDecision> RoutingDecisions { get; set; } = new List<NoteRoutingDecision>();
    public ICollection<NoteDecisionApproval> DecisionApprovals { get; set; } = new List<NoteDecisionApproval>();
    public ICollection<NotePartsRequirement> PartsRequirements { get; set; } = new List<NotePartsRequirement>();
    public ICollection<NoteSlaPausePeriod> SlaPausePeriods { get; set; } = new List<NoteSlaPausePeriod>();
}

/// <summary>
/// Unified four-eyes approval request — one entity type for all decision kinds (Invalid/Duplicate/NoAction/Treatment).
/// Proposer and reviewer must always differ; enforced in NoteDecisionApprovalService, not here.
/// </summary>
public sealed class NoteDecisionApproval : EntityBase
{
    public Guid OperationalNoteId { get; set; }
    public OperationalNote OperationalNote { get; set; } = null!;
    public NoteDecisionApprovalType DecisionType { get; set; }
    public NoteDecisionApprovalStatus Status { get; set; } = NoteDecisionApprovalStatus.Pending;

    /// <summary>مبرر اعتبارها غير صحيحة / مكررة / لا تتطلب إجراء (حسب DecisionType).</summary>
    public string? JustificationAr { get; set; }

    /// <summary>الملاحظة الأصلية المقترحة عند DecisionType=Duplicate فقط.</summary>
    public Guid? OriginalNoteId { get; set; }
    public OperationalNote? OriginalNote { get; set; }

    public Guid ProposedByUserId { get; set; }
    public User ProposedByUser { get; set; } = null!;
    public DateTimeOffset ProposedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }

    /// <summary>سبب إلزامي عند الإعادة؛ اختياري عند الاعتماد.</summary>
    public string? ReviewReason { get; set; }
}

/// <summary>عنصر واحد ضمن PartsRequirement[] — تعدد قطع حقيقي، لا حقل قطعة واحدة.</summary>
public sealed class NotePartsRequirement : EntityBase
{
    public Guid OperationalNoteId { get; set; }
    public OperationalNote OperationalNote { get; set; } = null!;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemCode { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? RequestNumber { get; set; }
    public NotePartsRequirementStatus Status { get; set; } = NotePartsRequirementStatus.Requested;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public DateTimeOffset? ReceivedAtUtc { get; set; }
    public DateTimeOffset? InstalledAtUtc { get; set; }
    public string? SupplierOrSource { get; set; }
    public string? Notes { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public Guid CreatedByUserId { get; set; }
}

/// <summary>
/// One recorded pause window for ProcessingSla while waiting on an approved external parts/supply wait.
/// Historical periods are append-only once EndedAtUtc is set (never re-dated on recompute).
/// </summary>
public sealed class NoteSlaPausePeriod : EntityBase
{
    public Guid OperationalNoteId { get; set; }
    public OperationalNote OperationalNote { get; set; } = null!;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public DateTimeOffset? ReviewDueAtUtc { get; set; }
    public string? EndReason { get; set; }
    /// <summary>Comma-separated NotePartsRequirement ids this pause is justified by (pragmatic join for a bounded, small set).</summary>
    public string? RelatedPartsRequirementIdsCsv { get; set; }
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
