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
    [Theory]
    [InlineData(CorrectiveActionStatus.Draft, CorrectiveActionStatus.Open)]
    [InlineData(CorrectiveActionStatus.Open, CorrectiveActionStatus.Assigned)]
    [InlineData(CorrectiveActionStatus.Assigned, CorrectiveActionStatus.InProgress)]
    [InlineData(CorrectiveActionStatus.InProgress, CorrectiveActionStatus.PendingVerification)]
    [InlineData(CorrectiveActionStatus.PendingVerification, CorrectiveActionStatus.Completed)]
    [InlineData(CorrectiveActionStatus.Completed, CorrectiveActionStatus.Reopened)]
    [InlineData(CorrectiveActionStatus.Reopened, CorrectiveActionStatus.InProgress)]
    [InlineData(CorrectiveActionStatus.Open, CorrectiveActionStatus.Cancelled)]
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

    private INoteWorkflowService BuildWorkflow(Guid userId)
    {
        var current = new FakeCurrentUser(
            true,
            userId,
            "actor",
            "actor",
            [PermissionCodes.NotesVerifyClosure, PermissionCodes.NotesView],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var org = new OrganizationalScopeService(current, _db);
        var scope = new NoteScopeService(org, current, _db);
        var audit = new AuditService(_db, current, org);
        var queries = new NoteQueryService(_db, current, scope, audit);
        return new NoteWorkflowService(_db, current, scope, audit, queries);
    }
}
