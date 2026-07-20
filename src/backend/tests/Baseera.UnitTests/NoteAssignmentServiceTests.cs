using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class NoteAssignmentServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    private INoteAssignmentService BuildService(Guid actorId, params string[] permissions)
    {
        NoteTestFixtures.GrantPermissions(_db, actorId, $"Actor-{actorId}", permissions);
        var current = FakeUser(actorId, permissions);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, typeAccess, audit);
        return new NoteAssignmentService(_db, current, scope, typeAccess, audit, queries);
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

    [Fact]
    public async Task Assign_with_no_target_is_rejected_before_database_mutation()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);
        var auditsBefore = _db.AuditLogs.Count();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(note.Id, new AssignNoteRequest(null, null, null, "بلا هدف", RowVersionOf(note))));

        Assert.Equal("يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.", ex.Message);
        AssertNoAssignmentSideEffects(note.Id, NoteStatus.Open, auditsBefore);
    }

    [Fact]
    public async Task Assign_with_both_user_and_department_is_rejected()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, "capable");
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, "Worker", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope { UserId = assignee.Id, ScopeType = ScopeType.Global, IsActive = true });
        var department = SeedDepartment();
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);
        var auditsBefore = _db.AuditLogs.Count();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(note.Id, new AssignNoteRequest(assignee.Id, department.Id, null, "كلاهما", RowVersionOf(note))));

        Assert.Equal("يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.", ex.Message);
        AssertNoAssignmentSideEffects(note.Id, NoteStatus.Open, auditsBefore);
    }

    [Fact]
    public async Task Assign_with_both_targets_does_not_validate_or_persist_either_target()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        // Valid user would succeed alone; missing department would 404 if validation ran.
        var assignee = NoteTestFixtures.AddUser(_db, "capable");
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, "Worker", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope { UserId = assignee.Id, ScopeType = ScopeType.Global, IsActive = true });
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);
        var missingDepartmentId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var auditsBefore = _db.AuditLogs.Count();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(
                note.Id,
                new AssignNoteRequest(assignee.Id, missingDepartmentId, null, "كلاهما", RowVersionOf(note))));

        Assert.Equal("يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.", ex.Message);
        Assert.IsNotType<KeyNotFoundException>(ex);
        AssertNoAssignmentSideEffects(note.Id, NoteStatus.Open, auditsBefore);
    }

    [Fact]
    public async Task Assign_with_invalid_department_is_rejected()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);
        var auditsBefore = _db.AuditLogs.Count();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AssignAsync(
                note.Id,
                new AssignNoteRequest(null, Guid.NewGuid(), null, "إدارة وهمية", RowVersionOf(note))));

        AssertNoAssignmentSideEffects(note.Id, NoteStatus.Open, auditsBefore);
    }

    [Fact]
    public async Task Assign_with_valid_user_only_succeeds()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, "capable-only");
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, "Worker", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope { UserId = assignee.Id, ScopeType = ScopeType.Global, IsActive = true });
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        var result = await service.AssignAsync(
            note.Id,
            new AssignNoteRequest(assignee.Id, null, null, "مستخدم فقط", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Assigned, result.Status);
        Assert.Equal(assignee.Id, result.CurrentAssignment!.AssignedToUserId);
        Assert.Null(result.CurrentAssignment.AssignedToDepartmentId);
    }

    [Fact]
    public async Task Assign_with_valid_department_only_succeeds()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var department = SeedDepartment();
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        var result = await service.AssignAsync(
            note.Id,
            new AssignNoteRequest(null, department.Id, null, "إدارة فقط", RowVersionOf(note)));

        Assert.Equal(department.Id, result.CurrentAssignment!.AssignedToDepartmentId);
        Assert.Null(result.CurrentAssignment.AssignedToUserId);
    }

    [Fact]
    public async Task Invalid_assign_transition_does_not_mutate_tracked_current_assignment()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var previousAssignee = NoteTestFixtures.AddUser(_db, "previous");
        NoteTestFixtures.GrantPermissions(_db, previousAssignee.Id, "Worker", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope { UserId = previousAssignee.Id, ScopeType = ScopeType.Global, IsActive = true });
        var nextAssignee = NoteTestFixtures.AddUser(_db, "next");
        NoteTestFixtures.GrantPermissions(_db, nextAssignee.Id, "Worker-Next", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope { UserId = nextAssignee.Id, ScopeType = ScopeType.Global, IsActive = true });
        _db.SaveChanges();

        // Closed notes cannot be assigned; seed a lingering "current" assignment to prove no premature mutate.
        var note = SeedNote(NoteStatus.Closed, reporter.Id);
        var current = new NoteAssignment
        {
            OperationalNoteId = note.Id,
            AssignedToUserId = previousAssignee.Id,
            AssignedByUserId = actor.Id,
            AssignedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            Reason = "تكليف سابق",
            IsCurrent = true,
            CreatedBy = "seed"
        };
        _db.NoteAssignments.Add(current);
        _db.SaveChanges();
        var auditsBefore = _db.AuditLogs.Count();
        var historyBefore = _db.NoteStatusHistories.Count(h => h.OperationalNoteId == note.Id);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(
                note.Id,
                new AssignNoteRequest(nextAssignee.Id, null, null, "محاولة غير صالحة", RowVersionOf(note))));

        var tracked = _db.NoteAssignments.Local.Single(a => a.Id == current.Id);
        Assert.True(tracked.IsCurrent);
        Assert.Null(tracked.EndedAtUtc);
        Assert.Null(tracked.EndReason);
        Assert.Equal(EntityState.Unchanged, _db.Entry(tracked).State);

        _db.ChangeTracker.Clear();
        var reloaded = _db.NoteAssignments.Single(a => a.Id == current.Id);
        Assert.True(reloaded.IsCurrent);
        Assert.Null(reloaded.EndedAtUtc);
        Assert.Null(reloaded.EndReason);
        Assert.Equal(1, _db.NoteAssignments.Count(a => a.OperationalNoteId == note.Id));
        Assert.Equal(NoteStatus.Closed, _db.OperationalNotes.Single(n => n.Id == note.Id).Status);
        Assert.Equal(historyBefore, _db.NoteStatusHistories.Count(h => h.OperationalNoteId == note.Id));
        Assert.Equal(auditsBefore, _db.AuditLogs.Count());
    }

    [Fact]
    public async Task Global_assignee_can_receive_region_note()
    {
        await AssertAssignSucceedsForAssigneeScopeAsync(
            ScopeType.Global, null, null, null,
            ScopeType.Region, SeedIds.RegionA, null, null);
    }

    [Fact]
    public async Task Global_assignee_can_receive_facility_note()
    {
        SeedOrgGraph();
        await AssertAssignSucceedsForAssigneeScopeAsync(
            ScopeType.Global, null, null, null,
            ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null);
    }

    [Fact]
    public async Task Global_assignee_can_receive_facility_unit_note()
    {
        SeedOrgGraph();
        await AssertAssignSucceedsForAssigneeScopeAsync(
            ScopeType.Global, null, null, null,
            ScopeType.FacilityUnit, SeedIds.RegionA, SeedIds.FacilityA1, UnitA1East);
    }

    [Fact]
    public async Task Headquarters_assignee_can_receive_headquarters_note()
    {
        await AssertAssignSucceedsForAssigneeScopeAsync(
            ScopeType.Headquarters, null, null, null,
            ScopeType.Headquarters, null, null, null);
    }

    [Fact]
    public async Task Headquarters_assignee_cannot_receive_region_note()
    {
        await AssertAssignRejectedForAssigneeScopeAsync(
            ScopeType.Headquarters, null, null, null,
            ScopeType.Region, SeedIds.RegionA, null, null);
    }

    [Fact]
    public async Task Region_assignee_cannot_receive_headquarters_note()
    {
        SeedOrgGraph();
        await AssertAssignRejectedForAssigneeScopeAsync(
            ScopeType.Region, SeedIds.RegionA, null, null,
            ScopeType.Headquarters, null, null, null);
    }

    private async Task AssertAssignSucceedsForAssigneeScopeAsync(
        ScopeType assigneeScope,
        Guid? assigneeRegionId,
        Guid? assigneeFacilityId,
        Guid? assigneeUnitId,
        ScopeType noteScope,
        Guid? noteRegionId,
        Guid? noteFacilityId,
        Guid? noteUnitId)
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, $"assignee-{Guid.NewGuid():N}");
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, $"Worker-{assignee.Id}", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope
        {
            UserId = assignee.Id,
            ScopeType = assigneeScope,
            RegionId = assigneeRegionId,
            FacilityId = assigneeFacilityId,
            FacilityUnitId = assigneeUnitId,
            IsActive = true
        });
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id, noteScope, noteRegionId, noteFacilityId, noteUnitId);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);

        var result = await service.AssignAsync(
            note.Id,
            new AssignNoteRequest(assignee.Id, null, null, "تكليف نطاق", RowVersionOf(note)));

        Assert.Equal(NoteStatus.Assigned, result.Status);
        Assert.Equal(assignee.Id, result.CurrentAssignment!.AssignedToUserId);
    }

    private async Task AssertAssignRejectedForAssigneeScopeAsync(
        ScopeType assigneeScope,
        Guid? assigneeRegionId,
        Guid? assigneeFacilityId,
        Guid? assigneeUnitId,
        ScopeType noteScope,
        Guid? noteRegionId,
        Guid? noteFacilityId,
        Guid? noteUnitId)
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var assignee = NoteTestFixtures.AddUser(_db, $"assignee-{Guid.NewGuid():N}");
        NoteTestFixtures.GrantPermissions(_db, assignee.Id, $"Worker-{assignee.Id}", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope
        {
            UserId = assignee.Id,
            ScopeType = assigneeScope,
            RegionId = assigneeRegionId,
            FacilityId = assigneeFacilityId,
            FacilityUnitId = assigneeUnitId,
            IsActive = true
        });
        _db.SaveChanges();
        var note = SeedNote(NoteStatus.Open, reporter.Id, noteScope, noteRegionId, noteFacilityId, noteUnitId);
        var service = BuildService(actor.Id, PermissionCodes.NotesAssign, PermissionCodes.NotesView);
        var auditsBefore = _db.AuditLogs.Count();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignAsync(
                note.Id,
                new AssignNoteRequest(assignee.Id, null, null, "تكليف مرفوض", RowVersionOf(note))));

        AssertNoAssignmentSideEffects(note.Id, NoteStatus.Open, auditsBefore);
    }

    private void AssertNoAssignmentSideEffects(Guid noteId, NoteStatus expectedStatus, int auditsBefore)
    {
        Assert.Empty(_db.NoteAssignments.Where(a => a.OperationalNoteId == noteId));
        Assert.Equal(expectedStatus, _db.OperationalNotes.Single(n => n.Id == noteId).Status);
        Assert.Empty(_db.NoteStatusHistories.Where(h => h.OperationalNoteId == noteId));
        Assert.Equal(auditsBefore, _db.AuditLogs.Count());
        Assert.DoesNotContain(_db.AuditLogs, a =>
            a.EntityId == noteId.ToString() &&
            (a.Action == "NoteAssigned" || a.Action == "NoteReassigned"));
    }

    private Department SeedDepartment()
    {
        if (!_db.Organizations.Any(o => o.Id == SeedIds.Organization))
        {
            _db.Organizations.Add(new Organization { Id = SeedIds.Organization, Code = "HQ", NameAr = "رئيسي" });
        }

        var department = new Department
        {
            Id = Guid.NewGuid(),
            OrganizationId = SeedIds.Organization,
            Code = $"DPT-{Guid.NewGuid():N}"[..12],
            NameAr = "إدارة الصيانة"
        };
        _db.Departments.Add(department);
        _db.SaveChanges();
        return department;
    }

    private static readonly Guid UnitA1East = Guid.Parse("44444444-4444-4444-4444-444444444401");

    private void SeedOrgGraph()
    {
        if (_db.Regions.Any(r => r.Id == SeedIds.RegionA))
        {
            return;
        }

        _db.Regions.AddRange(
            new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" },
            new Region { Id = SeedIds.RegionB, OrganizationId = SeedIds.Organization, Code = "B", NameAr = "ب" });
        _db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityA2, RegionId = SeedIds.RegionA, Code = "A2", NameAr = "أ2" });
        _db.FacilityUnits.Add(new FacilityUnit { Id = UnitA1East, FacilityId = SeedIds.FacilityA1, Code = "E", NameAr = "شرق" });
        _db.SaveChanges();
    }

    private OperationalNote SeedNote(
        NoteStatus status,
        Guid reporterId,
        ScopeType scopeType = ScopeType.Global,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? facilityUnitId = null)
    {
        if (scopeType is ScopeType.Region or ScopeType.Facility or ScopeType.FacilityUnit)
        {
            SeedOrgGraph();
        }

        var note = NoteTestFixtures.NewNote(
            scopeType,
            reporterId,
            regionId: regionId,
            facilityId: facilityId,
            facilityUnitId: facilityUnitId,
            status: status);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private static ICurrentUser FakeUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
}
