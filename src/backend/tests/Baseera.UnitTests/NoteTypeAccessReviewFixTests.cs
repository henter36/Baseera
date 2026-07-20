using Baseera.Application.Notes;
using Baseera.Application.Abstractions;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests;

public sealed class NoteTypeAccessReviewFixTests : IDisposable
{
    private static readonly Guid FacilityA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private static readonly Guid UnitA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2");
    private static readonly Guid UnitB = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa3");
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Eligible_user_with_facility_unit_scope_does_not_match_other_unit()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var unitAUser = AddWorker("unit-a", UnitA);
        AddWorker("unit-b", UnitB);
        var note = NoteTestFixtures.NewNote(
            ScopeType.FacilityUnit,
            reporter.Id,
            facilityId: FacilityA,
            facilityUnitId: UnitB,
            status: NoteStatus.Open);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        var service = BuildEligibilityService(actor.Id);

        var result = await service.GetEligibleAssigneesAsync(note.Id);

        Assert.DoesNotContain(result, item => item.Id == unitAUser.Id);
    }

    [Fact]
    public async Task Eligible_user_with_facility_unit_scope_matches_facility_wide_note_only()
    {
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var unitAUser = AddWorker("unit-a", UnitA);
        var note = NoteTestFixtures.NewNote(
            ScopeType.Facility,
            reporter.Id,
            facilityId: FacilityA,
            status: NoteStatus.Open);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        var service = BuildEligibilityService(actor.Id);

        var result = await service.GetEligibleAssigneesAsync(note.Id);

        Assert.Contains(result, item => item.Id == unitAUser.Id);
    }

    [Fact]
    public async Task Batch_effective_access_preserves_role_union_and_user_override_decisions()
    {
        var roleOnly = NoteTestFixtures.AddUser(_db, "role-only");
        var denied = NoteTestFixtures.AddUser(_db, "denied");
        var directAllow = NoteTestFixtures.AddUser(_db, "direct-allow");
        NoteTestFixtures.GrantPermissions(_db, roleOnly.Id, "RoleOnly", PermissionCodes.NotesView);
        NoteTestFixtures.GrantPermissions(_db, denied.Id, "Denied", PermissionCodes.NotesView);
        _db.UserNoteTypeOverrides.AddRange(
            new UserNoteTypeOverride
            {
                UserId = denied.Id,
                NoteTypeId = NoteTestFixtures.DefaultNoteTypeId,
                CanViewOverride = false,
                IsActive = true,
                Reason = "منع مباشر"
            },
            new UserNoteTypeOverride
            {
                UserId = directAllow.Id,
                NoteTypeId = NoteTestFixtures.DefaultNoteTypeId,
                CanViewOverride = true,
                IsActive = true,
                Reason = "سماح مباشر"
            });
        _db.SaveChanges();
        var service = new NoteTypeAccessService(_db, FakeUser(roleOnly.Id, PermissionCodes.NotesView));

        var access = await service.GetEffectiveAccessForUsersAsync(
            [roleOnly.Id, denied.Id, directAllow.Id],
            NoteTestFixtures.DefaultNoteTypeId);

        Assert.True(access[roleOnly.Id]!.View.Allowed);
        Assert.Equal("Role", access[roleOnly.Id]!.View.Source);
        Assert.False(access[denied.Id]!.View.Allowed);
        Assert.Equal("Direct Deny", access[denied.Id]!.View.Source);
        Assert.True(access[directAllow.Id]!.View.Allowed);
        Assert.Equal("Direct Allow", access[directAllow.Id]!.View.Source);
    }

    [Fact]
    public async Task Existing_intake_profile_update_requires_rowversion()
    {
        var admin = NoteTestFixtures.AddUser(_db, "admin");
        var target = NoteTestFixtures.AddUser(_db, "target");
        _db.UserNoteIntakeProfiles.Add(new UserNoteIntakeProfile
        {
            UserId = target.Id,
            LockType = NoteIntakeLockType.None,
            IsActive = true,
            RowVersion = [1, 2, 3]
        });
        _db.SaveChanges();
        var service = BuildManagementService(admin.Id);

        var request = new UpdateUserNoteIntakeProfileRequest(
            NoteIntakeLockType.None,
            null,
            null,
            "تغيير سياق الإدخال",
            null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateIntakeProfileAsync(target.Id, request));
    }

    private User AddWorker(string name, Guid unitId)
    {
        var user = NoteTestFixtures.AddUser(_db, name);
        NoteTestFixtures.GrantPermissions(_db, user.Id, $"Worker-{name}", PermissionCodes.NotesStartWork);
        _db.UserScopes.Add(new UserScope
        {
            UserId = user.Id,
            ScopeType = ScopeType.FacilityUnit,
            FacilityId = FacilityA,
            FacilityUnitId = unitId,
            IsActive = true
        });
        _db.SaveChanges();
        return user;
    }

    private NoteEligibilityService BuildEligibilityService(Guid actorId)
    {
        NoteTestFixtures.GrantPermissions(_db, actorId, $"Actor-{actorId}", PermissionCodes.NotesView);
        var current = FakeUser(actorId, PermissionCodes.NotesView);
        var noteScope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        return new NoteEligibilityService(_db, current, noteScope, new NoteTypeAccessService(_db, current));
    }

    private NoteTypeManagementService BuildManagementService(Guid adminId)
    {
        NoteTestFixtures.GrantPermissions(_db, adminId, $"Admin-{adminId}", PermissionCodes.NotesManageIntakeProfiles);
        var current = FakeUser(adminId, PermissionCodes.NotesManageIntakeProfiles);
        var orgScope = new OrganizationalScopeService(current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        var audit = new AuditService(_db, current, orgScope);
        return new NoteTypeManagementService(_db, current, orgScope, typeAccess, audit);
    }

    private static ICurrentUser FakeUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
}
