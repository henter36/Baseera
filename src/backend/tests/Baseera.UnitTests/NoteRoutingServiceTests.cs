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

public sealed class NoteRoutingServiceTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Submit_without_matching_rule_records_decision_and_keeps_note_open()
    {
        var (commands, actor, reporter) = BuildCommandService();
        var note = SeedNote(reporter);

        var result = await commands.SubmitAsync(note.Id, new TransitionNoteRequest("إرسال", Convert.ToBase64String(note.RowVersion)));

        Assert.Equal(NoteStatus.Open, result.Status);
        var decision = Assert.Single(_db.NoteRoutingDecisions);
        Assert.Equal(NoteRoutingResultStatus.NoMatchingRule, decision.ResultStatus);
        Assert.Empty(_db.NoteAssignments);
        Assert.Equal(actor, _db.NoteStatusHistories.Single(h => h.ToStatus == NoteStatus.Open).ChangedByUserId);
    }

    [Fact]
    public async Task Submit_with_department_rule_assigns_department_and_records_decision()
    {
        var (commands, _, reporter) = BuildCommandService();
        var department = new Department
        {
            OrganizationId = SeedOrganization().Id,
            Code = "OPS",
            NameAr = "التشغيل",
            IsActive = true
        };
        _db.Departments.Add(department);
        var note = SeedNote(reporter);
        _db.NoteRoutingRules.Add(new NoteRoutingRule
        {
            Code = "GLOBAL-OPS",
            NameAr = "توجيه التشغيل",
            NoteTypeId = note.NoteTypeId,
            ScopeType = ScopeType.Global,
            Priority = 10,
            ProcessingTargetType = NoteRoutingProcessingTargetType.Department,
            ProcessingDepartmentId = department.Id,
            AutoAssignOnSubmit = true,
            IsActive = true
        });
        _db.SaveChanges();

        var result = await commands.SubmitAsync(note.Id, new TransitionNoteRequest("إرسال", Convert.ToBase64String(note.RowVersion)));

        Assert.Equal(NoteStatus.Assigned, result.Status);
        var assignment = Assert.Single(_db.NoteAssignments);
        Assert.Equal(department.Id, assignment.AssignedToDepartmentId);
        Assert.True(assignment.IsCurrent);
        var decision = Assert.Single(_db.NoteRoutingDecisions);
        Assert.Equal(NoteRoutingResultStatus.AssignedToDepartment, decision.ResultStatus);
        Assert.Equal(decision.Id, assignment.RoutingDecisionId);
    }

    [Fact]
    public async Task Facility_specific_rule_wins_over_region_rule_even_with_higher_priority_number()
    {
        var (commands, _, reporter) = BuildCommandService();
        var org = SeedOrganization();
        var region = new Region { OrganizationId = org.Id, Code = "R1", NameAr = "منطقة", IsActive = true };
        _db.Regions.Add(region);
        var facility = new Facility { Region = region, Code = "F1", NameAr = "موقع", IsActive = true };
        _db.Facilities.Add(facility);
        var regionDepartment = new Department { OrganizationId = org.Id, Code = "REG", NameAr = "إقليمي", IsActive = true };
        var facilityDepartment = new Department { OrganizationId = org.Id, Code = "FAC", NameAr = "موقعي", IsActive = true };
        _db.Departments.AddRange(regionDepartment, facilityDepartment);
        var note = NoteTestFixtures.NewNote(ScopeType.Facility, reporter, region.Id, facility.Id, status: NoteStatus.Draft);
        _db.OperationalNotes.Add(note);
        _db.NoteRoutingRules.AddRange(
            new NoteRoutingRule
            {
                Code = "REGION",
                NameAr = "قاعدة منطقة",
                NoteTypeId = note.NoteTypeId,
                ScopeType = ScopeType.Region,
                RegionId = region.Id,
                Priority = 1,
                ProcessingTargetType = NoteRoutingProcessingTargetType.Department,
                ProcessingDepartmentId = regionDepartment.Id,
                AutoAssignOnSubmit = true,
                IsActive = true
            },
            new NoteRoutingRule
            {
                Code = "FACILITY",
                NameAr = "قاعدة موقع",
                NoteTypeId = note.NoteTypeId,
                ScopeType = ScopeType.Facility,
                RegionId = region.Id,
                FacilityId = facility.Id,
                Priority = 50,
                ProcessingTargetType = NoteRoutingProcessingTargetType.Department,
                ProcessingDepartmentId = facilityDepartment.Id,
                AutoAssignOnSubmit = true,
                IsActive = true
            });
        _db.SaveChanges();

        await commands.SubmitAsync(note.Id, new TransitionNoteRequest("إرسال", Convert.ToBase64String(note.RowVersion)));

        var assignment = Assert.Single(_db.NoteAssignments);
        Assert.Equal(facilityDepartment.Id, assignment.AssignedToDepartmentId);
        Assert.Equal("FACILITY", _db.NoteRoutingDecisions.IncludeRuleCode());
    }

    private (INoteCommandService Commands, Guid ActorId, Guid ReporterId) BuildCommandService()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        NoteTestFixtures.GrantPermissions(
            _db,
            actor.Id,
            "Actor",
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesView,
            PermissionCodes.NotesViewRouting,
            PermissionCodes.NotesRunRouting);
        var current = new FakeCurrentUser(true, actor.Id, actor.ExternalSubject, "actor", [
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesView,
            PermissionCodes.NotesViewRouting,
            PermissionCodes.NotesRunRouting
        ], [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var orgScope = new OrganizationalScopeService(current, _db);
        var noteScope = new NoteScopeService(orgScope, current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        var audit = new AuditService(_db, current, orgScope);
        var routing = new NoteRoutingService(_db, current, noteScope, typeAccess, audit, TimeProvider.System);
        var queries = new NoteQueryService(_db, current, noteScope, typeAccess, audit);
        return (new NoteCommandService(_db, current, noteScope, typeAccess, routing, audit, queries), actor.Id, reporter.Id);
    }

    private OperationalNote SeedNote(Guid reporterId)
    {
        var note = NoteTestFixtures.NewNote(ScopeType.Global, reporterId);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private Organization SeedOrganization()
    {
        var existing = _db.Organizations.FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var org = new Organization { Code = "HQ", NameAr = "الرئاسة", IsActive = true };
        _db.Organizations.Add(org);
        _db.SaveChanges();
        return org;
    }
}

internal static class NoteRoutingTestExtensions
{
    public static string? IncludeRuleCode(this IQueryable<NoteRoutingDecision> decisions) =>
        decisions.Select(decision => decision.RoutingRule!.Code).Single();
}
