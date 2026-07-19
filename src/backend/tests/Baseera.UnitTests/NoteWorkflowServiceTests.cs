using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class NoteWorkflowServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Creates real actor/reporter User rows (required because NoteAssignment/NoteStatusHistory
    /// query filters join through their AssignedByUser/ChangedByUser navigations).
    /// </summary>
    private (INoteWorkflowService Workflow, Guid ActorId, Guid ReporterId) BuildWorkflow(params string[] permissions)
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var current = FakeUser(actor.Id, permissions);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, audit);
        return (new NoteWorkflowService(_db, current, scope, audit, queries), actor.Id, reporter.Id);
    }

    private static string RowVersionOf(OperationalNote note) => Convert.ToBase64String(note.RowVersion);

    [Fact]
    public async Task StartWork_requires_permission()
    {
        var (workflow, _, reporterId) = BuildWorkflow(/* no permissions granted */);
        var note = SeedNote(NoteStatus.Assigned, reporterId: reporterId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            workflow.StartWorkAsync(note.Id, new TransitionNoteRequest("بدء", RowVersionOf(note))));
    }

    [Fact]
    public async Task StartWork_transitions_assigned_to_in_progress_and_stamps_last_processor()
    {
        var (workflow, actorId, reporterId) = BuildWorkflow(PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Assigned, reporterId: reporterId);

        var result = await workflow.StartWorkAsync(note.Id, new TransitionNoteRequest("بدء المعالجة", RowVersionOf(note)));

        Assert.Equal(NoteStatus.InProgress, result.Status);
        var stored = _db.OperationalNotes.Single(n => n.Id == note.Id);
        Assert.Equal(actorId, stored.LastProcessedByUserId);
        Assert.NotNull(stored.WorkStartedAtUtc);
    }

    [Fact]
    public async Task Invalid_transition_throws_conflict_style_exception()
    {
        var (workflow, _, reporterId) = BuildWorkflow(PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Draft, reporterId: reporterId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.StartWorkAsync(note.Id, new TransitionNoteRequest("بدء", RowVersionOf(note))));
    }

    [Fact]
    public async Task Reopened_note_cannot_start_work_without_a_current_assignment()
    {
        var (workflow, _, reporterId) = BuildWorkflow(PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Reopened, reporterId: reporterId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.StartWorkAsync(note.Id, new TransitionNoteRequest("استكمال", RowVersionOf(note))));
    }

    [Fact]
    public async Task Critical_note_processor_cannot_verify_closure_alone()
    {
        var (workflow, actorId, reporterId) = BuildWorkflow(PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.PendingVerification, reporterId: reporterId, severity: NoteSeverity.Critical);
        note.LastProcessedByUserId = actorId;
        _db.SaveChanges();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note))));
        Assert.Contains("فصل الواجبات", ex.Message);
    }

    [Fact]
    public async Task Different_verifier_can_close_a_critical_note()
    {
        var (workflow, _, reporterId) = BuildWorkflow(PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var processor = NoteTestFixtures.AddUser(_db, "processor");
        var note = SeedNote(NoteStatus.PendingVerification, reporterId: reporterId, severity: NoteSeverity.Critical);
        note.LastProcessedByUserId = processor.Id;
        _db.SaveChanges();

        var result = await workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Closed, result.Status);
    }

    [Fact]
    public async Task Non_critical_note_can_be_closed_by_its_own_processor()
    {
        var (workflow, actorId, reporterId) = BuildWorkflow(PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.PendingVerification, reporterId: reporterId, severity: NoteSeverity.Low);
        note.LastProcessedByUserId = actorId;
        _db.SaveChanges();

        var result = await workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Closed, result.Status);
    }

    [Fact]
    public async Task SystemAdministrator_role_does_not_bypass_critical_sod()
    {
        var (workflow, actorId, reporterId) = BuildWorkflow(PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        NoteTestFixtures.GrantPermissions(_db, actorId, RoleCodes.SystemAdministrator, PermissionCodes.NotesVerifyClosure);
        var note = SeedNote(NoteStatus.PendingVerification, reporterId: reporterId, severity: NoteSeverity.Critical);
        note.LastProcessedByUserId = actorId;
        _db.SaveChanges();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note))));
    }

    [Fact]
    public async Task Reopen_clears_closure_fields_and_appends_history()
    {
        var (workflow, _, reporterId) = BuildWorkflow(PermissionCodes.NotesReopen, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Closed, reporterId: reporterId);
        note.ClosedAtUtc = DateTimeOffset.UtcNow;
        note.ClosedByUserId = reporterId;
        note.ClosureSummary = "تم الإغلاق سابقًا";
        _db.SaveChanges();

        var result = await workflow.ReopenAsync(note.Id, new ReopenNoteRequest("تكرر العطل بعد الإغلاق", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Reopened, result.Status);
        Assert.Null(result.ClosedByUserId);
        Assert.Null(result.ClosureSummary);

        var history = await LoadHistoryAsync(note.Id);
        Assert.Contains(history, h => h.ToStatus == NoteStatus.Reopened && h.FromStatus == NoteStatus.Closed);
    }

    [Fact]
    public async Task Cancel_is_blocked_from_closed_status()
    {
        var (workflow, _, reporterId) = BuildWorkflow(PermissionCodes.NotesCancel, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Closed, reporterId: reporterId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CancelAsync(note.Id, new TransitionNoteRequest("إلغاء", RowVersionOf(note))));
    }

    [Fact]
    public async Task Cancel_from_open_ends_current_assignment()
    {
        var (workflow, actorId, reporterId) = BuildWorkflow(PermissionCodes.NotesCancel, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.Open, reporterId: reporterId);
        var assignment = new NoteAssignment
        {
            OperationalNoteId = note.Id,
            AssignedToUserId = reporterId,
            AssignedByUserId = actorId,
            AssignedAtUtc = DateTimeOffset.UtcNow,
            Reason = "تكليف",
            IsCurrent = true
        };
        _db.NoteAssignments.Add(assignment);
        _db.SaveChanges();

        await workflow.CancelAsync(note.Id, new TransitionNoteRequest("إلغاء الحاجة", RowVersionOf(note)));

        var stored = _db.NoteAssignments.IgnoreQueryFilters().Single(a => a.Id == assignment.Id);
        Assert.False(stored.IsCurrent);
        Assert.NotNull(stored.EndedAtUtc);
    }

    [Fact]
    public async Task Out_of_scope_note_is_treated_as_not_found()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        _db.Regions.Add(new Domain.Organization.Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" });
        _db.SaveChanges();

        var note = NoteTestFixtures.NewNote(ScopeType.Region, reporter.Id, regionId: SeedIds.RegionA, status: NoteStatus.Assigned);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var current = new FakeCurrentUser(true, actor.Id, actor.Id.ToString(), "actor",
            [PermissionCodes.NotesStartWork, PermissionCodes.NotesView],
            [new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionB, null, null)]);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, audit);
        var workflow = new NoteWorkflowService(_db, current, scope, audit, queries);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            workflow.StartWorkAsync(note.Id, new TransitionNoteRequest("بدء", Convert.ToBase64String(note.RowVersion))));
    }

    private async Task<IReadOnlyList<NoteStatusHistoryDto>> LoadHistoryAsync(Guid noteId)
    {
        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        var current = FakeUser(viewer.Id, PermissionCodes.NotesView);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, audit);
        return await queries.GetHistoryAsync(noteId);
    }

    private OperationalNote SeedNote(
        NoteStatus status,
        Guid reporterId,
        NoteSeverity severity = NoteSeverity.Medium,
        ScopeType scopeType = ScopeType.Global)
    {
        var note = NoteTestFixtures.NewNote(scopeType, reporterId, status: status, severity: severity);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private static ICurrentUser FakeUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
}
