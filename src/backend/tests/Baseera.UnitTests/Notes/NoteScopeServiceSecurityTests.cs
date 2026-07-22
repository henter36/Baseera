using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Notes;

/// <summary>
/// Regression coverage for Notes scope expansion: facilities expand only from
/// directly granted regions (no Facility → Region → sibling Facilities closure).
/// </summary>
public sealed class NoteScopeServiceSecurityTests
{
    private static readonly Guid UnitA1 = Guid.Parse("44444444-4444-4444-4444-444444444411");
    private static readonly Guid UnitA2 = Guid.Parse("44444444-4444-4444-4444-444444444412");

    [Fact]
    public async Task Facility_scope_does_not_leak_sibling_facilities_or_routing_rules()
    {
        await using var db = NoteTestFixtures.CreateDb();
        SeedOrgGraph(db);
        var reporter = NoteTestFixtures.AddUser(db);

        var noteA1 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA1, reference: "OBS-00000001");
        var noteA2 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA2, reference: "OBS-00000002");
        var noteB1 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityB1, reference: "OBS-00000003");
        db.OperationalNotes.AddRange(noteA1, noteA2, noteB1);

        var ruleA1 = NewRule("RULE-A1", ScopeType.Facility, facilityId: SeedIds.FacilityA1);
        var ruleA2 = NewRule("RULE-A2", ScopeType.Facility, facilityId: SeedIds.FacilityA2);
        db.NoteRoutingRules.AddRange(ruleA1, ruleA2);
        await db.SaveChangesAsync();

        var scope = CreateService(db, new UserScopeSnapshot(
            ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        var syncNotes = scope.FilterQueryable(db.OperationalNotes).Select(n => n.Id).OrderBy(id => id).ToList();
        var asyncNotes = (await scope.FilterQueryableAsync(db.OperationalNotes))
            .Select(n => n.Id).OrderBy(id => id).ToList();
        Assert.Equal(syncNotes, asyncNotes);
        Assert.Equal([noteA1.Id], syncNotes);

        var syncRules = scope.FilterRoutingRulesQueryable(db.NoteRoutingRules)
            .Select(r => r.Id).OrderBy(id => id).ToList();
        var asyncRules = (await scope.FilterRoutingRulesQueryableAsync(db.NoteRoutingRules))
            .Select(r => r.Id).OrderBy(id => id).ToList();
        Assert.Equal(syncRules, asyncRules);
        Assert.Equal([ruleA1.Id], syncRules);
        Assert.DoesNotContain(ruleA2.Id, syncRules);
    }

    [Fact]
    public async Task FacilityUnit_scope_isolates_units_and_sibling_facilities()
    {
        await using var db = NoteTestFixtures.CreateDb();
        SeedOrgGraph(db);
        SeedUnits(db);
        var reporter = NoteTestFixtures.AddUser(db);

        var unitA1Note = NoteTestFixtures.NewNote(
            ScopeType.FacilityUnit,
            reporter.Id,
            facilityId: SeedIds.FacilityA1,
            facilityUnitId: UnitA1,
            reference: "OBS-00000001");
        var unitA2Note = NoteTestFixtures.NewNote(
            ScopeType.FacilityUnit,
            reporter.Id,
            facilityId: SeedIds.FacilityA1,
            facilityUnitId: UnitA2,
            reference: "OBS-00000002");
        var facilityA1Note = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA1, reference: "OBS-00000003");
        var facilityA2Note = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA2, reference: "OBS-00000004");
        var regionANote = NoteTestFixtures.NewNote(
            ScopeType.Region, reporter.Id, regionId: SeedIds.RegionA, reference: "OBS-00000005");
        db.OperationalNotes.AddRange(unitA1Note, unitA2Note, facilityA1Note, facilityA2Note, regionANote);

        var ruleUnitA1 = NewRule(
            "RULE-UA1", ScopeType.FacilityUnit, facilityId: SeedIds.FacilityA1, facilityUnitId: UnitA1);
        var ruleUnitA2 = NewRule(
            "RULE-UA2", ScopeType.FacilityUnit, facilityId: SeedIds.FacilityA1, facilityUnitId: UnitA2);
        var ruleFacilityA2 = NewRule("RULE-A2", ScopeType.Facility, facilityId: SeedIds.FacilityA2);
        db.NoteRoutingRules.AddRange(ruleUnitA1, ruleUnitA2, ruleFacilityA2);
        await db.SaveChangesAsync();

        var scope = CreateService(db, new UserScopeSnapshot(
            ScopeType.FacilityUnit, SeedIds.RegionA, SeedIds.FacilityA1, UnitA1));

        var syncNotes = scope.FilterQueryable(db.OperationalNotes).Select(n => n.Id).OrderBy(id => id).ToList();
        var asyncNotes = (await scope.FilterQueryableAsync(db.OperationalNotes))
            .Select(n => n.Id).OrderBy(id => id).ToList();
        Assert.Equal(syncNotes, asyncNotes);

        // Legacy Notes behavior: unit scope promotes parent facility + derived parent region.
        Assert.Contains(unitA1Note.Id, syncNotes);
        Assert.Contains(facilityA1Note.Id, syncNotes);
        Assert.Contains(regionANote.Id, syncNotes);
        Assert.DoesNotContain(unitA2Note.Id, syncNotes);
        Assert.DoesNotContain(facilityA2Note.Id, syncNotes);

        var syncRules = scope.FilterRoutingRulesQueryable(db.NoteRoutingRules)
            .Select(r => r.Id).OrderBy(id => id).ToList();
        var asyncRules = (await scope.FilterRoutingRulesQueryableAsync(db.NoteRoutingRules))
            .Select(r => r.Id).OrderBy(id => id).ToList();
        Assert.Equal(syncRules, asyncRules);
        Assert.Equal([ruleUnitA1.Id], syncRules);
        Assert.DoesNotContain(ruleUnitA2.Id, syncRules);
        Assert.DoesNotContain(ruleFacilityA2.Id, syncRules);
    }

    [Fact]
    public async Task Region_scope_sees_all_facilities_and_units_in_region_only()
    {
        await using var db = NoteTestFixtures.CreateDb();
        SeedOrgGraph(db);
        SeedUnits(db);
        var reporter = NoteTestFixtures.AddUser(db);

        var noteA1 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA1, reference: "OBS-00000001");
        var noteA2 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA2, reference: "OBS-00000002");
        var noteB1 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityB1, reference: "OBS-00000003");
        var unitNote = NoteTestFixtures.NewNote(
            ScopeType.FacilityUnit,
            reporter.Id,
            facilityId: SeedIds.FacilityA1,
            facilityUnitId: UnitA1,
            reference: "OBS-00000004");
        var regionNote = NoteTestFixtures.NewNote(
            ScopeType.Region, reporter.Id, regionId: SeedIds.RegionA, reference: "OBS-00000005");
        db.OperationalNotes.AddRange(noteA1, noteA2, noteB1, unitNote, regionNote);

        var ruleRegion = NewRule("RULE-RA", ScopeType.Region, regionId: SeedIds.RegionA);
        var ruleA1 = NewRule("RULE-A1", ScopeType.Facility, facilityId: SeedIds.FacilityA1);
        var ruleA2 = NewRule("RULE-A2", ScopeType.Facility, facilityId: SeedIds.FacilityA2);
        var ruleB1 = NewRule("RULE-B1", ScopeType.Facility, facilityId: SeedIds.FacilityB1);
        var ruleUnit = NewRule(
            "RULE-UA1", ScopeType.FacilityUnit, facilityId: SeedIds.FacilityA1, facilityUnitId: UnitA1);
        db.NoteRoutingRules.AddRange(ruleRegion, ruleA1, ruleA2, ruleB1, ruleUnit);
        await db.SaveChangesAsync();

        var scope = CreateService(db, new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));

        var noteIds = scope.FilterQueryable(db.OperationalNotes).Select(n => n.Id).ToHashSet();
        Assert.Contains(noteA1.Id, noteIds);
        Assert.Contains(noteA2.Id, noteIds);
        Assert.Contains(unitNote.Id, noteIds);
        Assert.Contains(regionNote.Id, noteIds);
        Assert.DoesNotContain(noteB1.Id, noteIds);

        var ruleIds = scope.FilterRoutingRulesQueryable(db.NoteRoutingRules).Select(r => r.Id).ToHashSet();
        Assert.Contains(ruleRegion.Id, ruleIds);
        Assert.Contains(ruleA1.Id, ruleIds);
        Assert.Contains(ruleA2.Id, ruleIds);
        Assert.Contains(ruleUnit.Id, ruleIds);
        Assert.DoesNotContain(ruleB1.Id, ruleIds);

        var asyncNoteIds = (await scope.FilterQueryableAsync(db.OperationalNotes)).Select(n => n.Id).ToHashSet();
        Assert.Equal(noteIds, asyncNoteIds);
    }

    [Fact]
    public async Task Mixed_facility_and_region_scopes_do_not_close_over_sibling_facilities()
    {
        await using var db = NoteTestFixtures.CreateDb();
        SeedOrgGraph(db);
        var reporter = NoteTestFixtures.AddUser(db);

        var noteA1 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA1, reference: "OBS-00000001");
        var noteA2 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA2, reference: "OBS-00000002");
        var noteB1 = NoteTestFixtures.NewNote(
            ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityB1, reference: "OBS-00000003");
        var regionBNote = NoteTestFixtures.NewNote(
            ScopeType.Region, reporter.Id, regionId: SeedIds.RegionB, reference: "OBS-00000004");
        db.OperationalNotes.AddRange(noteA1, noteA2, noteB1, regionBNote);
        await db.SaveChangesAsync();

        var scope = CreateService(
            db,
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null),
            new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionB, null, null));

        var syncIds = scope.FilterQueryable(db.OperationalNotes).Select(n => n.Id).OrderBy(id => id).ToList();
        var asyncIds = (await scope.FilterQueryableAsync(db.OperationalNotes))
            .Select(n => n.Id).OrderBy(id => id).ToList();
        Assert.Equal(syncIds, asyncIds);

        Assert.Contains(noteA1.Id, syncIds);
        Assert.Contains(noteB1.Id, syncIds);
        Assert.Contains(regionBNote.Id, syncIds);
        Assert.DoesNotContain(noteA2.Id, syncIds);
    }

    private static NoteScopeService CreateService(BaseeraDbContext db, params UserScopeSnapshot[] scopes)
    {
        var current = new FakeCurrentUser(true, Guid.NewGuid(), "u1", "user", Array.Empty<string>(), scopes);
        return new NoteScopeService(new OrganizationalScopeService(current, db), current, db);
    }

    private static void SeedOrgGraph(BaseeraDbContext db)
    {
        db.Organizations.Add(new Organization
        {
            Id = SeedIds.Organization,
            Code = "HQ",
            NameAr = "رئيسي",
            IsActive = true
        });
        db.Regions.AddRange(
            new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ", IsActive = true },
            new Region { Id = SeedIds.RegionB, OrganizationId = SeedIds.Organization, Code = "B", NameAr = "ب", IsActive = true });
        db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1", IsActive = true },
            new Facility { Id = SeedIds.FacilityA2, RegionId = SeedIds.RegionA, Code = "A2", NameAr = "أ2", IsActive = true },
            new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1", IsActive = true });
        db.SaveChanges();
    }

    private static void SeedUnits(BaseeraDbContext db)
    {
        db.FacilityUnits.AddRange(
            new FacilityUnit { Id = UnitA1, FacilityId = SeedIds.FacilityA1, Code = "UA1", NameAr = "وحدة1", IsActive = true },
            new FacilityUnit { Id = UnitA2, FacilityId = SeedIds.FacilityA1, Code = "UA2", NameAr = "وحدة2", IsActive = true });
        db.SaveChanges();
    }

    private static NoteRoutingRule NewRule(
        string code,
        ScopeType scopeType,
        Guid? regionId = null,
        Guid? facilityId = null,
        Guid? facilityUnitId = null) =>
        new()
        {
            Code = code,
            NameAr = code,
            NoteTypeId = NoteTestFixtures.DefaultNoteTypeId,
            ScopeType = scopeType,
            RegionId = regionId,
            FacilityId = facilityId,
            FacilityUnitId = facilityUnitId,
            Priority = 10,
            ProcessingTargetType = NoteRoutingProcessingTargetType.Department,
            IsActive = true
        };
}
