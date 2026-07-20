namespace Baseera.Application.Notes;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;

public sealed record NoteListItemDto(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string? DescriptionSnippet,
    NoteStatus Status,
    string StatusAr,
    NoteSeverity Severity,
    string SeverityAr,
    Guid NoteTypeId,
    string NoteTypeCode,
    string NoteTypeNameAr,
    bool NoteTypeIsActive,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    DateTimeOffset? DueAtUtc,
    bool IsOverdue,
    string? CurrentAssigneeDisplay,
    DateTimeOffset CreatedAtUtc,
    string RowVersion,
    bool IsSensitiveRedacted);

public sealed record NoteDetailDto(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    NoteStatus Status,
    string StatusAr,
    NoteSeverity Severity,
    string SeverityAr,
    Guid NoteTypeId,
    string NoteTypeCode,
    string NoteTypeNameAr,
    string? NoteTypeDescriptionAr,
    string? NoteTypeEntryInstructionsAr,
    bool NoteTypeIsActive,
    NoteSourceType SourceType,
    string SourceAr,
    string? SourceReference,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerDepartmentId,
    Guid ReportedByUserId,
    string? ReportedByDisplayName,
    DateTimeOffset ReportedAtUtc,
    DateTimeOffset? DueAtUtc,
    bool IsOverdue,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? WorkStartedAtUtc,
    DateTimeOffset? SubmittedForVerificationAtUtc,
    DateTimeOffset? ClosedAtUtc,
    Guid? ClosedByUserId,
    string? ClosureSummary,
    DateTimeOffset? ReopenedAtUtc,
    string? ReopenReason,
    NoteAssignmentDto? CurrentAssignment,
    DateTimeOffset CreatedAtUtc,
    string RowVersion,
    bool IsSensitiveRedacted);

public sealed record NoteAssignmentDto(
    Guid Id,
    Guid OperationalNoteId,
    Guid? AssignedToUserId,
    string? AssignedToUserDisplayName,
    Guid? AssignedToDepartmentId,
    string? AssignedToDepartmentName,
    Guid AssignedByUserId,
    string? AssignedByDisplayName,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? DueAtUtc,
    string Reason,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? EndReason,
    bool IsCurrent);

public sealed record NoteStatusHistoryDto(
    Guid Id,
    NoteStatus? FromStatus,
    NoteStatus ToStatus,
    string ToStatusAr,
    Guid ChangedByUserId,
    string? ChangedByDisplayName,
    DateTimeOffset ChangedAtUtc,
    string? Reason,
    Guid? AssignmentId);

public sealed record CreateNoteRequest(
    string Title,
    string Description,
    Guid NoteTypeId,
    NoteSeverity Severity,
    NoteSourceType SourceType,
    string? SourceReference,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerDepartmentId,
    DateTimeOffset? DueAtUtc);

public sealed record UpdateNoteRequest(
    string Title,
    string Description,
    Guid NoteTypeId,
    NoteSeverity Severity,
    NoteSourceType SourceType,
    string? SourceReference,
    ClassificationLevel Classification,
    Guid? OwnerDepartmentId,
    DateTimeOffset? DueAtUtc,
    string RowVersion);

public sealed record AssignNoteRequest(
    Guid? AssignedToUserId,
    Guid? AssignedToDepartmentId,
    DateTimeOffset? DueAtUtc,
    string Reason,
    string RowVersion);

public sealed record TransitionNoteRequest(string Reason, string RowVersion);

public sealed record WorkflowActionRequest(string? Reason, string RowVersion);

public sealed record CloseNoteRequest(string Reason, string ClosureSummary, string RowVersion);

public sealed record ReopenNoteRequest(string Reason, string RowVersion);

