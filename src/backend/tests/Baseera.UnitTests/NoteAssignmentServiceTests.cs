using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests;

public sealed class NoteAssignmentServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    private INoteAssignmentService BuildService(Guid actorId, params string[] permissions)
    {
        var current = FakeUser(actorId, permissions);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, audit);
        return new NoteAssignmentService(_db, current, scope, audit, queries);
    }

    private static string RowVersionOf(OperationalNote note) => Convert.ToBase64String(note.RowVersion);

    [Fact]
    public async Task Assign_to_user_without_work_permission_is_rejected()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, "assignee-no-permission");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(note.Id, new AssignNoteRequest(assignee.Id, null, null, "تكليف أولي", RowVersionOf(note))));
    }

    [Fact]
    public async Task Assign_to_inactive_user_is_rejected()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, "inactive-assignee", active: false);
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, "Worker", PermissionCodes.NotesStartWork);
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(note.Id, new AssignNoteRequest(assignee.Id, null, null, "تكليف أولي", RowVersionOf(note))));
    }

    [Fact]
    public async Task Assign_to_capable_user_in_scope_succeeds_and_transitions_to_assigned()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, "capable-assignee");
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, "Worker", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope { UserId = assignee.Id, ScopeType = ScopeType.Global, IsActive = true });
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        var result = await service.AssignAsync(note.Id, new AssignNoteRequest(assignee.Id, null, null, "تكليف أولي", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Assigned, result.Status);
        Assert.NotNull(result.CurrentAssignment);
        Assert.Equal(assignee.Id, result.CurrentAssignment!.AssignedToUserId);
        Assert.True(result.CurrentAssignment.IsCurrent);
    }

    [Fact]
    public async Task Assign_to_user_outside_note_scope_is_rejected()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, "out-of-scope-assignee");
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, "Worker", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope { UserId = assignee.Id, ScopeType = ScopeType.Region, RegionId = SeedIds.RegionB, IsActive = true });
        _db.Regions.AddRange(
            new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" },
            new Region { Id = SeedIds.RegionB, OrganizationId = SeedIds.Organization, Code = "B", NameAr = "ب" });
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id, scopeType: ScopeType.Region, regionId: SeedIds.RegionA);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(note.Id, new AssignNoteRequest(assignee.Id, null, null, "تكليف أولي", RowVersionOf(note))));
    }

    [Fact]
    public async Task Assign_to_department_succeeds_without_work_permission_checks()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var org = new Organization { Id = SeedIds.Organization, Code = "HQ", NameAr = "رئيسي" };
        var department = new Department { Id = Guid.NewGuid(), OrganizationId = org.Id, Code = "DPT", NameAr = "إدارة الصيانة" };
        _db.Organizations.Add(org);
        _db.Departments.Add(department);
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        var result = await service.AssignAsync(note.Id, new AssignNoteRequest(null, department.Id, null, "تكليف إدارة", RowVersionOf(note)));

        Assert.Equal(department.Id, result.CurrentAssignment!.AssignedToDepartmentId);
    }

    [Fact]
    public async Task Reassign_ends_previous_assignment_and_keeps_status_assigned()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var firstAssignee = NoteTestFixtures.AddUser(_db, "first-assignee");
        var secondAssignee = NoteTestFixtures.AddUser(_db, "second-assignee");
        foreach (var user in new[] { firstAssignee, secondAssignee })
        {
            NoteTestFixtures.GrantPermissions(_db, user.Id, $"Worker-{user.Id}", PermissionCodes.NotesStartWork);
            _db.UserScopes.Add(new UserScope { UserId = user.Id, ScopeType = ScopeType.Global, IsActive = true });
        }

        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        var first = await service.AssignAsync(note.Id, new AssignNoteRequest(firstAssignee.Id, null, null, "تكليف أول", RowVersionOf(note)));
        var noteAfterFirst = _db.OperationalNotes.Single(n => n.Id == note.Id);

        var second = await service.AssignAsync(note.Id, new AssignNoteRequest(secondAssignee.Id, null, null, "إعادة تكليف", Convert.ToBase64String(noteAfterFirst.RowVersion)));

        Assert.Equal(NoteStatus.Assigned, second.Status);
        Assert.Equal(secondAssignee.Id, second.CurrentAssignment!.AssignedToUserId);

        var previous = _db.NoteAssignments.Single(a => a.AssignedToUserId == firstAssignee.Id);
        Assert.False(previous.IsCurrent);
        Assert.NotNull(previous.EndedAtUtc);
        Assert.Equal("إعادة تكليف", previous.EndReason);
    }

    private OperationalNote SeedNote(
        NoteStatus status,
        Guid reporterId,
        ScopeType scopeType = ScopeType.Global,
        Guid? regionId = null)
    {
        var note = NoteTestFixtures.NewNote(scopeType, reporterId, regionId: regionId, status: status);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private static ICurrentUser FakeUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
}
