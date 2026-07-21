using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class NoteRoutingPr8StructuralFixTests : IDisposable
{
    private static readonly Guid RegionA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
    private static readonly Guid RegionB = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2");
    private static readonly Guid FacilityA = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1");
    private static readonly Guid FacilityA2 = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb3");
    private static readonly Guid FacilityB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb2");
    private static readonly Guid UnitA = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc1");
    private static readonly Guid UnitB = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc2");

    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public NoteRoutingPr8StructuralFixTests()
    {
        EnsureDefaultNoteType();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ResolveIntakeAsync_resolves_facility_in_user_scope()
    {
        SeedOrgGraph();
        var user = NoteTestFixtures.AddUser(_db, "creator");
        AddScope(user.Id, ScopeType.Facility, RegionA, FacilityA);
        var scopeService = BuildNoteScopeService(user.Id, ScopeType.Facility, RegionA, FacilityA);

        var intake = await scopeService.ResolveIntakeAsync(user.Id, RegionA, FacilityA);

        Assert.Equal(RegionA, intake.RegionId);
        Assert.Equal(FacilityA, intake.FacilityId);
    }

    [Fact]
    public async Task ResolveIntakeAsync_rejects_facility_outside_user_scope()
    {
        SeedOrgGraph();
        var user = NoteTestFixtures.AddUser(_db, "creator");
        AddScope(user.Id, ScopeType.Facility, RegionA, FacilityA);
        var scopeService = BuildNoteScopeService(user.Id, ScopeType.Facility, RegionA, FacilityA);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scopeService.ResolveIntakeAsync(user.Id, RegionB, FacilityB));
    }

    [Fact]
    public async Task ResolveIntakeAsync_enforces_facility_intake_lock()
    {
        SeedOrgGraph();
        var user = NoteTestFixtures.AddUser(_db, "creator");
        _db.UserNoteIntakeProfiles.Add(new UserNoteIntakeProfile
        {
            UserId = user.Id,
            LockType = NoteIntakeLockType.Facility,
            FacilityId = FacilityA,
            IsActive = true
        });
        _db.SaveChanges();
        var scopeService = BuildNoteScopeService(user.Id, ScopeType.Global);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scopeService.ResolveIntakeAsync(user.Id, RegionA, FacilityA2));
    }

    [Theory]
    [InlineData(ScopeType.Region)]
    [InlineData(ScopeType.MultipleRegions)]
    public void Routing_intersection_supports_region_scopes(
        ScopeType scopeType)
    {
        var scope = new UserScope
        {
            UserId = Guid.NewGuid(),
            ScopeType = scopeType,
            RegionId = RegionA,
            IsActive = true
        };

        var matchingNote = new OperationalNote
        {
            ScopeType = ScopeType.Region,
            RegionId = RegionA
        };

        var siblingNote = new OperationalNote
        {
            ScopeType = ScopeType.Region,
            RegionId = RegionB
        };

        Assert.True(
            NoteAssigneeScopeIntersection
                .IntersectsUserScopeForRouting(
                    scope,
                    matchingNote));

        Assert.False(
            NoteAssigneeScopeIntersection
                .IntersectsUserScopeForRouting(
                    scope,
                    siblingNote));
    }

    [Theory]
    [InlineData(ScopeType.Facility)]
    [InlineData(ScopeType.MultipleFacilities)]
    public void Routing_intersection_supports_facility_scopes(
        ScopeType scopeType)
    {
        var scope = new UserScope
        {
            UserId = Guid.NewGuid(),
            ScopeType = scopeType,
            RegionId = RegionA,
            FacilityId = FacilityA,
            IsActive = true
        };

        var matchingNote = new OperationalNote
        {
            ScopeType = ScopeType.Facility,
            RegionId = RegionA,
            FacilityId = FacilityA
        };

        var siblingNote = new OperationalNote
        {
            ScopeType = ScopeType.Facility,
            RegionId = RegionB,
            FacilityId = FacilityB
        };

        Assert.True(
            NoteAssigneeScopeIntersection
                .IntersectsUserScopeForRouting(
                    scope,
                    matchingNote));

        Assert.False(
            NoteAssigneeScopeIntersection
                .IntersectsUserScopeForRouting(
                    scope,
                    siblingNote));
    }

    [Fact]
    public async Task ResolveIntakeAsync_rejects_soft_deleted_facility()
    {
        SeedOrgGraph();

        var user = NoteTestFixtures.AddUser(
            _db,
            "archived-facility-user");

        AddScope(
            user.Id,
            ScopeType.Facility,
            RegionA,
            FacilityA);

        var facility = _db.Facilities.Single(
            item => item.Id == FacilityA);

        facility.IsDeleted = true;
        facility.DeletedAtUtc =
            DateTimeOffset.UtcNow;

        _db.SaveChanges();

        var scopeService = BuildNoteScopeService(
            user.Id,
            ScopeType.Facility,
            RegionA,
            FacilityA);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => scopeService.ResolveIntakeAsync(
                user.Id,
                RegionA,
                FacilityA));
    }

    [Fact]
    public async Task CreateRule_rejects_invalid_processing_target_shape()
    {
        var admin = NoteTestFixtures.AddUser(_db, "admin");
        var service = BuildRoutingService(admin.Id, ScopeType.Global);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRuleAsync(new CreateNoteRoutingRuleRequest(
            "BAD-TARGET",
            "هدف غير صالح",
            null,
            NoteTestFixtures.DefaultNoteTypeId,
            ScopeType.Global,
            null,
            null,
            null,
            10,
            NoteRoutingProcessingTargetType.Department,
            null,
            Guid.NewGuid(),
            null,
            null,
            true,
            false,
            "اختبار")));
    }

    [Fact]
    public async Task ActivateRule_rejects_ambiguous_active_rule()
    {
        var admin = NoteTestFixtures.AddUser(_db, "admin");
        var department = SeedDepartment();
        var service = BuildRoutingService(admin.Id, ScopeType.Global);
        var first = await service.CreateRuleAsync(GlobalDepartmentRule("RULE-A", department.Id, 10));
        var second = await service.CreateRuleAsync(GlobalDepartmentRule("RULE-B", department.Id, 10));
        await service.ActivateRuleAsync(first.Id, new TransitionNoteRequest("تفعيل", first.RowVersion));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ActivateRuleAsync(second.Id, new TransitionNoteRequest("تفعيل", second.RowVersion)));
    }

    [Fact]
    public async Task ReplaceRoleGrants_records_new_update_revoke_reactivate_and_noop()
    {
        var admin = NoteTestFixtures.AddUser(_db, "admin");
        var role = new Role { Code = "GRANT-ROLE", NameAr = "دور", IsSystem = true };
        _db.Roles.Add(role);
        _db.SaveChanges();
        var service = BuildManagementService(admin.Id);
        var typeId = NoteTestFixtures.DefaultNoteTypeId;
        var grant = FullGrant(typeId, canProcess: false);

        await service.ReplaceRoleGrantsAsync(role.Id, new ReplaceRoleNoteTypeGrantsRequest([grant], "منح جديد"));
        Assert.Contains(_db.NoteTypeAccessChangeHistories, row => row.ChangeType == NoteTypeAccessChangeType.Granted);

        await service.ReplaceRoleGrantsAsync(role.Id, new ReplaceRoleNoteTypeGrantsRequest([FullGrant(typeId, canProcess: true)], "تحديث"));
        Assert.Contains(_db.NoteTypeAccessChangeHistories, row => row.ChangeType == NoteTypeAccessChangeType.Updated);

        await service.ReplaceRoleGrantsAsync(role.Id, new ReplaceRoleNoteTypeGrantsRequest([], "إلغاء"));
        Assert.Contains(_db.NoteTypeAccessChangeHistories, row => row.ChangeType == NoteTypeAccessChangeType.Revoked);

        var historyCount = _db.NoteTypeAccessChangeHistories.Count();
        await service.ReplaceRoleGrantsAsync(role.Id, new ReplaceRoleNoteTypeGrantsRequest([FullGrant(typeId)], "إعادة تفعيل"));
        Assert.True(_db.RoleNoteTypeGrants.Single(g => g.RoleId == role.Id && g.NoteTypeId == typeId).IsActive);
        Assert.True(_db.NoteTypeAccessChangeHistories.Count() > historyCount);

        historyCount = _db.NoteTypeAccessChangeHistories.Count();
        await service.ReplaceRoleGrantsAsync(role.Id, new ReplaceRoleNoteTypeGrantsRequest([FullGrant(typeId)], "بدون تغيير"));
        Assert.Equal(historyCount, _db.NoteTypeAccessChangeHistories.Count());
    }

    [Fact]
    public async Task ReplaceUserOverrides_records_allow_deny_update_remove_and_noop()
    {
        var admin = NoteTestFixtures.AddUser(_db, "admin");
        var target = NoteTestFixtures.AddUser(_db, "target");
        var service = BuildManagementService(admin.Id);
        var typeId = NoteTestFixtures.DefaultNoteTypeId;

        await service.ReplaceUserOverridesAsync(target.Id, new ReplaceUserNoteTypeOverridesRequest([
            new ReplaceUserNoteTypeOverrideItem(typeId, true, null, null, null, null, null, null, null, null, null)
        ], "سماح مباشر"));
        Assert.Contains(_db.NoteTypeAccessChangeHistories, row => row.ChangeType == NoteTypeAccessChangeType.DirectAllowAdded);

        await service.ReplaceUserOverridesAsync(target.Id, new ReplaceUserNoteTypeOverridesRequest([
            new ReplaceUserNoteTypeOverrideItem(typeId, false, null, null, null, null, null, null, null, null, null)
        ], "منع مباشر"));
        Assert.Contains(_db.NoteTypeAccessChangeHistories, row => row.ChangeType == NoteTypeAccessChangeType.DirectDenyAdded);

        await service.ReplaceUserOverridesAsync(target.Id, new ReplaceUserNoteTypeOverridesRequest([
            new ReplaceUserNoteTypeOverrideItem(typeId, true, true, null, null, null, null, null, null, null, null)
        ], "تحديث"));
        Assert.Contains(_db.NoteTypeAccessChangeHistories, row => row.ChangeType == NoteTypeAccessChangeType.Updated);

        await service.ReplaceUserOverridesAsync(target.Id, new ReplaceUserNoteTypeOverridesRequest([], "إزالة"));
        Assert.Contains(_db.NoteTypeAccessChangeHistories, row => row.ChangeType == NoteTypeAccessChangeType.OverrideRemoved);

        var historyCount = _db.NoteTypeAccessChangeHistories.Count();
        await service.ReplaceUserOverridesAsync(target.Id, new ReplaceUserNoteTypeOverridesRequest([
            new ReplaceUserNoteTypeOverrideItem(typeId, true, null, null, null, null, null, null, null, null, null)
        ], "بدون تغيير"));
        await service.ReplaceUserOverridesAsync(target.Id, new ReplaceUserNoteTypeOverridesRequest([
            new ReplaceUserNoteTypeOverrideItem(typeId, true, null, null, null, null, null, null, null, null, null)
        ], "بدون تغيير"));
        Assert.Equal(historyCount + 1, _db.NoteTypeAccessChangeHistories.Count());
    }

    [Fact]
    public async Task ListRulesAsync_applies_geographic_scope_isolation()
    {
        SeedOrgGraph();
        var department = SeedDepartment();
        var globalAdmin = NoteTestFixtures.AddUser(_db, "global-admin");
        var regionAdmin = NoteTestFixtures.AddUser(_db, "region-admin");
        var facilityAdmin = NoteTestFixtures.AddUser(_db, "facility-admin");
        var unitAdmin = NoteTestFixtures.AddUser(_db, "unit-admin");
        AddScope(regionAdmin.Id, ScopeType.Region, RegionA);
        AddScope(facilityAdmin.Id, ScopeType.Facility, RegionA, FacilityA);
        AddScope(unitAdmin.Id, ScopeType.FacilityUnit, RegionA, FacilityA, UnitA);

        var globalService = BuildRoutingService(globalAdmin.Id, ScopeType.Global);
        var regionRule = await globalService.CreateRuleAsync(RegionDepartmentRule("REG-A", RegionA, department.Id));
        var siblingRule = await globalService.CreateRuleAsync(RegionDepartmentRule("REG-B", RegionB, department.Id));
        var facilityRule = await globalService.CreateRuleAsync(FacilityDepartmentRule("FAC-A", RegionA, FacilityA, department.Id));
        var unitRule = await globalService.CreateRuleAsync(UnitDepartmentRule("UNIT-A", RegionA, FacilityA, UnitA, department.Id));
        var hqRule = await globalService.CreateRuleAsync(GlobalDepartmentRule("HQ", department.Id, 20));
        await globalService.ActivateRuleAsync(regionRule.Id, new TransitionNoteRequest("تفعيل", regionRule.RowVersion));
        await globalService.ActivateRuleAsync(siblingRule.Id, new TransitionNoteRequest("تفعيل", siblingRule.RowVersion));
        await globalService.ActivateRuleAsync(facilityRule.Id, new TransitionNoteRequest("تفعيل", facilityRule.RowVersion));
        await globalService.ActivateRuleAsync(unitRule.Id, new TransitionNoteRequest("تفعيل", unitRule.RowVersion));
        await globalService.ActivateRuleAsync(hqRule.Id, new TransitionNoteRequest("تفعيل", hqRule.RowVersion));

        var globalList = await globalService.ListRulesAsync(new NoteRoutingRuleQuery());
        Assert.Equal(5, globalList.TotalCount);

        var regionList = await BuildRoutingService(regionAdmin.Id, ScopeType.Region, RegionA).ListRulesAsync(new NoteRoutingRuleQuery());
        Assert.Contains(regionList.Items, item => item.Code == "REG-A");
        Assert.DoesNotContain(regionList.Items, item => item.Code == "REG-B");
        Assert.Contains(regionList.Items, item => item.Code == "FAC-A");
        Assert.Contains(regionList.Items, item => item.Code == "UNIT-A");

        var facilityList = await BuildRoutingService(facilityAdmin.Id, ScopeType.Facility, RegionA, FacilityA).ListRulesAsync(new NoteRoutingRuleQuery());
        Assert.DoesNotContain(facilityList.Items, item => item.Code == "REG-A");
        Assert.Contains(facilityList.Items, item => item.Code == "FAC-A");
        Assert.Contains(facilityList.Items, item => item.Code == "UNIT-A");

        var unitList = await BuildRoutingService(unitAdmin.Id, ScopeType.FacilityUnit, RegionA, FacilityA, UnitA).ListRulesAsync(new NoteRoutingRuleQuery());
        Assert.Single(unitList.Items);
        Assert.Equal("UNIT-A", unitList.Items[0].Code);

        var outOfScope = await BuildRoutingService(regionAdmin.Id, ScopeType.Region, RegionA).GetRuleAsync(siblingRule.Id);
        Assert.Null(outOfScope);
    }

    [Fact]
    public async Task Sensitive_note_routing_excludes_users_without_view_sensitive_permission()
    {
        SeedOrgGraph();
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var processingRole = new Role { Code = "PROC", NameAr = "معالجة", IsSystem = true };
        _db.Roles.Add(processingRole);
        var allowed = AddRoutingWorker("allowed", processingRole.Id, ScopeType.Facility, RegionA, FacilityA, includeSensitive: true);
        var blocked = AddRoutingWorker("blocked", processingRole.Id, ScopeType.Facility, RegionA, FacilityA, includeSensitive: false);
        var department = SeedDepartment();
        _db.NoteRoutingRules.Add(new NoteRoutingRule
        {
            Code = "SENSITIVE-ROLE",
            NameAr = "توجيه حساس",
            NoteTypeId = NoteTestFixtures.DefaultNoteTypeId,
            ScopeType = ScopeType.Facility,
            RegionId = RegionA,
            FacilityId = FacilityA,
            Priority = 1,
            ProcessingTargetType = NoteRoutingProcessingTargetType.Role,
            ProcessingRoleId = processingRole.Id,
            AutoAssignOnSubmit = true,
            IsActive = true
        });
        var note = NoteTestFixtures.NewNote(
            ScopeType.Facility,
            reporter.Id,
            RegionA,
            FacilityA,
            classification: ClassificationLevel.Confidential,
            status: NoteStatus.Draft);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var commands = BuildCommandService(actor.Id, ScopeType.Global);
        await commands.SubmitAsync(note.Id, new TransitionNoteRequest("إرسال", Convert.ToBase64String(note.RowVersion)));

        var assignment = Assert.Single(_db.NoteAssignments);
        Assert.Equal(allowed.Id, assignment.AssignedToUserId);
        Assert.NotEqual(blocked.Id, assignment.AssignedToUserId);
    }

    [Fact]
    public async Task Facility_wide_note_is_not_auto_assigned_to_facility_unit_only_user()
    {
        SeedOrgGraph();

        var reporter = NoteTestFixtures.AddUser(
            _db,
            "reporter");

        var actor = NoteTestFixtures.AddUser(
            _db,
            "actor");

        var processingRole = new Role
        {
            Code = "UNIT-PROC",
            NameAr = "معالجة وحدة",
            IsSystem = true
        };

        _db.Roles.Add(processingRole);

        AddRoutingWorker(
            "unit-worker",
            processingRole.Id,
            ScopeType.FacilityUnit,
            RegionA,
            FacilityA,
            UnitA);

        _db.NoteRoutingRules.Add(new NoteRoutingRule
        {
            Code = "FAC-WIDE",
            NameAr = "توجيه موقع",
            NoteTypeId =
                NoteTestFixtures.DefaultNoteTypeId,
            ScopeType = ScopeType.Facility,
            RegionId = RegionA,
            FacilityId = FacilityA,
            Priority = 1,
            ProcessingTargetType =
                NoteRoutingProcessingTargetType.Role,
            ProcessingRoleId = processingRole.Id,
            AutoAssignOnSubmit = true,
            IsActive = true
        });

        var note = NoteTestFixtures.NewNote(
            ScopeType.Facility,
            reporter.Id,
            RegionA,
            FacilityA,
            status: NoteStatus.Draft);

        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var commands = BuildCommandService(
            actor.Id,
            ScopeType.Global);

        await commands.SubmitAsync(
            note.Id,
            new TransitionNoteRequest(
                "إرسال",
                Convert.ToBase64String(note.RowVersion)));

        Assert.Empty(_db.NoteAssignments);

        var decision = Assert.Single(
            _db.NoteRoutingDecisions);

        Assert.Equal(
            NoteRoutingResultStatus.NoEligibleUser,
            decision.ResultStatus);

        Assert.Null(decision.ResolvedUserId);
    }

    [Fact]
    public async Task Role_routing_selects_one_eligible_user_from_multiple_candidates()
    {
        SeedOrgGraph();
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var processingRole = new Role
        {
            Code = "BATCH-PROC",
            NameAr = "دفعة",
            IsSystem = true
        };

        _db.Roles.Add(processingRole);

        var eligibleCandidateIds = new HashSet<Guid>();

        for (var i = 0; i < 5; i++)
        {
            var candidate = AddRoutingWorker(
                $"worker-{i}",
                processingRole.Id,
                ScopeType.Facility,
                RegionA,
                FacilityA);

            eligibleCandidateIds.Add(candidate.Id);
        }

        _db.NoteRoutingRules.Add(new NoteRoutingRule
        {
            Code = "BATCH",
            NameAr = "توجيه دفعة",
            NoteTypeId = NoteTestFixtures.DefaultNoteTypeId,
            ScopeType = ScopeType.Facility,
            RegionId = RegionA,
            FacilityId = FacilityA,
            Priority = 1,
            ProcessingTargetType = NoteRoutingProcessingTargetType.Role,
            ProcessingRoleId = processingRole.Id,
            AutoAssignOnSubmit = true,
            IsActive = true
        });
        var note = NoteTestFixtures.NewNote(ScopeType.Facility, reporter.Id, RegionA, FacilityA, status: NoteStatus.Draft);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();

        var commands = BuildCommandService(actor.Id, ScopeType.Global);
        await commands.SubmitAsync(note.Id, new TransitionNoteRequest("إرسال", Convert.ToBase64String(note.RowVersion)));

        var assignment = Assert.Single(
            _db.NoteAssignments);

        Assert.True(
            assignment.AssignedToUserId.HasValue);

        Assert.Contains(
            assignment.AssignedToUserId.Value,
            eligibleCandidateIds);
    }

    private INoteScopeService BuildNoteScopeService(
        Guid userId,
        ScopeType scopeType,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? unitId = null)
    {
        var current = FakeUser(userId, ScopeSnapshot(scopeType, regionId, facilityId, unitId));
        return new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
    }

    private INoteRoutingService BuildRoutingService(
        Guid userId,
        ScopeType scopeType,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? unitId = null,
        BaseeraDbContext? db = null)
    {
        db ??= _db;
        var current = FakeUser(
            userId,
            ScopeSnapshot(scopeType, regionId, facilityId, unitId),
            PermissionCodes.NotesViewRouting,
            PermissionCodes.NotesManageRoutingRules,
            PermissionCodes.NotesActivateRoutingRules);
        NoteTestFixtures.GrantPermissions(
            _db,
            userId,
            $"Routing-{userId}",
            PermissionCodes.NotesViewRouting,
            PermissionCodes.NotesManageRoutingRules,
            PermissionCodes.NotesActivateRoutingRules);
        var noteScope = new NoteScopeService(new OrganizationalScopeService(current, db), current, db);
        var typeAccess = new NoteTypeAccessService(db, current);
        var audit = new AuditService(db, current, new OrganizationalScopeService(current, db));
        return new NoteRoutingService(db, current, noteScope, typeAccess, audit, TimeProvider.System);
    }

    private NoteTypeManagementService BuildManagementService(Guid adminId)
    {
        NoteTestFixtures.GrantPermissions(
            _db,
            adminId,
            $"Admin-{adminId}",
            PermissionCodes.NotesManageRoleTypeAccess,
            PermissionCodes.NotesManageUserTypeOverrides);
        var current = FakeUser(
            adminId,
            ScopeSnapshot(ScopeType.Global),
            PermissionCodes.NotesManageRoleTypeAccess,
            PermissionCodes.NotesManageUserTypeOverrides);
        var orgScope = new OrganizationalScopeService(current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        var audit = new AuditService(_db, current, orgScope);
        return new NoteTypeManagementService(_db, current, orgScope, typeAccess, audit);
    }

    private INoteCommandService BuildCommandService(
        Guid actorId,
        ScopeType scopeType,
        BaseeraDbContext? db = null,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? unitId = null)
    {
        db ??= _db;
        var current = FakeUser(
            actorId,
            ScopeSnapshot(scopeType, regionId, facilityId, unitId),
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesView,
            PermissionCodes.NotesCreate);
        NoteTestFixtures.GrantPermissions(_db, actorId, $"Actor-{actorId}", PermissionCodes.NotesUpdate, PermissionCodes.NotesView, PermissionCodes.NotesCreate);
        var noteScope = new NoteScopeService(new OrganizationalScopeService(current, db), current, db);
        var typeAccess = new NoteTypeAccessService(db, current);
        var audit = new AuditService(db, current, new OrganizationalScopeService(current, db));
        var routing = new NoteRoutingService(db, current, noteScope, typeAccess, audit, TimeProvider.System);
        var queries = new NoteQueryService(db, current, noteScope, typeAccess, audit);
        return new NoteCommandService(db, current, noteScope, typeAccess, routing, audit, queries);
    }

    private User AddRoutingWorker(
        string name,
        Guid roleId,
        ScopeType scopeType,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? unitId = null,
        bool includeSensitive = true)
    {
        var user = NoteTestFixtures.AddUser(_db, name);
        var permissions = new List<string> { PermissionCodes.NotesStartWork, PermissionCodes.NotesView, PermissionCodes.NotesCreate };
        if (includeSensitive)
        {
            permissions.Add(PermissionCodes.NotesViewSensitive);
        }

        NoteTestFixtures.GrantPermissions(_db, user.Id, $"Worker-{name}", [.. permissions]);
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        AddScope(user.Id, scopeType, regionId, facilityId, unitId);
        _db.SaveChanges();
        return user;
    }

    private static ReplaceRoleNoteTypeGrantItem FullGrant(Guid noteTypeId, bool canProcess = true) =>
        new(noteTypeId, true, true, true, canProcess, true, true, true, true, true, true);

    private static CreateNoteRoutingRuleRequest GlobalDepartmentRule(string code, Guid departmentId, int priority = 10) =>
        new(code, code, null, NoteTestFixtures.DefaultNoteTypeId, ScopeType.Global, null, null, null, priority,
            NoteRoutingProcessingTargetType.Department, departmentId, null, null, null, true, false, "اختبار");

    private static CreateNoteRoutingRuleRequest RegionDepartmentRule(string code, Guid regionId, Guid departmentId) =>
        new(code, code, null, NoteTestFixtures.DefaultNoteTypeId, ScopeType.Region, regionId, null, null, 10,
            NoteRoutingProcessingTargetType.Department, departmentId, null, null, null, true, false, "اختبار");

    private static CreateNoteRoutingRuleRequest FacilityDepartmentRule(string code, Guid regionId, Guid facilityId, Guid departmentId) =>
        new(code, code, null, NoteTestFixtures.DefaultNoteTypeId, ScopeType.Facility, regionId, facilityId, null, 10,
            NoteRoutingProcessingTargetType.Department, departmentId, null, null, null, true, false, "اختبار");

    private static CreateNoteRoutingRuleRequest UnitDepartmentRule(
        string code,
        Guid regionId,
        Guid facilityId,
        Guid unitId,
        Guid departmentId) =>
        new(code, code, null, NoteTestFixtures.DefaultNoteTypeId, ScopeType.FacilityUnit, regionId, facilityId, unitId, 10,
            NoteRoutingProcessingTargetType.Department, departmentId, null, null, null, true, false, "اختبار");

    private Department SeedDepartment()
    {
        var organization = SeedOrganization();

        var existing = _db.Departments.FirstOrDefault(
            department =>
                department.OrganizationId == organization.Id &&
                department.Code == "OPS");

        if (existing is not null)
        {
            return existing;
        }

        var department = new Department
        {
            OrganizationId = organization.Id,
            Code = "OPS",
            NameAr = "تشغيل",
            IsActive = true
        };

        _db.Departments.Add(department);
        _db.SaveChanges();
        return department;
    }

    private Organization SeedOrganization()
    {
        var existing = _db.Organizations.FirstOrDefault(
            organization => organization.Code == "HQ");

        if (existing is not null)
        {
            return existing;
        }

        var organization = new Organization
        {
            Code = "HQ",
            NameAr = "الرئاسة",
            IsActive = true
        };

        _db.Organizations.Add(organization);
        _db.SaveChanges();
        return organization;
    }

    private void SeedOrgGraph()
    {
        var org = SeedOrganization();
        _db.Regions.AddRange(
            new Region { Id = RegionA, OrganizationId = org.Id, Code = "RA", NameAr = "منطقة أ", IsActive = true },
            new Region { Id = RegionB, OrganizationId = org.Id, Code = "RB", NameAr = "منطقة ب", IsActive = true });
        _db.Facilities.AddRange(
            new Facility { Id = FacilityA, RegionId = RegionA, Code = "FA", NameAr = "موقع أ", IsActive = true },
            new Facility { Id = FacilityA2, RegionId = RegionA, Code = "FA2", NameAr = "موقع أ-2", IsActive = true },
            new Facility { Id = FacilityB, RegionId = RegionB, Code = "FB", NameAr = "موقع ب", IsActive = true });
        _db.FacilityUnits.AddRange(
            new FacilityUnit { Id = UnitA, FacilityId = FacilityA, Code = "UA", NameAr = "وحدة أ", IsActive = true },
            new FacilityUnit { Id = UnitB, FacilityId = FacilityA, Code = "UB", NameAr = "وحدة ب", IsActive = true });
        _db.SaveChanges();
    }

    private void AddScope(
        Guid userId,
        ScopeType scopeType,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? unitId = null)
    {
        _db.UserScopes.Add(new UserScope
        {
            UserId = userId,
            ScopeType = scopeType,
            RegionId = regionId,
            FacilityId = facilityId,
            FacilityUnitId = unitId,
            IsActive = true
        });
        _db.SaveChanges();
    }

    private static UserScopeSnapshot ScopeSnapshot(
        ScopeType scopeType,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? unitId = null) =>
        new(scopeType, regionId, facilityId, unitId);

    private static ICurrentUser FakeUser(Guid userId, UserScopeSnapshot scope, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [scope]);

    private void EnsureDefaultNoteType()
    {
        if (_db.NoteTypes.Any(type => type.Id == NoteTestFixtures.DefaultNoteTypeId))
        {
            return;
        }

        _db.NoteTypes.Add(new NoteType
        {
            Id = NoteTestFixtures.DefaultNoteTypeId,
            Code = "OPERATIONAL",
            NameAr = "تشغيلية",
            IsActive = true,
            SortOrder = 30,
            DefaultSeverity = NoteSeverity.Medium
        });
        _db.SaveChanges();
    }
}