public sealed record NoteListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public NoteStatus? Status { get; set; }
    public NoteSeverity? Severity { get; set; }
    public Guid? NoteTypeId { get; set; }
    public NoteSourceType? SourceType { get; set; }
    public ClassificationLevel? Classification { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public Guid? OwnerDepartmentId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool OverdueOnly { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
    public bool RequiresMyAction { get; set; }
}

public sealed record NoteTypeDto(
    Guid Id,
    string Code,
    string NameAr,
    string? DescriptionAr,
    string? EntryInstructionsAr,
    int SortOrder,
    bool IsActive,
    NoteSeverity DefaultSeverity,
    string DefaultSeverityAr,
    int? DefaultDueDays,
    string RowVersion);

public sealed record CreateNoteTypeRequest(
    string Code,
    string NameAr,
    string? DescriptionAr,
    string? EntryInstructionsAr,
    int SortOrder,
    bool IsActive,
    NoteSeverity DefaultSeverity,
    int? DefaultDueDays);

public sealed record UpdateNoteTypeRequest(
    string NameAr,
    string? DescriptionAr,
    string? EntryInstructionsAr,
    int SortOrder,
    NoteSeverity DefaultSeverity,
    int? DefaultDueDays,
    string RowVersion);

public sealed record NoteTypeCapabilityDto(
    bool CanView,
    bool CanCreate,
    bool CanAssign,
    bool CanProcess,
    bool CanSubmitForVerification,
    bool CanReview,
    bool CanCancel,
    bool CanReopen,
    bool CanArchive,
    bool CanRestore);

public sealed record EffectiveNoteTypeAccessDto(
    Guid NoteTypeId,
    string NoteTypeCode,
    string NoteTypeNameAr,
    bool NoteTypeIsActive,
    NoteTypeCapabilityDecisionDto View,
    NoteTypeCapabilityDecisionDto Create,
    NoteTypeCapabilityDecisionDto Assign,
    NoteTypeCapabilityDecisionDto Process,
    NoteTypeCapabilityDecisionDto SubmitForVerification,
    NoteTypeCapabilityDecisionDto Review,
    NoteTypeCapabilityDecisionDto Cancel,
    NoteTypeCapabilityDecisionDto Reopen,
    NoteTypeCapabilityDecisionDto Archive,
    NoteTypeCapabilityDecisionDto Restore);

public sealed record NoteTypeCapabilityDecisionDto(bool Allowed, string Source);

public sealed record RoleNoteTypeGrantDto(
    Guid NoteTypeId,
    string NoteTypeCode,
    string NoteTypeNameAr,
    NoteTypeCapabilityDto Capabilities,
    string? RowVersion);

public sealed record ReplaceRoleNoteTypeGrantsRequest(
    IReadOnlyList<ReplaceRoleNoteTypeGrantItem> Grants,
    string Reason);

public sealed record ReplaceRoleNoteTypeGrantItem(
    Guid NoteTypeId,
    bool CanView,
    bool CanCreate,
    bool CanAssign,
    bool CanProcess,
    bool CanSubmitForVerification,
    bool CanReview,
    bool CanCancel,
    bool CanReopen,
    bool CanArchive,
    bool CanRestore);

public sealed record UserNoteTypeOverrideDto(
    Guid NoteTypeId,
    string NoteTypeCode,
    string NoteTypeNameAr,
    bool? CanViewOverride,
    bool? CanCreateOverride,
    bool? CanAssignOverride,
    bool? CanProcessOverride,
    bool? CanSubmitForVerificationOverride,
    bool? CanReviewOverride,
    bool? CanCancelOverride,
    bool? CanReopenOverride,
    bool? CanArchiveOverride,
    bool? CanRestoreOverride,
    string? Reason,
    string? RowVersion);

public sealed record ReplaceUserNoteTypeOverridesRequest(
    IReadOnlyList<ReplaceUserNoteTypeOverrideItem> Overrides,
    string Reason);

public sealed record ReplaceUserNoteTypeOverrideItem(
    Guid NoteTypeId,
    bool? CanViewOverride,
    bool? CanCreateOverride,
    bool? CanAssignOverride,
    bool? CanProcessOverride,
    bool? CanSubmitForVerificationOverride,
    bool? CanReviewOverride,
    bool? CanCancelOverride,
    bool? CanReopenOverride,
    bool? CanArchiveOverride,
    bool? CanRestoreOverride);

public sealed record UserNoteIntakeProfileDto(
    Guid? Id,
    Guid UserId,
    NoteIntakeLockType LockType,
    Guid? RegionId,
    string? RegionNameAr,
    Guid? FacilityId,
    string? FacilityNameAr,
    bool IsValid,
    string? InvalidReason,
    string? RowVersion);

public sealed record UpdateUserNoteIntakeProfileRequest(
    NoteIntakeLockType LockType,
    Guid? RegionId,
    Guid? FacilityId,
    string Reason,
    string? RowVersion);

public sealed record NoteIntakeContextDto(
    NoteIntakeLockType LockType,
    Guid? LockedRegionId,
    string? LockedRegionNameAr,
    Guid? LockedFacilityId,
    string? LockedFacilityNameAr,
    IReadOnlyList<NoteIntakeRegionDto> Regions,
    IReadOnlyList<NoteTypeDto> CreatableNoteTypes);

public sealed record NoteIntakeRegionDto(Guid Id, string NameAr);
public sealed record NoteIntakeFacilityDto(Guid Id, Guid RegionId, string NameAr);
public sealed record EligibleUserDto(Guid Id, string DisplayNameAr, string UserName);
public sealed record NoteTypeTabDto(Guid? NoteTypeId, string Code, string NameAr, string? DescriptionAr, bool IsActive, int Count);
