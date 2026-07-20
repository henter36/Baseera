using Baseera.Application.Abstractions;
using Baseera.Application.CorrectiveActions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class CorrectiveActionStateMachineTests
{
    public static IEnumerable<object[]> AllowedTransitions =>
    [
        [CorrectiveActionStatus.Draft, CorrectiveActionStatus.Open],
        [CorrectiveActionStatus.Draft, CorrectiveActionStatus.Cancelled],
        [CorrectiveActionStatus.Open, CorrectiveActionStatus.Assigned],
        [CorrectiveActionStatus.Open, CorrectiveActionStatus.Cancelled],
        [CorrectiveActionStatus.Assigned, CorrectiveActionStatus.InProgress],
        [CorrectiveActionStatus.Assigned, CorrectiveActionStatus.Assigned],
        [CorrectiveActionStatus.Assigned, CorrectiveActionStatus.Cancelled],
        [CorrectiveActionStatus.InProgress, CorrectiveActionStatus.PendingVerification],
        [CorrectiveActionStatus.InProgress, CorrectiveActionStatus.Cancelled],
        [CorrectiveActionStatus.PendingVerification, CorrectiveActionStatus.Completed],
        [CorrectiveActionStatus.PendingVerification, CorrectiveActionStatus.InProgress],
        [CorrectiveActionStatus.PendingVerification, CorrectiveActionStatus.Cancelled],
        [CorrectiveActionStatus.Completed, CorrectiveActionStatus.Reopened],
        [CorrectiveActionStatus.Reopened, CorrectiveActionStatus.Assigned],
        [CorrectiveActionStatus.Reopened, CorrectiveActionStatus.InProgress],
        [CorrectiveActionStatus.Reopened, CorrectiveActionStatus.Cancelled]
    ];

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Allowed_transitions_are_accepted(CorrectiveActionStatus from, CorrectiveActionStatus to) =>
        Assert.True(CorrectiveActionStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(CorrectiveActionStatus.Open, CorrectiveActionStatus.Completed)]
    [InlineData(CorrectiveActionStatus.Assigned, CorrectiveActionStatus.Completed)]
    [InlineData(CorrectiveActionStatus.InProgress, CorrectiveActionStatus.Completed)]
    [InlineData(CorrectiveActionStatus.Cancelled, CorrectiveActionStatus.Open)]
    public void Disallowed_transitions_are_rejected(CorrectiveActionStatus from, CorrectiveActionStatus to) =>
        Assert.False(CorrectiveActionStateMachine.CanTransition(from, to));

    [Fact]
    public void Reference_formatter_uses_ca_prefix_and_eight_digits() =>
        Assert.Equal("CA-00000042", CorrectiveActionReferenceFormatter.Format(42));

    [Fact]
    public void Overdue_is_computed_not_stored()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(CorrectiveActionStateMachine.IsOverdue(CorrectiveActionStatus.Open, now.AddDays(-1), now));
        Assert.False(CorrectiveActionStateMachine.IsOverdue(CorrectiveActionStatus.Completed, now.AddDays(-1), now));
        Assert.True(CorrectiveActionStateMachine.IsDueSoon(CorrectiveActionStatus.Open, now.AddDays(2), now, 3));
    }
}

