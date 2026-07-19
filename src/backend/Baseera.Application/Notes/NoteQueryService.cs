namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Attachments;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;

public interface INoteQueryService
{
    Task<PagedResult<NoteListItemDto>> ListAsync(NoteListQuery query, CancellationToken cancellationToken = default);
    Task<NoteDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteStatusHistoryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteAssignmentDto>> GetAssignmentsAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class NoteQueryService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    IAuditService audit) : INoteQueryService
{
    private static readonly HashSet<string> SortAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAtUtc", "dueAtUtc", "severity", "status", "referenceNumber", "title"
    };

    public async Task<PagedResult<NoteListItemDto>> ListAsync(NoteListQuery query, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        var canSensitive = NoteAccessHelper.CanViewSensitive(currentUser);
        var now = DateTimeOffset.UtcNow;

        // Sensitive notes stay in the list (so users know they exist within their scope) but are
        // redacted below when the caller lacks Notes.ViewSensitive — never excluded outright.
        var q = noteScope.FilterQueryable(db.OperationalNotes);
        q = ApplyFilters(q, query, now);

        var total = q.Count();
        q = ApplySort(q, query.SortBy, query.SortDesc);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = q.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var noteIds = rows.Select(r => r.Id).ToList();
        var currentAssignments = db.NoteAssignments
            .Where(a => noteIds.Contains(a.OperationalNoteId) && a.IsCurrent)
            .ToList();
        var userIds = currentAssignments.Where(a => a.AssignedToUserId.HasValue).Select(a => a.AssignedToUserId!.Value).ToHashSet();
        var users = db.Users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u.DisplayNameAr);
        var deptIds = currentAssignments.Where(a => a.AssignedToDepartmentId.HasValue).Select(a => a.AssignedToDepartmentId!.Value).ToHashSet();
        var depts = db.Departments.Where(d => deptIds.Contains(d.Id)).ToDictionary(d => d.Id, d => d.NameAr);

        var items = rows.Select(n =>
        {
            var redact = NoteAccessHelper.RequiresSensitive(n.Classification) && !canSensitive;
            var assignment = currentAssignments.FirstOrDefault(a => a.OperationalNoteId == n.Id);
            string? assignee = null;
            if (assignment?.AssignedToUserId is Guid uid && users.TryGetValue(uid, out var uname))
            {
                assignee = uname;
            }
            else if (assignment?.AssignedToDepartmentId is Guid did && depts.TryGetValue(did, out var dname))
            {
                assignee = dname;
            }

            return new NoteListItemDto(
                n.Id,
                n.ReferenceNumber,
                redact ? NoteAccessHelper.RedactedTitle : n.Title,
                redact ? null : Truncate(n.Description, 160),
                n.Status,
                NoteDisplay.StatusAr(n.Status),
                n.Severity,
                NoteDisplay.SeverityAr(n.Severity),
                n.Category,
                NoteDisplay.CategoryAr(n.Category),
                n.Classification,
                n.ScopeType,
                n.RegionId,
                n.FacilityId,
                n.FacilityUnitId,
                n.DueAtUtc,
                NoteStateMachine.IsOverdue(n.Status, n.DueAtUtc, now),
                assignee,
                n.CreatedAtUtc,
                Convert.ToBase64String(n.RowVersion),
                redact);
        }).ToList();

        return await Task.FromResult(new PagedResult<NoteListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<NoteDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        var note = db.OperationalNotes.FirstOrDefault(n => n.Id == id);
        if (note is null || !noteScope.CanAccess(note))
        {
            return null;
        }

        var canSensitive = NoteAccessHelper.CanViewSensitive(currentUser);
        var redact = NoteAccessHelper.RequiresSensitive(note.Classification) && !canSensitive;
        if (NoteAccessHelper.RequiresSensitive(note.Classification) && canSensitive)
        {
            await audit.WriteAsync(new AuditEntry
            {
                Action = "NoteSensitiveViewed",
                Module = "Notes",
                EntityType = nameof(OperationalNote),
                EntityId = note.Id.ToString(),
                IsSensitiveView = true,
                NewValues = new { note.ReferenceNumber, note.Classification }
            }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var current = MapAssignment(db.NoteAssignments.FirstOrDefault(a => a.OperationalNoteId == id && a.IsCurrent));
        var reporter = db.Users.FirstOrDefault(u => u.Id == note.ReportedByUserId)?.DisplayNameAr;
        var now = DateTimeOffset.UtcNow;

        return new NoteDetailDto(
            note.Id,
            note.ReferenceNumber,
            redact ? NoteAccessHelper.RedactedTitle : note.Title,
            redact ? NoteAccessHelper.RedactedDescription : note.Description,
            note.Status,
            NoteDisplay.StatusAr(note.Status),
            note.Severity,
            NoteDisplay.SeverityAr(note.Severity),
            note.Category,
            NoteDisplay.CategoryAr(note.Category),
            note.SourceType,
            NoteDisplay.SourceAr(note.SourceType),
            note.SourceReference,
            note.Classification,
            note.ScopeType,
            note.RegionId,
            note.FacilityId,
            note.FacilityUnitId,
            note.OwnerDepartmentId,
            note.ReportedByUserId,
            reporter,
            note.ReportedAtUtc,
            note.DueAtUtc,
            NoteStateMachine.IsOverdue(note.Status, note.DueAtUtc, now),
            note.SubmittedAtUtc,
            note.WorkStartedAtUtc,
            note.SubmittedForVerificationAtUtc,
            note.ClosedAtUtc,
            note.ClosedByUserId,
            redact ? null : note.ClosureSummary,
            note.ReopenedAtUtc,
            redact ? null : note.ReopenReason,
            current,
            note.CreatedAtUtc,
            Convert.ToBase64String(note.RowVersion),
            redact);
    }

    public Task<IReadOnlyList<NoteStatusHistoryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        _ = NoteAccessHelper.LoadInScopeOrNotFound(db, noteScope, id);

        var rows = db.NoteStatusHistories
            .Where(h => h.OperationalNoteId == id)
            .OrderBy(h => h.ChangedAtUtc)
            .ToList();
        var userIds = rows.Select(r => r.ChangedByUserId).ToHashSet();
        var users = db.Users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => u.DisplayNameAr);

        IReadOnlyList<NoteStatusHistoryDto> result = rows.Select(h => new NoteStatusHistoryDto(
            h.Id,
            h.FromStatus,
            h.ToStatus,
            NoteDisplay.StatusAr(h.ToStatus),
            h.ChangedByUserId,
            users.GetValueOrDefault(h.ChangedByUserId),
            h.ChangedAtUtc,
            h.Reason,
            h.AssignmentId)).ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<NoteAssignmentDto>> GetAssignmentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        _ = NoteAccessHelper.LoadInScopeOrNotFound(db, noteScope, id);

        var rows = db.NoteAssignments
            .Where(a => a.OperationalNoteId == id)
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToList();

        IReadOnlyList<NoteAssignmentDto> result = rows.Select(MapAssignment).Where(a => a is not null).Cast<NoteAssignmentDto>().ToList();
        return Task.FromResult(result);
    }

    private NoteAssignmentDto? MapAssignment(NoteAssignment? a)
    {
        if (a is null)
        {
            return null;
        }

        string? userName = a.AssignedToUserId is Guid uid
            ? db.Users.FirstOrDefault(u => u.Id == uid)?.DisplayNameAr
            : null;
        string? deptName = a.AssignedToDepartmentId is Guid did
            ? db.Departments.FirstOrDefault(d => d.Id == did)?.NameAr
            : null;
        string? byName = db.Users.FirstOrDefault(u => u.Id == a.AssignedByUserId)?.DisplayNameAr;

        return new NoteAssignmentDto(
            a.Id,
            a.OperationalNoteId,
            a.AssignedToUserId,
            userName,
            a.AssignedToDepartmentId,
            deptName,
            a.AssignedByUserId,
            byName,
            a.AssignedAtUtc,
            a.DueAtUtc,
            a.Reason,
            a.AcceptedAtUtc,
            a.CompletedAtUtc,
            a.EndedAtUtc,
            a.EndReason,
            a.IsCurrent);
    }

    private static IQueryable<OperationalNote> ApplyFilters(IQueryable<OperationalNote> q, NoteListQuery query, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(n => n.ReferenceNumber.Contains(term) || n.Title.Contains(term));
        }

        if (query.Status.HasValue)
        {
            q = q.Where(n => n.Status == query.Status.Value);
        }

        if (query.Severity.HasValue)
        {
            q = q.Where(n => n.Severity == query.Severity.Value);
        }

        if (query.Category.HasValue)
        {
            q = q.Where(n => n.Category == query.Category.Value);
        }

        if (query.SourceType.HasValue)
        {
            q = q.Where(n => n.SourceType == query.SourceType.Value);
        }

        if (query.Classification.HasValue)
        {
            q = q.Where(n => n.Classification == query.Classification.Value);
        }

        if (query.RegionId.HasValue)
        {
            q = q.Where(n => n.RegionId == query.RegionId.Value);
        }

        if (query.FacilityId.HasValue)
        {
            q = q.Where(n => n.FacilityId == query.FacilityId.Value);
        }

        if (query.FacilityUnitId.HasValue)
        {
            q = q.Where(n => n.FacilityUnitId == query.FacilityUnitId.Value);
        }

        if (query.OwnerDepartmentId.HasValue)
        {
            q = q.Where(n => n.OwnerDepartmentId == query.OwnerDepartmentId.Value);
        }

        if (query.DueFrom.HasValue)
        {
            q = q.Where(n => n.DueAtUtc >= query.DueFrom.Value);
        }

        if (query.DueTo.HasValue)
        {
            q = q.Where(n => n.DueAtUtc <= query.DueTo.Value);
        }

        if (query.CreatedFrom.HasValue)
        {
            q = q.Where(n => n.CreatedAtUtc >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            q = q.Where(n => n.CreatedAtUtc <= query.CreatedTo.Value);
        }

        if (query.OverdueOnly)
        {
            q = q.Where(n =>
                n.DueAtUtc.HasValue &&
                n.DueAtUtc < now &&
                n.Status != NoteStatus.Closed &&
                n.Status != NoteStatus.Cancelled);
        }

        if (query.AssignedToUserId.HasValue)
        {
            var uid = query.AssignedToUserId.Value;
            q = q.Where(n => n.Assignments.Any(a => a.IsCurrent && a.AssignedToUserId == uid));
        }

        return q;
    }

    private static IQueryable<OperationalNote> ApplySort(IQueryable<OperationalNote> q, string? sortBy, bool sortDesc)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) || !SortAllowlist.Contains(sortBy)
            ? "createdAtUtc"
            : sortBy;

        return (key.ToLowerInvariant(), sortDesc) switch
        {
            ("dueatutc", true) => q.OrderByDescending(n => n.DueAtUtc).ThenByDescending(n => n.CreatedAtUtc),
            ("dueatutc", false) => q.OrderBy(n => n.DueAtUtc).ThenBy(n => n.CreatedAtUtc),
            ("severity", true) => q.OrderByDescending(n => n.Severity).ThenByDescending(n => n.CreatedAtUtc),
            ("severity", false) => q.OrderBy(n => n.Severity).ThenBy(n => n.CreatedAtUtc),
            ("status", true) => q.OrderByDescending(n => n.Status).ThenByDescending(n => n.CreatedAtUtc),
            ("status", false) => q.OrderBy(n => n.Status).ThenBy(n => n.CreatedAtUtc),
            ("referencenumber", true) => q.OrderByDescending(n => n.ReferenceNumber),
            ("referencenumber", false) => q.OrderBy(n => n.ReferenceNumber),
            ("title", true) => q.OrderByDescending(n => n.Title),
            ("title", false) => q.OrderBy(n => n.Title),
            (_, true) => q.OrderByDescending(n => n.CreatedAtUtc),
            _ => q.OrderBy(n => n.CreatedAtUtc)
        };
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
