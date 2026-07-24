namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Common;
using Baseera.Application.CorrectiveActions;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteWorkspaceQueryService
{
    Task<NoteWorkspaceListDto> ListAsync(NoteListQuery query, CancellationToken cancellationToken = default);
    Task<NoteWorkspaceDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class NoteWorkspaceQueryService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteQueryService notes,
    ICorrectiveActionQueryService correctiveActions,
    IAttachmentAppService attachments) : INoteWorkspaceQueryService
{
    public async Task<NoteWorkspaceListDto> ListAsync(NoteListQuery query, CancellationToken cancellationToken = default)
    {
        query.PageSize = Math.Clamp(query.PageSize, 1, 50);
        return new NoteWorkspaceListDto(await notes.ListAsync(query, cancellationToken));
    }

    public async Task<NoteWorkspaceDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await notes.GetDetailAsync(id, cancellationToken);
        if (note is null)
        {
            return null;
        }

        var assignments = await notes.GetAssignmentsAsync(id, cancellationToken);
        var history = await notes.GetHistoryAsync(id, cancellationToken);
        var actionPage = await correctiveActions.ListForNoteAsync(
            id,
            new CorrectiveActionListQuery
            {
                Page = 1,
                PageSize = 10,
                SortBy = "createdAtUtc",
                SortDesc = true
            },
            cancellationToken);
        var attachmentRows = await attachments.ListForEntityAsync(nameof(OperationalNote), id, cancellationToken);
        var timeline = BuildTimeline(note, history, actionPage.Items);
        var openActions = await db.CorrectiveActions.CountAsync(
            action =>
                action.OperationalNoteId == id &&
                action.Status != CorrectiveActionStatus.Completed &&
                action.Status != CorrectiveActionStatus.Cancelled,
            cancellationToken);

        return new NoteWorkspaceDetailDto(
            note,
            BuildAllowedActions(note),
            new NoteWorkspaceSummaryDto(
                openActions,
                attachmentRows.Count,
                note.Status == NoteStatus.InProgress && openActions > 0,
                note.Status == NoteStatus.PendingVerification,
                false,
                false,
                ResolveProgress(note.Status, openActions),
                ResolveBlocker(note, openActions),
                timeline.Count > 0 ? timeline.Max(entry => entry.OccurredAtUtc) : note.CreatedAtUtc),
            assignments,
            actionPage,
            attachmentRows,
            Array.Empty<NoteWorkspaceResourceDto>(),
            Array.Empty<NoteWorkspaceDecisionDto>(),
            Array.Empty<NoteWorkspaceLinkDto>(),
            timeline);
    }

    private static IReadOnlyList<NoteWorkspaceTimelineEntryDto> BuildTimeline(
        NoteDetailDto note,
        IReadOnlyList<NoteStatusHistoryDto> history,
        IReadOnlyList<CorrectiveActionListItemDto> actionItems)
    {
        var entries = history.Select(item => new NoteWorkspaceTimelineEntryDto(
            item.Id,
            "STATUS",
            $"تغيير الحالة إلى {item.ToStatusAr}",
            item.Reason,
            item.ChangedByDisplayName,
            item.ChangedAtUtc,
            TimelineToneForStatus(item.ToStatus))).ToList();

        entries.Add(new NoteWorkspaceTimelineEntryDto(
            note.Id,
            "CREATED",
            "إنشاء الملاحظة",
            note.SourceReference,
            note.ReportedByDisplayName,
            note.ReportedAtUtc,
            "info"));

        entries.AddRange(actionItems.Select(action => new NoteWorkspaceTimelineEntryDto(
            action.Id,
            "CORRECTIVE_ACTION",
            $"إجراء تصحيحي: {action.Title}",
            action.StatusAr,
            null,
            action.CreatedAtUtc,
            action.IsOverdue ? "danger" : "info")));

        return entries
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ToList();
    }

    private IReadOnlyList<string> BuildAllowedActions(NoteDetailDto note)
    {
        var allowed = new List<string>();
        AddIf(allowed, "SUBMIT", currentUser.HasPermission(PermissionCodes.NotesUpdate) && NoteStateMachine.CanTransition(note.Status, NoteStatus.Open));
        AddIf(allowed, "ASSIGN", currentUser.HasPermission(PermissionCodes.NotesAssign) && (note.Status is NoteStatus.Open or NoteStatus.Assigned or NoteStatus.Reopened));
        AddIf(allowed, "REASSIGN", currentUser.HasPermission(PermissionCodes.NotesAssign) && note.CurrentAssignment is not null && !NoteStateMachine.IsTerminalLocked(note.Status));
        AddIf(allowed, "START_WORK", currentUser.HasPermission(PermissionCodes.NotesStartWork) && NoteStateMachine.CanTransition(note.Status, NoteStatus.InProgress));
        AddIf(allowed, "ADD_ACTION", currentUser.HasPermission(PermissionCodes.CorrectiveActionsCreate) && !NoteStateMachine.IsTerminalLocked(note.Status));
        AddIf(allowed, "REQUEST_VERIFICATION", currentUser.HasPermission(PermissionCodes.NotesSubmitForVerification) && NoteStateMachine.CanTransition(note.Status, NoteStatus.PendingVerification));
        AddIf(allowed, "REJECT_VERIFICATION", currentUser.HasPermission(PermissionCodes.NotesReturnForRework) && NoteStateMachine.CanTransition(note.Status, NoteStatus.InProgress));
        AddIf(allowed, "REOPEN", currentUser.HasPermission(PermissionCodes.NotesReopen) && NoteStateMachine.CanTransition(note.Status, NoteStatus.Reopened));
        AddIf(allowed, "CANCEL", currentUser.HasPermission(PermissionCodes.NotesCancel) && !NoteStateMachine.IsTerminalLocked(note.Status));
        return allowed;
    }

    private static string TimelineToneForStatus(NoteStatus status)
    {
        if (status == NoteStatus.Closed)
        {
            return "ok";
        }

        if (status == NoteStatus.Cancelled)
        {
            return "danger";
        }

        return "muted";
    }

    private static void AddIf(List<string> actions, string action, bool condition)
    {
        if (condition)
        {
            actions.Add(action);
        }
    }

    private static int ResolveProgress(NoteStatus status, int openActions) => status switch
    {
        NoteStatus.Draft => 5,
        NoteStatus.Open => 15,
        NoteStatus.Assigned => 30,
        NoteStatus.InProgress => ResolveInProgressProgress(openActions),
        NoteStatus.PendingVerification => 82,
        NoteStatus.Closed => 100,
        NoteStatus.Reopened => 40,
        NoteStatus.Cancelled => 0,
        _ => 0
    };

    private static string? ResolveBlocker(NoteDetailDto note, int openActions)
    {
        if (note.IsOverdue)
        {
            return "متجاوزة للموعد";
        }

        if (note.Status == NoteStatus.PendingVerification)
        {
            return "بانتظار التحقق";
        }

        if (note.Status == NoteStatus.InProgress && openActions > 0)
        {
            return "بانتظار إكمال الإجراءات المفتوحة";
        }

        return null;
    }

    private static int ResolveInProgressProgress(int openActions)
    {
        return openActions > 0 ? 55 : 65;
    }
}