public sealed class CorrectiveActionValidatorTests
{
    [Fact]
    public void Create_rejects_whitespace_title_and_description()
    {
        var result = new CreateCorrectiveActionRequestValidator().TestValidate(
            new CreateCorrectiveActionRequest(" ", " ", CorrectiveActionPriority.Medium, null, null, DateTimeOffset.UtcNow.AddDays(1)));
        result.ShouldHaveValidationErrorFor(x => x.Title);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Assignment_requires_xor_target_and_reason()
    {
        var validator = new AssignCorrectiveActionRequestValidator();
        var both = validator.TestValidate(new AssignCorrectiveActionRequest(Guid.NewGuid(), Guid.NewGuid(), null, "سبب", "AAA="));
        both.ShouldHaveValidationErrorFor(x => x);
        var missingReason = validator.TestValidate(new AssignCorrectiveActionRequest(Guid.NewGuid(), null, null, " ", "AAA="));
        missingReason.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Completion_summary_is_required()
    {
        var result = new CompleteCorrectiveActionRequestValidator().TestValidate(
            new CompleteCorrectiveActionRequest("سبب", " ", "AAA="));
        result.ShouldHaveValidationErrorFor(x => x.CompletionSummary);
    }
}

public sealed class CorrectiveActionAppendOnlyTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Modified_status_history_is_rejected()
    {
        var user = NoteTestFixtures.AddUser(_db, "user");
        var note = AddNote(user.Id);
        var action = AddAction(note.Id, user.Id);
        var history = new CorrectiveActionStatusHistory
        {
            CorrectiveActionId = action.Id,
            ToStatus = CorrectiveActionStatus.Draft,
            ChangedByUserId = user.Id
        };
        _db.CorrectiveActionStatusHistories.Add(history);
        _db.SaveChanges();

        history.Reason = "changed";
        Assert.Throws<InvalidOperationException>(() => _db.SaveChanges());
    }

    [Fact]
    public void Deleted_status_history_is_rejected()
    {
        var user = NoteTestFixtures.AddUser(_db, "user");
        var note = AddNote(user.Id);
        var action = AddAction(note.Id, user.Id);
        var history = new CorrectiveActionStatusHistory
        {
            CorrectiveActionId = action.Id,
            ToStatus = CorrectiveActionStatus.Draft,
            ChangedByUserId = user.Id
        };
        _db.CorrectiveActionStatusHistories.Add(history);
        _db.SaveChanges();

        _db.CorrectiveActionStatusHistories.Remove(history);
        Assert.Throws<InvalidOperationException>(() => _db.SaveChanges());
    }

    private OperationalNote AddNote(Guid userId)
    {
        var note = NoteTestFixtures.NewNote(ScopeType.Global, userId, status: NoteStatus.Open);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private CorrectiveAction AddAction(Guid noteId, Guid userId)
    {
        var action = new CorrectiveAction
        {
            ReferenceNumber = "CA-00000001",
            OperationalNoteId = noteId,
            Title = "إجراء",
            Description = "وصف",
            CreatedByUserId = userId,
            CreatedBy = "test"
        };
        _db.CorrectiveActions.Add(action);
        _db.SaveChanges();
        return action;
    }
}

public sealed class CorrectiveActionNoteGuardTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Note_closure_is_blocked_when_active_corrective_action_exists()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = NoteTestFixtures.NewNote(ScopeType.Global, actor.Id, status: NoteStatus.PendingVerification);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        _db.CorrectiveActions.Add(new CorrectiveAction
        {
            ReferenceNumber = "CA-00000001",
            OperationalNoteId = note.Id,
            Title = "إجراء",
            Description = "وصف",
            CreatedByUserId = actor.Id,
            Status = CorrectiveActionStatus.Open
        });
        _db.SaveChanges();

        var workflow = BuildWorkflow(actor.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.VerifyClosureAsync(note.Id, new CloseNoteRequest("اعتماد", "ملخص", Convert.ToBase64String(note.RowVersion))));

        Assert.Equal(NoteStatus.PendingVerification, note.Status);
        Assert.Contains(_db.AuditLogs, a => a.Action == "NoteClosureBlockedByCorrectiveActions");
    }

    [Fact]
    public async Task Note_cancellation_is_blocked_before_mutation_when_active_corrective_action_exists()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = NoteTestFixtures.NewNote(ScopeType.Global, actor.Id, status: NoteStatus.Open);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        _db.CorrectiveActions.Add(new CorrectiveAction
        {
            ReferenceNumber = "CA-00000002",
            OperationalNoteId = note.Id,
            Title = "إجراء",
            Description = "وصف",
            CreatedByUserId = actor.Id,
            Status = CorrectiveActionStatus.Open
        });
        _db.SaveChanges();

        var workflow = BuildWorkflow(actor.Id, PermissionCodes.NotesCancel, PermissionCodes.NotesView);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CancelAsync(note.Id, new TransitionNoteRequest("إلغاء", Convert.ToBase64String(note.RowVersion))));

        Assert.Equal(NoteStatus.Open, note.Status);
        Assert.DoesNotContain(_db.NoteStatusHistories, h => h.OperationalNoteId == note.Id && h.ToStatus == NoteStatus.Cancelled);
        Assert.DoesNotContain(_db.AuditLogs, a => a.Action == "NoteCancelled");
        Assert.Contains(_db.AuditLogs, a => a.Action == "NoteCancellationBlockedByCorrectiveActions");
    }

    private INoteWorkflowService BuildWorkflow(Guid userId, params string[] permissions)
    {
        var effectivePermissions = permissions.Length == 0
            ? [PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView]
            : permissions;
        var current = new FakeCurrentUser(
            true,
            userId,
            "actor",
            "actor",
            effectivePermissions,
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var org = new OrganizationalScopeService(current, _db);
        var scope = new NoteScopeService(org, current, _db);
        var audit = new AuditService(_db, current, org);
        var queries = new NoteQueryService(_db, current, scope, audit);
        return new NoteWorkflowService(_db, current, scope, audit, queries);
    }
}

public sealed class CorrectiveActionAssignmentHardeningTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Validation_failure_does_not_end_current_assignment()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var currentAssignee = NoteTestFixtures.AddUser(_db, "current");
        var nextAssignee = NoteTestFixtures.AddUser(_db, "next");
        NoteTestFixtures.GrantPermissions(_db, nextAssignee.Id, "Worker", PermissionCodes.CorrectiveActionsStartWork);
        _db.UserScopes.Add(new UserScope { UserId = nextAssignee.Id, ScopeType = ScopeType.Global, IsActive = true });
        var note = NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, status: NoteStatus.Open);
        _db.OperationalNotes.Add(note);
        var action = NewAction(note.Id, reporter.Id, CorrectiveActionStatus.InProgress);
        var assignment = new CorrectiveActionAssignment
        {
            CorrectiveActionId = action.Id,
            AssignedToUserId = currentAssignee.Id,
            AssignedByUserId = actor.Id,
            Reason = "حالي",
            IsCurrent = true
        };
        _db.CorrectiveActions.Add(action);
        _db.CorrectiveActionAssignments.Add(assignment);
        _db.SaveChanges();

        var service = BuildAssignmentService(actor.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(action.Id, new AssignCorrectiveActionRequest(nextAssignee.Id, null, null, "إعادة", Convert.ToBase64String(action.RowVersion))));

        Assert.True(assignment.IsCurrent);
        Assert.Null(assignment.EndedAtUtc);
    }

    [Fact]
    public void Non_unique_db_update_exception_is_not_classified_as_current_assignment_conflict()
    {
        var method = typeof(CorrectiveActionAssignmentService).GetMethod(
            "IsCurrentAssignmentUniqueConflict",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var result = (bool)method.Invoke(null, [new DbUpdateException("generic failure")])!;
        Assert.False(result);
    }

    private CorrectiveActionAssignmentService BuildAssignmentService(Guid userId)
    {
        var current = new FakeCurrentUser(
            true,
            userId,
            "actor",
            "actor",
            [PermissionCodes.CorrectiveActionsAssign, PermissionCodes.CorrectiveActionsView],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var org = new OrganizationalScopeService(current, _db);
        var noteScope = new NoteScopeService(org, current, _db);
        var actionScope = new CorrectiveActionScopeService(_db, noteScope);
        var audit = new AuditService(_db, current, org);
        var queries = new CorrectiveActionQueryService(_db, current, actionScope, noteScope, audit);
        return new CorrectiveActionAssignmentService(_db, current, actionScope, audit, queries);
    }

    private static CorrectiveAction NewAction(Guid noteId, Guid userId, CorrectiveActionStatus status) => new()
    {
        ReferenceNumber = $"CA-{Random.Shared.Next(1, 99999999):00000000}",
        OperationalNoteId = noteId,
        Title = "إجراء",
        Description = "وصف",
        CreatedByUserId = userId,
        Status = status
    };
}

public sealed class CorrectiveActionWorkflowHardeningTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Critical_sod_is_enforced_before_mutation()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = NoteTestFixtures.NewNote(ScopeType.Global, actor.Id, status: NoteStatus.Open);
        _db.OperationalNotes.Add(note);
        var action = new CorrectiveAction
        {
            ReferenceNumber = "CA-00001000",
            OperationalNoteId = note.Id,
            Title = "حرج",
            Description = "وصف",
            Priority = CorrectiveActionPriority.Critical,
            Status = CorrectiveActionStatus.PendingVerification,
            CreatedByUserId = actor.Id
        };
        _db.CorrectiveActions.Add(action);
        _db.SaveChanges();
        _db.CorrectiveActionStatusHistories.Add(new CorrectiveActionStatusHistory
        {
            CorrectiveActionId = action.Id,
            FromStatus = CorrectiveActionStatus.InProgress,
            ToStatus = CorrectiveActionStatus.PendingVerification,
            ChangedByUserId = actor.Id,
            Reason = "شارك"
        });
        _db.SaveChanges();

        var workflow = BuildWorkflow(actor.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.VerifyCompletionAsync(action.Id, new CompleteCorrectiveActionRequest("اعتماد", "ملخص", Convert.ToBase64String(action.RowVersion))));

        Assert.Equal(CorrectiveActionStatus.PendingVerification, action.Status);
        Assert.Null(action.CompletedAtUtc);
        Assert.Null(action.CompletedByUserId);
        Assert.DoesNotContain(_db.CorrectiveActionStatusHistories, h => h.CorrectiveActionId == action.Id && h.ToStatus == CorrectiveActionStatus.Completed);
        Assert.DoesNotContain(_db.AuditLogs, a => a.EntityId == action.Id.ToString() && a.Action == "CorrectiveActionCompleted");
    }

    private CorrectiveActionWorkflowService BuildWorkflow(Guid userId)
    {
        var current = new FakeCurrentUser(
            true,
            userId,
            "actor",
            "actor",
            [PermissionCodes.CorrectiveActionsVerifyCompletion, PermissionCodes.CorrectiveActionsView],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var org = new OrganizationalScopeService(current, _db);
        var noteScope = new NoteScopeService(org, current, _db);
        var actionScope = new CorrectiveActionScopeService(_db, noteScope);
        var audit = new AuditService(_db, current, org);
        var queries = new CorrectiveActionQueryService(_db, current, actionScope, noteScope, audit);
        return new CorrectiveActionWorkflowService(_db, current, actionScope, audit, queries);
    }
}
