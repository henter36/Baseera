namespace Baseera.Application.Notes;

using Baseera.Application.Attachments;
using Baseera.Application.Common;
using Baseera.Application.CorrectiveActions;

public sealed record NoteWorkspaceListDto(
    PagedResult<NoteListItemDto> Notes);

public sealed record NoteWorkspaceDetailDto(
    NoteDetailDto Note,
    IReadOnlyList<string> AllowedActions,
    NoteWorkspaceSummaryDto Summary,
    IReadOnlyList<NoteAssignmentDto> Assignments,
    PagedResult<CorrectiveActionListItemDto> CorrectiveActions,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<NoteWorkspaceResourceDto> Resources,
    IReadOnlyList<NoteWorkspaceDecisionDto> Decisions,
    IReadOnlyList<NoteWorkspaceLinkDto> Links,
    IReadOnlyList<NoteWorkspaceTimelineEntryDto> Timeline);

public sealed record NoteWorkspaceSummaryDto(
    int OpenCorrectiveActions,
    int AttachmentCount,
    bool WaitingResource,
    bool WaitingVerification,
    bool WaitingClosureApproval,
    bool HasEscalation,
    int ProgressPercent,
    string? CurrentBlockerAr,
    DateTimeOffset LastUpdatedAtUtc);

public sealed record NoteWorkspaceTimelineEntryDto(
    Guid Id,
    string Type,
    string TitleAr,
    string? DescriptionAr,
    string? ActorDisplayName,
    DateTimeOffset OccurredAtUtc,
    string Tone);

public sealed record NoteWorkspaceResourceDto(
    Guid Id,
    string TitleAr,
    string StatusAr,
    string? ResponsiblePartyAr,
    int? Quantity,
    DateTimeOffset? RequestedAtUtc,
    DateTimeOffset? ExpectedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    string? ImpactAr);

public sealed record NoteWorkspaceDecisionDto(
    Guid Id,
    string DecisionAr,
    string? ReasonAr,
    string? AlternativesAr,
    string? EvidenceAr,
    string? DecisionOwnerDisplayName,
    DateTimeOffset DecidedAtUtc,
    string? ExpectedOutcomeAr,
    string? ActualOutcomeAr);

public sealed record NoteWorkspaceLinkDto(
    Guid Id,
    string LinkTypeAr,
    string Reference,
    string TitleAr);
