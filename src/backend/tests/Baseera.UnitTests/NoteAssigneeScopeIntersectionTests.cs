using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests;

public sealed class NoteAssigneeScopeIntersectionTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();
    private static readonly Guid UnitA1East = Guid.Parse("44444444-4444-4444-4444-444444444401");
    private static readonly Guid UnitA1West = Guid.Parse("44444444-4444-4444-4444-444444444402");

    public void Dispose() => _db.Dispose();

    private OperationalNote Note(ScopeType scope, Guid? regionId = null, Guid? facilityId = null, Guid? unitId = null) =>
        NoteTestFixtures.NewNote(scope, Guid.NewGuid(), regionId, facilityId, unitId, status: NoteStatus.Open);

    private static IReadOnlyList<UserScopeSnapshot> Scopes(params UserScopeSnapshot[] scopes) => scopes;

    private void SeedOrgGraph()
    {
        _db.Regions.AddRange(
            new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" },
            new Region { Id = SeedIds.RegionB, OrganizationId = SeedIds.Organization, Code = "B", NameAr = "ب" });
        _db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityA2, RegionId = SeedIds.RegionA, Code = "A2", NameAr = "أ2" },
            new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1" });
        _db.FacilityUnits.AddRange(
            new FacilityUnit { Id = UnitA1East, FacilityId = SeedIds.FacilityA1, Code = "E", NameAr = "شرق" },
            new FacilityUnit { Id = UnitA1West, FacilityId = SeedIds.FacilityA1, Code = "W", NameAr = "غرب" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Global_scope_intersects_global_and_headquarters_notes()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Global, null, null, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsAsync(_db, scopes, Note(ScopeType.Global), default));
        Assert.True(await NoteAssigneeScopeIntersection.IntersectsAsync(_db, scopes, Note(ScopeType.Headquarters), default));
        // Region/Facility/Unit notes are accepted for Global assignees via EnsureAssigneeScopeIntersectsAsync
        // short-circuit before IntersectsAsync — not inside this helper.
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsAsync(
            _db, scopes, Note(ScopeType.Region, SeedIds.RegionA), default));
    }

    [Fact]
    public async Task Headquarters_intersects_headquarters_but_not_region()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Headquarters, null, null, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsAsync(_db, scopes, Note(ScopeType.Headquarters), default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsAsync(
            _db, scopes, Note(ScopeType.Region, SeedIds.RegionA), default));
        Assert.False(NoteAssigneeScopeIntersection.HasGlobalScope(scopes));
        Assert.True(NoteAssigneeScopeIntersection.HasHeadquartersScope(scopes));
    }

    [Fact]
    public async Task Region_intersects_same_region_only()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsRegionAsync(_db, scopes, SeedIds.RegionA, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsRegionAsync(_db, scopes, SeedIds.RegionB, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsRegionAsync(_db, scopes, null, default));
    }

    [Fact]
    public async Task MultipleRegions_intersects_matching_region()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.MultipleRegions, SeedIds.RegionB, null, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsRegionAsync(_db, scopes, SeedIds.RegionB, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsRegionAsync(_db, scopes, SeedIds.RegionA, default));
    }

    [Fact]
    public async Task Facility_scope_intersects_region_note_via_facility_region_map()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsRegionAsync(_db, scopes, SeedIds.RegionA, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsRegionAsync(_db, scopes, SeedIds.RegionB, default));
    }

    [Fact]
    public async Task Facility_intersects_same_facility_and_rejects_other()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, SeedIds.FacilityA1, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, SeedIds.FacilityB1, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, null, default));
    }

    [Fact]
    public async Task Region_scope_intersects_facility_in_same_region()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, SeedIds.FacilityA1, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, SeedIds.FacilityB1, default));
    }

    [Fact]
    public async Task MultipleFacilities_intersects_matching_facility()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.MultipleFacilities, SeedIds.RegionA, SeedIds.FacilityA2, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, SeedIds.FacilityA2, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, SeedIds.FacilityA1, default));
    }

    [Fact]
    public async Task FacilityUnit_intersects_same_unit_only_for_unit_scopes()
    {
        SeedOrgGraph();
        var east = Scopes(new UserScopeSnapshot(ScopeType.FacilityUnit, SeedIds.RegionA, SeedIds.FacilityA1, UnitA1East));
        var west = Scopes(new UserScopeSnapshot(ScopeType.FacilityUnit, SeedIds.RegionA, SeedIds.FacilityA1, UnitA1West));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsFacilityUnitAsync(
            _db, east, SeedIds.FacilityA1, UnitA1East, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityUnitAsync(
            _db, west, SeedIds.FacilityA1, UnitA1East, default));
    }

    [Fact]
    public async Task Facility_scope_intersects_unit_notes_in_same_facility()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        Assert.True(await NoteAssigneeScopeIntersection.IntersectsFacilityUnitAsync(
            _db, scopes, SeedIds.FacilityA1, UnitA1East, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityUnitAsync(
            _db, scopes, SeedIds.FacilityB1, UnitA1East, default));
    }

    [Fact]
    public async Task FacilityUnit_rejects_missing_ids_and_unknown_facility()
    {
        SeedOrgGraph();
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));
        var missingFacility = Guid.NewGuid();

        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityUnitAsync(
            _db, scopes, SeedIds.FacilityA1, null, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityUnitAsync(
            _db, scopes, null, UnitA1East, default));
        Assert.False(await NoteAssigneeScopeIntersection.IntersectsFacilityAsync(_db, scopes, missingFacility, default));
        Assert.Null(await NoteAssigneeScopeIntersection.GetFacilityRegionIdAsync(_db, missingFacility, default));
    }

    [Fact]
    public async Task Global_user_short_circuit_via_HasGlobalScope_helper()
    {
        var scopes = Scopes(new UserScopeSnapshot(ScopeType.Global, null, null, null));
        Assert.True(NoteAssigneeScopeIntersection.HasGlobalScope(scopes));
        Assert.True(NoteAssigneeScopeIntersection.HasHeadquartersScope(scopes));
    }
}
