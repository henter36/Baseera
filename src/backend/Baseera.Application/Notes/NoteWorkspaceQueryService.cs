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
    IAttachmentAppService attachments,
    INoteWorkspaceEnrichmentService enrichmentService) : INoteWorkspaceQueryService
{
    // Bounds the combined status-history/corrective-action timeline returned per note to a fixed
    // preview window (docs/ux-rescue/phase1a-observation-performance.md) — never "full audit" history.
    private const int TimelinePreviewLimit = 30;

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
        var history = await LoadRecentHistoryAsync(id, cancellationToken);
        var actionPage = await correctiveActions.ListForNoteAsync(
            id,
            new CorrectiveActionListQuery
            {
                Page = 1,
                PageSize = TimelinePreviewLimit,
                SortBy = "createdAtUtc",
                SortDesc = true
            },
            cancellationToken);
        var attachmentRows = await attachments.ListForEntityAsync(nameof(OperationalNote), id, cancellationToken);
        var timeline = BuildTimeline(note, history, actionPage.Items);
        var openActions = await CountOpenCorrectiveActionsAsync(id, cancellationToken);
        var enrichment = await enrichmentService.BuildAsync(note, cancellationToken);

        var allowedActions = BuildAllowedActions(note);
        var actionCenter = BuildActionCenter(note, allowedActions, enrichment.PendingDecision, enrichment.PartsProgress, openActions, currentUser);

        return new NoteWorkspaceDetailDto(
            note,
            allowedActions,
            new NoteWorkspaceSummaryDto(
                openActions,
                attachmentRows.Count,
                note.Status == NoteStatus.InProgress && openActions > 0,
                note.Status == NoteStatus.PendingVerification,
                enrichment.PendingDecision is not null,
                false,
                ResolveProgress(note.Status, openActions),
                ResolveBlocker(note, openActions),
                timeline.Count > 0 ? timeline.Max(entry => entry.OccurredAtUtc) : note.CreatedAtUtc),
            assignments,
            actionPage,
            attachmentRows,
            timeline,
            enrichment.Decisions,
            enrichment.PartsItems,
            enrichment.SlaState,
            actionCenter);
    }

    private async Task<int> CountOpenCorrectiveActionsAsync(Guid id, CancellationToken cancellationToken) =>
        await db.CorrectiveActions.CountAsync(
            action =>
                action.OperationalNoteId == id &&
                action.Status != CorrectiveActionStatus.Completed &&
                action.Status != CorrectiveActionStatus.Cancelled,
            cancellationToken);

    private async Task<IReadOnlyList<NoteStatusHistoryDto>> LoadRecentHistoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await db.NoteStatusHistories
            .Where(history => history.OperationalNoteId == id)
            .OrderByDescending(history => history.ChangedAtUtc)
            .Take(TimelinePreviewLimit)
            .ToListAsync(cancellationToken);
        var userIds = rows.Select(row => row.ChangedByUserId).ToHashSet();
        var users = await db.Users
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayNameAr, cancellationToken);

        return rows.Select(history => new NoteStatusHistoryDto(
            history.Id,
            history.FromStatus,
            history.ToStatus,
            NoteDisplay.StatusAr(history.ToStatus),
            history.ChangedByUserId,
            users.GetValueOrDefault(history.ChangedByUserId),
            history.ChangedAtUtc,
            history.Reason,
            history.AssignmentId)).ToList();
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
            .Take(TimelinePreviewLimit)
            .ToList();
    }

    private IReadOnlyList<string> BuildAllowedActions(NoteDetailDto note) => ComputeAllowedActions(note, currentUser);

    /// <summary>
    /// Pure allowed-actions computation (permission + lifecycle status only, no I/O) — extracted as a
    /// public static method so it is directly unit-testable against a fake <see cref="ICurrentUser"/>
    /// without constructing the full service graph. Behavior must stay identical to the instance caller.
    /// </summary>
    public static IReadOnlyList<string> ComputeAllowedActions(NoteDetailDto note, ICurrentUser currentUser)
    {
        var allowed = new List<string>();
        AddCoreLifecycleActions(allowed, note, currentUser);
        AddTriageGateActions(allowed, note, currentUser);
        AddTreatmentActions(allowed, note, currentUser);
        AddPartsAndSlaActions(allowed, note, currentUser);
        return allowed;
    }

    private static void AddCoreLifecycleActions(List<string> allowed, NoteDetailDto note, ICurrentUser currentUser)
    {
        AddIf(allowed, "SUBMIT", currentUser.HasPermission(PermissionCodes.NotesUpdate) && NoteStateMachine.CanTransition(note.Status, NoteStatus.Open));
        AddIf(allowed, "ASSIGN", currentUser.HasPermission(PermissionCodes.NotesAssign) && (note.Status is NoteStatus.Open or NoteStatus.Assigned or NoteStatus.Reopened));
        AddIf(allowed, "REASSIGN", currentUser.HasPermission(PermissionCodes.NotesAssign) && note.CurrentAssignment is not null && !NoteStateMachine.IsTerminalLocked(note.Status));
        AddIf(allowed, "START_WORK", currentUser.HasPermission(PermissionCodes.NotesStartWork) && NoteStateMachine.CanTransition(note.Status, NoteStatus.InProgress));
        AddIf(allowed, "ADD_ACTION", currentUser.HasPermission(PermissionCodes.CorrectiveActionsCreate) && !NoteStateMachine.IsTerminalLocked(note.Status));
        AddIf(allowed, "REQUEST_VERIFICATION", currentUser.HasPermission(PermissionCodes.NotesSubmitForVerification) && NoteStateMachine.CanTransition(note.Status, NoteStatus.PendingVerification));
        AddIf(allowed, "REJECT_VERIFICATION", currentUser.HasPermission(PermissionCodes.NotesReturnForRework) && NoteStateMachine.CanTransition(note.Status, NoteStatus.InProgress));
        // Deliberately Status==PendingVerification rather than CanTransition(status, Closed): the latter
        // is now also true for the Phase 1B decision-approval closures (Invalid/Duplicate/NoAction),
        // which close via NoteDecisionApprovalService/Notes.Approve* — never via VERIFY_CLOSURE.
        AddIf(allowed, "VERIFY_CLOSURE", currentUser.HasPermission(PermissionCodes.NotesVerifyClosure) && note.Status == NoteStatus.PendingVerification);
        AddIf(allowed, "REOPEN", currentUser.HasPermission(PermissionCodes.NotesReopen) && NoteStateMachine.CanTransition(note.Status, NoteStatus.Reopened));
        AddIf(allowed, "CANCEL", currentUser.HasPermission(PermissionCodes.NotesCancel) && !NoteStateMachine.IsTerminalLocked(note.Status));
    }

    // --- Phase 1B: triage gate / treatment result / parts / SLA — purely additive tokens,
    // computed from NoteDetailDto fields only (no I/O), never gating the pre-existing tokens
    // above so pre-Phase-1B notes/tests keep their exact original AllowedActions behavior.
    private static void AddTriageGateActions(List<string> allowed, NoteDetailDto note, ICurrentUser currentUser)
    {
        var atTriageGate = note.Status == NoteStatus.Open && note.TriageOutcome is null;
        AddIf(allowed, "TRIAGE_VALID", currentUser.HasPermission(PermissionCodes.NotesUpdate) && atTriageGate);
        AddIf(allowed, "TRIAGE_PROPOSE_INVALID", currentUser.HasPermission(PermissionCodes.NotesProposeInvalid) && atTriageGate);
        AddIf(allowed, "TRIAGE_PROPOSE_DUPLICATE", currentUser.HasPermission(PermissionCodes.NotesProposeDuplicate) && atTriageGate);
    }

    private static void AddTreatmentActions(List<string> allowed, NoteDetailDto note, ICurrentUser currentUser)
    {
        var validAndOpenForTreatment = note.TriageOutcome == NoteTriageOutcome.Valid && !NoteStateMachine.IsTerminalLocked(note.Status);
        AddIf(allowed, "RECORD_TREATMENT", currentUser.HasPermission(PermissionCodes.NotesStartWork) && validAndOpenForTreatment);
        AddIf(allowed, "PROPOSE_NO_ACTION", currentUser.HasPermission(PermissionCodes.NotesProposeNoAction) && validAndOpenForTreatment);
    }

    private static void AddPartsAndSlaActions(List<string> allowed, NoteDetailDto note, ICurrentUser currentUser)
    {
        var requiresParts = note.TreatmentExecutionType == NoteTreatmentExecutionType.RequiresParts && !NoteStateMachine.IsTerminalLocked(note.Status);
        AddIf(allowed, "MANAGE_PARTS", currentUser.HasPermission(PermissionCodes.NotesStartWork) && requiresParts);
        AddIf(allowed, "REQUEST_SLA_PAUSE", currentUser.HasPermission(PermissionCodes.NotesStartWork) && requiresParts);
        AddIf(allowed, "APPROVE_SLA_PAUSE", currentUser.HasPermission(PermissionCodes.NotesApproveSlaPause) && requiresParts);
    }

    private static readonly string[] ActionPriority =
    [
        "TRIAGE_VALID", "TRIAGE_PROPOSE_INVALID", "TRIAGE_PROPOSE_DUPLICATE",
        "ASSIGN", "REASSIGN", "START_WORK", "RECORD_TREATMENT", "MANAGE_PARTS",
        "REQUEST_VERIFICATION", "APPROVE_SLA_PAUSE", "REQUEST_SLA_PAUSE",
        "VERIFY_CLOSURE", "REJECT_VERIFICATION", "PROPOSE_NO_ACTION",
        "REOPEN", "CANCEL", "ADD_ACTION", "SUBMIT"
    ];

    /// <summary>
    /// Server-authored مركز الإجراءات contract (spec §مركز الإجراءات): exactly one primary action,
    /// up to three secondary actions, plus the state hints the UI needs to explain why an action is
    /// blocked without re-deriving policy client-side.
    /// </summary>
    private static NoteActionCenterDto BuildActionCenter(
        NoteDetailDto note,
        IReadOnlyList<string> allowedActions,
        NoteDecisionApprovalDto? pendingDecision,
        NotePartsProgressDto? partsProgress,
        int openActions,
        ICurrentUser currentUser)
    {
        var ordered = ActionPriority.Where(allowedActions.Contains).ToList();
        var primary = ordered.FirstOrDefault();
        var secondary = ordered.Skip(1).Take(3).ToList();

        // True when the *current* user may approve the pending decision (not the proposer, and
        // holds the matching Notes.Approve* permission) — i.e. "can this viewer approve it", not
        // "is self-approval allowed" (which is never true; see NoteDecisionApprovalService).
        var canApprovePendingDecision = pendingDecision is not null
            && currentUser.UserId != pendingDecision.ProposedByUserId
            && currentUser.HasPermission(pendingDecision.DecisionType switch
            {
                NoteDecisionApprovalType.Invalid => PermissionCodes.NotesApproveInvalid,
                NoteDecisionApprovalType.Duplicate => PermissionCodes.NotesApproveDuplicate,
                NoteDecisionApprovalType.NoAction => PermissionCodes.NotesApproveNoAction,
                _ => string.Empty
            });

        var blocker = ResolveBlocker(note, openActions);
        if (pendingDecision is not null)
        {
            blocker = $"بانتظار اعتماد: {pendingDecision.DecisionTypeAr}";
        }
        else if (partsProgress is not null && !partsProgress.AllResolved)
        {
            blocker = $"بانتظار قطع ({partsProgress.Installed} من {partsProgress.Total} تم تركيبها)";
        }

        var nextAction = ResolveNextAction(note, pendingDecision, partsProgress);
        var closureReadiness = note.Status == NoteStatus.PendingVerification || pendingDecision is not null;

        return new NoteActionCenterDto(
            allowedActions,
            primary,
            secondary,
            pendingDecision?.DecisionTypeAr,
            pendingDecision is not null,
            canApprovePendingDecision,
            blocker,
            nextAction,
            note.ClosureReason?.ToString() ?? "Open",
            partsProgress,
            closureReadiness);
    }

    private static string? ResolveNextAction(
        NoteDetailDto note,
        NoteDecisionApprovalDto? pendingDecision,
        NotePartsProgressDto? partsProgress)
    {
        if (NoteStateMachine.IsTerminalLocked(note.Status))
        {
            return null;
        }

        if (pendingDecision is not null)
        {
            return $"اعتماد {pendingDecision.DecisionTypeAr}";
        }

        if (note.Status == NoteStatus.Open && note.TriageOutcome is null)
        {
            return "فرز الملاحظة";
        }

        if (note.TriageOutcome == NoteTriageOutcome.Valid && note.CurrentAssignment is null)
        {
            return "تكليف الملاحظة";
        }

        if (note.Status == NoteStatus.Assigned)
        {
            return "بدء المعالجة";
        }

        if (note.Status == NoteStatus.InProgress && note.TreatmentResultType is null)
        {
            return "تسجيل نتيجة المعالجة";
        }

        if (partsProgress is not null && !partsProgress.AllResolved)
        {
            return "إكمال متطلبات القطع";
        }

        if (note.Status == NoteStatus.InProgress && note.TreatmentResultType == NoteTreatmentResultType.Treated)
        {
            return "إرسال المعالجة للتحقق";
        }

        if (note.Status == NoteStatus.PendingVerification)
        {
            return "اعتماد نتيجة المعالجة";
        }

        return null;
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

    public static int ResolveProgress(NoteStatus status, int openActions) => status switch
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

    public static string? ResolveBlocker(NoteDetailDto note, int openActions)
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
