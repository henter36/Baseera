namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Notes;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface ICorrectiveActionQueryService
{
    Task<PagedResult<CorrectiveActionListItemDto>> ListAsync(CorrectiveActionListQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<CorrectiveActionListItemDto>> ListForNoteAsync(Guid noteId, CorrectiveActionListQuery query, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CorrectiveActionStatusHistoryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CorrectiveActionAssignmentDto>> GetAssignmentsAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class CorrectiveActionQueryService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    ICorrectiveActionScopeService scope,
    INoteScopeService noteScope,
    IAuditService audit) : ICorrectiveActionQueryService
{
    private static readonly HashSet<string> SortAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAtUtc", "dueAtUtc", "priority", "status", "referenceNumber", "title"
    };

    public async Task<PagedResult<CorrectiveActionListItemDto>> ListAsync(
        CorrectiveActionListQuery query,
        CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsView);
        var q = await scope.FilterQueryableAsync(db.CorrectiveActions, cancellationToken);
        return await ListScopedAsync(q.AsNoTracking(), query, cancellationToken);
    }

    public async Task<PagedResult<CorrectiveActionListItemDto>> ListForNoteAsync(
        Guid noteId,
        CorrectiveActionListQuery query,
        CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsView);
        var note = await db.OperationalNotes.FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken);
        if (note is null || !noteScope.CanAccess(note))
        {
            throw new KeyNotFoundException("الملاحظة غير موجودة.");
        }

        var q = await scope.FilterQueryableAsync(db.CorrectiveActions.Where(a => a.OperationalNoteId == noteId), cancellationToken);
        query.NoteId = noteId;
        return await ListScopedAsync(q.AsNoTracking(), query, cancellationToken);
    }

    public async Task<CorrectiveActionDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsView);
        var action = await db.CorrectiveActions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (action is null || !await scope.CanAccessAsync(action, cancellationToken))
        {
            return null;
        }

        var canSensitive = CorrectiveActionAccessHelper.CanViewSensitive(currentUser);
        var redact = CorrectiveActionAccessHelper.RequiresSensitive(action.Classification) && !canSensitive;
        if (CorrectiveActionAccessHelper.RequiresSensitive(action.Classification) && canSensitive)
        {
            await audit.WriteAsync(new AuditEntry
            {
                Action = "CorrectiveActionSensitiveViewed",
                Module = CorrectiveActionAccessHelper.ModuleName,
                EntityType = nameof(CorrectiveAction),
                EntityId = action.Id.ToString(),
                IsSensitiveView = true,
                NewValues = new { action.ReferenceNumber, action.OperationalNoteId, action.Classification }
            }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var current = await db.CorrectiveActionAssignments
            .Include(a => a.AssignedToUser)
            .Include(a => a.AssignedToDepartment)
            .Include(a => a.AssignedByUser)
            .FirstOrDefaultAsync(a => a.CorrectiveActionId == id && a.IsCurrent, cancellationToken);
        var creator = (await db.Users.FirstOrDefaultAsync(u => u.Id == action.CreatedByUserId, cancellationToken))?.DisplayNameAr;
        var noteRef = (await db.OperationalNotes.FirstOrDefaultAsync(n => n.Id == action.OperationalNoteId, cancellationToken))?.ReferenceNumber;
        var now = DateTimeOffset.UtcNow;

        return new CorrectiveActionDetailDto(
            action.Id,
            action.ReferenceNumber,
            action.OperationalNoteId,
            noteRef,
            redact ? CorrectiveActionAccessHelper.RedactedTitle : action.Title,
            redact ? CorrectiveActionAccessHelper.RedactedDescription : action.Description,
            action.Priority,
            CorrectiveActionDisplay.PriorityAr(action.Priority),
            action.Status,
            CorrectiveActionDisplay.StatusAr(action.Status),
            action.Classification,
            action.OwnerDepartmentId,
            action.CreatedByUserId,
            creator,
            action.CreatedAtUtc,
            action.SubmittedAtUtc,
            action.WorkStartedAtUtc,
            action.SubmittedForVerificationAtUtc,
            action.CompletedAtUtc,
            action.CompletedByUserId,
            redact ? null : action.CompletionSummary,
            action.ReopenedAtUtc,
            redact ? null : action.ReopenReason,
            action.CancelledAtUtc,
            redact ? null : action.CancelReason,
            action.DueAtUtc,
            CorrectiveActionStateMachine.IsOverdue(action.Status, action.DueAtUtc, now),
            OverdueDays(action.Status, action.DueAtUtc, now),
            MapAssignment(current),
            Convert.ToBase64String(action.RowVersion),
            redact);
    }

    public async Task<IReadOnlyList<CorrectiveActionStatusHistoryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsView);
        _ = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, scope, id, cancellationToken: cancellationToken);

        var rows = await db.CorrectiveActionStatusHistories
            .Where(h => h.CorrectiveActionId == id)
            .OrderBy(h => h.ChangedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var userIds = rows.Select(r => r.ChangedByUserId).ToHashSet();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameAr, cancellationToken);
        return rows.Select(h => new CorrectiveActionStatusHistoryDto(
            h.Id,
            h.FromStatus,
            h.ToStatus,
            CorrectiveActionDisplay.StatusAr(h.ToStatus),
            h.ChangedByUserId,
            users.GetValueOrDefault(h.ChangedByUserId),
            h.ChangedAtUtc,
            h.Reason,
            h.AssignmentId,
            h.MetadataJson)).ToList();
    }

    public async Task<IReadOnlyList<CorrectiveActionAssignmentDto>> GetAssignmentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsView);
        _ = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, scope, id, cancellationToken: cancellationToken);

        var rows = await db.CorrectiveActionAssignments
            .Where(a => a.CorrectiveActionId == id)
            .Include(a => a.AssignedToUser)
            .Include(a => a.AssignedToDepartment)
            .Include(a => a.AssignedByUser)
            .OrderByDescending(a => a.AssignedAtUtc)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return rows.Select(MapAssignment).OfType<CorrectiveActionAssignmentDto>().ToList();
    }

    private async Task<PagedResult<CorrectiveActionListItemDto>> ListScopedAsync(
        IQueryable<CorrectiveAction> q,
        CorrectiveActionListQuery query,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        q = ApplyFilters(q, query, now);
        var total = await q.CountAsync(cancellationToken);
        q = ApplySort(q, query.SortBy, query.SortDesc);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = await MapListItemsAsync(rows, now, query.DueSoonDays ?? 7, cancellationToken);
        return new PagedResult<CorrectiveActionListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    private async Task<IReadOnlyList<CorrectiveActionListItemDto>> MapListItemsAsync(
        IReadOnlyList<CorrectiveAction> rows,
        DateTimeOffset now,
        int dueSoonDays,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var noteIds = rows.Select(r => r.OperationalNoteId).ToHashSet();
        var noteRefs = await db.OperationalNotes.Where(n => noteIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.ReferenceNumber, cancellationToken);
        var currentAssignments = await db.CorrectiveActionAssignments
            .Where(a => ids.Contains(a.CorrectiveActionId) && a.IsCurrent)
            .ToListAsync(cancellationToken);
        var userIds = currentAssignments.Where(a => a.AssignedToUserId.HasValue).Select(a => a.AssignedToUserId!.Value).ToHashSet();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameAr, cancellationToken);
        var deptIds = currentAssignments.Where(a => a.AssignedToDepartmentId.HasValue).Select(a => a.AssignedToDepartmentId!.Value).ToHashSet();
        var depts = await db.Departments.Where(d => deptIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.NameAr, cancellationToken);
        var canSensitive = CorrectiveActionAccessHelper.CanViewSensitive(currentUser);

        return rows.Select(a =>
        {
            var redact = CorrectiveActionAccessHelper.RequiresSensitive(a.Classification) && !canSensitive;
            var assignment = currentAssignments.FirstOrDefault(x => x.CorrectiveActionId == a.Id);
            var assignee = ResolveAssignee(assignment, users, depts);
            return new CorrectiveActionListItemDto(
                a.Id,
                a.ReferenceNumber,
                a.OperationalNoteId,
                noteRefs.GetValueOrDefault(a.OperationalNoteId),
                redact ? CorrectiveActionAccessHelper.RedactedTitle : a.Title,
                redact ? null : Truncate(a.Description, 160),
                a.Priority,
                CorrectiveActionDisplay.PriorityAr(a.Priority),
                a.Status,
                CorrectiveActionDisplay.StatusAr(a.Status),
                a.Classification,
                a.OwnerDepartmentId,
                a.DueAtUtc,
                CorrectiveActionStateMachine.IsOverdue(a.Status, a.DueAtUtc, now),
                CorrectiveActionStateMachine.IsDueSoon(a.Status, a.DueAtUtc, now, dueSoonDays),
                OverdueDays(a.Status, a.DueAtUtc, now),
                assignee,
                a.CreatedAtUtc,
                Convert.ToBase64String(a.RowVersion),
                redact);
        }).ToList();
    }

    private static IQueryable<CorrectiveAction> ApplyFilters(IQueryable<CorrectiveAction> q, CorrectiveActionListQuery query, DateTimeOffset now)
    {
        q = ApplyIdentityFilters(q, query);
        q = ApplyAssignmentAndScopeFilters(q, query);
        q = ApplyDateFilters(q, query);
        return ApplyDueStateFilters(q, query, now);
    }

    private static IQueryable<CorrectiveAction> ApplyIdentityFilters(IQueryable<CorrectiveAction> q, CorrectiveActionListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(a => a.ReferenceNumber.Contains(term) || a.Title.Contains(term));
        }

        if (query.NoteId.HasValue) q = q.Where(a => a.OperationalNoteId == query.NoteId.Value);
        if (query.Status.HasValue) q = q.Where(a => a.Status == query.Status.Value);
        if (query.Priority.HasValue) q = q.Where(a => a.Priority == query.Priority.Value);
        if (query.Classification.HasValue) q = q.Where(a => a.Classification == query.Classification.Value);
        if (query.OwnerDepartmentId.HasValue) q = q.Where(a => a.OwnerDepartmentId == query.OwnerDepartmentId.Value);
        return q;
    }

    private static IQueryable<CorrectiveAction> ApplyAssignmentAndScopeFilters(IQueryable<CorrectiveAction> q, CorrectiveActionListQuery query)
    {
        if (query.AssignedToUserId.HasValue)
        {
            var uid = query.AssignedToUserId.Value;
            q = q.Where(a => a.Assignments.Any(x => x.IsCurrent && x.AssignedToUserId == uid));
        }

        if (query.RegionId.HasValue) q = q.Where(a => a.OperationalNote.RegionId == query.RegionId.Value);
        if (query.FacilityId.HasValue) q = q.Where(a => a.OperationalNote.FacilityId == query.FacilityId.Value);
        if (query.FacilityUnitId.HasValue) q = q.Where(a => a.OperationalNote.FacilityUnitId == query.FacilityUnitId.Value);
        return q;
    }

    private static IQueryable<CorrectiveAction> ApplyDateFilters(IQueryable<CorrectiveAction> q, CorrectiveActionListQuery query)
    {
        if (query.DueFrom.HasValue) q = q.Where(a => a.DueAtUtc >= query.DueFrom.Value);
        if (query.DueTo.HasValue) q = q.Where(a => a.DueAtUtc <= query.DueTo.Value);
        if (query.CreatedFrom.HasValue) q = q.Where(a => a.CreatedAtUtc >= query.CreatedFrom.Value);
        if (query.CreatedTo.HasValue) q = q.Where(a => a.CreatedAtUtc <= query.CreatedTo.Value);
        return q;
    }

    private static IQueryable<CorrectiveAction> ApplyDueStateFilters(IQueryable<CorrectiveAction> q, CorrectiveActionListQuery query, DateTimeOffset now)
    {
        if (query.OverdueOnly) q = q.Where(a => a.DueAtUtc.HasValue && a.DueAtUtc < now && a.Status != CorrectiveActionStatus.Completed && a.Status != CorrectiveActionStatus.Cancelled);
        if (query.DueSoonDays.HasValue)
        {
            var dueTo = now.AddDays(Math.Max(query.DueSoonDays.Value, 0));
            q = q.Where(a => a.DueAtUtc.HasValue && a.DueAtUtc >= now && a.DueAtUtc <= dueTo && a.Status != CorrectiveActionStatus.Completed && a.Status != CorrectiveActionStatus.Cancelled);
        }

        return q;
    }

    private static IQueryable<CorrectiveAction> ApplySort(IQueryable<CorrectiveAction> q, string? sortBy, bool sortDesc)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) || !SortAllowlist.Contains(sortBy) ? "createdAtUtc" : sortBy;
        return (key.ToLowerInvariant(), sortDesc) switch
        {
            ("dueatutc", true) => q.OrderByDescending(a => a.DueAtUtc).ThenByDescending(a => a.CreatedAtUtc),
            ("dueatutc", false) => q.OrderBy(a => a.DueAtUtc).ThenBy(a => a.CreatedAtUtc),
            ("priority", true) => q.OrderByDescending(a => a.Priority).ThenByDescending(a => a.CreatedAtUtc),
            ("priority", false) => q.OrderBy(a => a.Priority).ThenBy(a => a.CreatedAtUtc),
            ("status", true) => q.OrderByDescending(a => a.Status).ThenByDescending(a => a.CreatedAtUtc),
            ("status", false) => q.OrderBy(a => a.Status).ThenBy(a => a.CreatedAtUtc),
            ("referencenumber", true) => q.OrderByDescending(a => a.ReferenceNumber),
            ("referencenumber", false) => q.OrderBy(a => a.ReferenceNumber),
            ("title", true) => q.OrderByDescending(a => a.Title),
            ("title", false) => q.OrderBy(a => a.Title),
            (_, true) => q.OrderByDescending(a => a.CreatedAtUtc),
            _ => q.OrderBy(a => a.CreatedAtUtc)
        };
    }

    public static CorrectiveActionAssignmentDto? MapAssignment(CorrectiveActionAssignment? a)
    {
        if (a is null) return null;
        return new CorrectiveActionAssignmentDto(
            a.Id,
            a.CorrectiveActionId,
            a.AssignedToUserId,
            a.AssignedToUser?.DisplayNameAr,
            a.AssignedToDepartmentId,
            a.AssignedToDepartment?.NameAr,
            a.AssignedByUserId,
            a.AssignedByUser?.DisplayNameAr,
            a.AssignedAtUtc,
            a.DueAtUtc,
            a.Reason,
            a.AcceptedAtUtc,
            a.CompletedAtUtc,
            a.EndedAtUtc,
            a.EndReason,
            a.IsCurrent);
    }

    private static string? ResolveAssignee(
        CorrectiveActionAssignment? assignment,
        IReadOnlyDictionary<Guid, string> users,
        IReadOnlyDictionary<Guid, string> depts)
    {
        if (assignment?.AssignedToUserId is Guid uid && users.TryGetValue(uid, out var uname)) return uname;
        if (assignment?.AssignedToDepartmentId is Guid did && depts.TryGetValue(did, out var dname)) return dname;
        return null;
    }

    private static int? OverdueDays(CorrectiveActionStatus status, DateTimeOffset? dueAtUtc, DateTimeOffset now) =>
        CorrectiveActionStateMachine.IsOverdue(status, dueAtUtc, now)
            ? Math.Max(0, (int)Math.Floor((now - dueAtUtc!.Value).TotalDays))
            : null;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
