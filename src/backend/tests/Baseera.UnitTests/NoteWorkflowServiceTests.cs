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
        return (BuildWorkflowForUser(actor.Id, permissions), actor.Id, reporter.Id);
    }

    private INoteWorkflowService BuildWorkflowForUser(Guid userId, params string[] permissions)
    {
        var current = FakeUser(userId, permissions);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, audit);
        return new NoteWorkflowService(_db, current, scope, audit, queries);
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
    public async Task Critical_note_start_work_actor_cannot_verify_closure()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var processor = NoteTestFixtures.AddUser(_db, "processor");
        var note = SeedPendingCritical(reporter.Id, processor.Id);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, NoteTestFixtures.AddUser(_db, "other").Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(processor.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        await AssertVerifyRejectedWithoutMutation(workflow, note);
    }

    [Fact]
    public async Task Critical_note_submit_for_verification_actor_cannot_verify_closure()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var submitter = NoteTestFixtures.AddUser(_db, "submitter");
        var starter = NoteTestFixtures.AddUser(_db, "starter");
        var note = SeedPendingCritical(reporter.Id, submitter.Id);
        AppendProcessingHistory(note.Id, starter.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, submitter.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(submitter.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        await AssertVerifyRejectedWithoutMutation(workflow, note);
    }

    [Fact]
    public async Task Critical_note_earlier_processor_cannot_verify_after_another_user_submits()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var first = NoteTestFixtures.AddUser(_db, "first");
        var second = NoteTestFixtures.AddUser(_db, "second");
        var note = SeedPendingCritical(reporter.Id, second.Id);
        AppendProcessingHistory(note.Id, first.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, second.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(first.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        await AssertVerifyRejectedWithoutMutation(workflow, note);
    }

    [Fact]
    public async Task Critical_note_reopened_start_work_actor_cannot_verify_closure()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var processor = NoteTestFixtures.AddUser(_db, "reopen-processor");
        var note = SeedPendingCritical(reporter.Id, processor.Id);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.Reopened, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, NoteTestFixtures.AddUser(_db, "submitter").Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(processor.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        await AssertVerifyRejectedWithoutMutation(workflow, note);
    }

    [Fact]
    public async Task Critical_note_system_admin_processor_cannot_verify_closure()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var admin = NoteTestFixtures.AddUser(_db, "sysadmin");
        NoteTestFixtures.GrantPermissions(_db, admin.Id, RoleCodes.SystemAdministrator, PermissionCodes.NotesVerifyClosure);
        var note = SeedPendingCritical(reporter.Id, admin.Id);
        AppendProcessingHistory(note.Id, admin.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, admin.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(admin.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        await AssertVerifyRejectedWithoutMutation(workflow, note);
    }

    [Fact]
    public async Task Critical_note_processor_is_rejected_before_note_mutation()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var processor = NoteTestFixtures.AddUser(_db, "processor");
        var assigner = NoteTestFixtures.AddUser(_db, "assigner");
        var note = SeedPendingCritical(reporter.Id, processor.Id);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);
        var assignment = new NoteAssignment
        {
            OperationalNoteId = note.Id,
            AssignedToUserId = processor.Id,
            AssignedByUserId = assigner.Id,
            AssignedAtUtc = DateTimeOffset.UtcNow,
            Reason = "تكليف",
            IsCurrent = true
        };
        _db.NoteAssignments.Add(assignment);
        _db.SaveChanges();

        var auditsBefore = _db.AuditLogs.Count();
        var historyBefore = _db.NoteStatusHistories.Count(h => h.OperationalNoteId == note.Id);
        var workflow = BuildWorkflowForUser(processor.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note))));

        Assert.Equal(EntityState.Unchanged, _db.Entry(note).State);
        Assert.Equal(NoteStatus.PendingVerification, note.Status);
        Assert.Null(note.ClosedAtUtc);
        Assert.Null(note.ClosedByUserId);
        Assert.Null(note.ClosureSummary);
        Assert.Null(_db.NoteAssignments.Single(a => a.Id == assignment.Id).CompletedAtUtc);
        Assert.Equal(historyBefore, _db.NoteStatusHistories.Count(h => h.OperationalNoteId == note.Id));
        Assert.Equal(auditsBefore, _db.AuditLogs.Count());
        Assert.DoesNotContain(_db.AuditLogs, a => a.Action == "NoteClosed");
    }

    [Fact]
    public async Task Critical_note_independent_verifier_can_close()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var processor = NoteTestFixtures.AddUser(_db, "processor");
        var verifier = NoteTestFixtures.AddUser(_db, "verifier");
        var note = SeedPendingCritical(reporter.Id, processor.Id);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(verifier.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var result = await workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Closed, result.Status);
        Assert.Equal(verifier.Id, result.ClosedByUserId);
    }

    [Fact]
    public async Task Critical_note_assigner_who_never_processed_can_verify_if_authorized()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var processor = NoteTestFixtures.AddUser(_db, "processor");
        var assigner = NoteTestFixtures.AddUser(_db, "assigner");
        var note = SeedPendingCritical(reporter.Id, processor.Id);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);
        // Assigner only appears as AssignedBy, never as processing history actor.
        _db.NoteAssignments.Add(new NoteAssignment
        {
            OperationalNoteId = note.Id,
            AssignedToUserId = processor.Id,
            AssignedByUserId = assigner.Id,
            AssignedAtUtc = DateTimeOffset.UtcNow,
            Reason = "تكليف",
            IsCurrent = true
        });
        _db.SaveChanges();

        var workflow = BuildWorkflowForUser(assigner.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var result = await workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note)));
        Assert.Equal(NoteStatus.Closed, result.Status);
        Assert.Equal(assigner.Id, result.ClosedByUserId);
    }

    [Fact]
    public async Task Critical_note_creator_who_never_processed_can_verify_if_authorized()
    {
        var creator = NoteTestFixtures.AddUser(_db, "creator");
        var processor = NoteTestFixtures.AddUser(_db, "processor");
        var note = SeedPendingCritical(creator.Id, processor.Id);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(creator.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var result = await workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note)));
        Assert.Equal(NoteStatus.Closed, result.Status);
        Assert.Equal(creator.Id, result.ClosedByUserId);
    }

    [Fact]
    public async Task Non_critical_processor_can_verify_when_policy_allows()
    {
        // Policy: Critical SoD applies only to NoteSeverity.Critical.
        // Non-critical notes may be verified by their own processor when authorized.
        var (workflow, actorId, reporterId) = BuildWorkflow(PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var note = SeedNote(NoteStatus.PendingVerification, reporterId: reporterId, severity: NoteSeverity.Low);
        note.LastProcessedByUserId = actorId;
        AppendProcessingHistory(note.Id, actorId, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, actorId, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var result = await workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note)));
        Assert.Equal(NoteStatus.Closed, result.Status);
    }

    [Fact]
    public async Task Critical_multi_user_sequence_only_independent_verifier_succeeds()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var userA = NoteTestFixtures.AddUser(_db, "user-a");
        var userB = NoteTestFixtures.AddUser(_db, "user-b");
        var userC = NoteTestFixtures.AddUser(_db, "user-c");
        var note = SeedPendingCritical(reporter.Id, userB.Id);
        AppendProcessingHistory(note.Id, userA.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, userB.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflowA = BuildWorkflowForUser(userA.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var workflowB = BuildWorkflowForUser(userB.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var workflowC = BuildWorkflowForUser(userC.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);

        await AssertVerifyRejectedWithoutMutation(workflowA, note);
        await AssertVerifyRejectedWithoutMutation(workflowB, note);

        var closed = await workflowC.VerifyClosureAsync(
            note.Id,
            new CloseNoteRequest("اعتماد مستقل", "تم التحقق", RowVersionOf(note)));
        Assert.Equal(NoteStatus.Closed, closed.Status);
        Assert.Equal(userC.Id, closed.ClosedByUserId);
        Assert.Equal(1, _db.NoteStatusHistories.Count(h => h.OperationalNoteId == note.Id && h.ToStatus == NoteStatus.Closed));
        Assert.Equal(1, _db.AuditLogs.Count(a => a.Action == "NoteClosed" && a.EntityId == note.Id.ToString()));
    }

    [Fact]
    public async Task Return_for_rework_actor_is_not_treated_as_processor_for_sod()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var processor = NoteTestFixtures.AddUser(_db, "processor");
        var reviewer = NoteTestFixtures.AddUser(_db, "reviewer");
        var note = SeedPendingCritical(reporter.Id, processor.Id);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.Assigned, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);
        // Reviewer returned for rework (PendingVerification → InProgress) — not a processing participant.
        AppendProcessingHistory(note.Id, reviewer.Id, NoteStatus.PendingVerification, NoteStatus.InProgress);
        AppendProcessingHistory(note.Id, processor.Id, NoteStatus.InProgress, NoteStatus.PendingVerification);

        var workflow = BuildWorkflowForUser(reviewer.Id, PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView);
        var result = await workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note)));
        Assert.Equal(NoteStatus.Closed, result.Status);
        Assert.Equal(reviewer.Id, result.ClosedByUserId);
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

    private async Task AssertVerifyRejectedWithoutMutation(INoteWorkflowService workflow, OperationalNote note)
    {
        var auditsBefore = _db.AuditLogs.Count(a => a.Action == "NoteClosed");
        var historyBefore = _db.NoteStatusHistories.Count(h => h.OperationalNoteId == note.Id && h.ToStatus == NoteStatus.Closed);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "تم الحل", RowVersionOf(note))));
        Assert.Contains("فصل الواجبات", ex.Message);

        _db.Entry(note).Reload();
        Assert.Equal(NoteStatus.PendingVerification, note.Status);
        Assert.Null(note.ClosedAtUtc);
        Assert.Null(note.ClosedByUserId);
        Assert.Null(note.ClosureSummary);
        Assert.Equal(historyBefore, _db.NoteStatusHistories.Count(h => h.OperationalNoteId == note.Id && h.ToStatus == NoteStatus.Closed));
        Assert.Equal(auditsBefore, _db.AuditLogs.Count(a => a.Action == "NoteClosed"));
    }

    private OperationalNote SeedPendingCritical(Guid reporterId, Guid lastProcessorId)
    {
        var note = SeedNote(NoteStatus.PendingVerification, reporterId, NoteSeverity.Critical);
        note.LastProcessedByUserId = lastProcessorId;
        _db.SaveChanges();
        return note;
    }

    private void AppendProcessingHistory(Guid noteId, Guid userId, NoteStatus from, NoteStatus to)
    {
        _db.NoteStatusHistories.Add(new NoteStatusHistory
        {
            OperationalNoteId = noteId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            ChangedAtUtc = DateTimeOffset.UtcNow,
            Reason = "اختبار"
        });
        _db.SaveChanges();
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
