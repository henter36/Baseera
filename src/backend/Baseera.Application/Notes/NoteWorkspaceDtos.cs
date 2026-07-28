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
