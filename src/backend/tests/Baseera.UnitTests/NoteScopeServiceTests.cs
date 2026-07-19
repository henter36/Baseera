using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests;

public sealed class NoteScopeShapeValidationTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();
    private readonly INoteScopeService _service;

    public NoteScopeShapeValidationTests()
    {
        var user = FakeUser([]);
        _service = new NoteScopeService(new OrganizationalScopeService(user, _db), user, _db);
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(ScopeType.MultipleRegions)]
    [InlineData(ScopeType.MultipleFacilities)]
    public void Unsupported_scope_types_are_rejected_in_b1(ScopeType scopeType) =>
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(scopeType, null, null, null));

    [Fact]
    public void Global_scope_rejects_any_id() =>
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Global, Guid.NewGuid(), null, null));

    [Fact]
    public void Headquarters_scope_rejects_any_id() =>
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Headquarters, null, Guid.NewGuid(), null));

    [Fact]
    public void Global_scope_with_no_ids_is_valid() =>
        _service.ValidateScopeShape(ScopeType.Global, null, null, null);

    [Fact]
    public void Region_scope_requires_region_only()
    {
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Region, null, null, null));
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Region, Guid.NewGuid(), Guid.NewGuid(), null));
        _service.ValidateScopeShape(ScopeType.Region, Guid.NewGuid(), null, null);
    }

    [Fact]
    public void Facility_scope_requires_facility_without_unit()
    {
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Facility, null, null, null));
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Facility, null, Guid.NewGuid(), Guid.NewGuid()));
        _service.ValidateScopeShape(ScopeType.Facility, null, Guid.NewGuid(), null);
    }

    [Fact]
    public void FacilityUnit_scope_requires_both_facility_and_unit()
    {
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.FacilityUnit, null, Guid.NewGuid(), null));
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.FacilityUnit, null, null, Guid.NewGuid()));
        _service.ValidateScopeShape(ScopeType.FacilityUnit, null, Guid.NewGuid(), Guid.NewGuid());
    }

    private static ICurrentUser FakeUser(IReadOnlyCollection<UserScopeSnapshot> scopes) =>
        new FakeCurrentUser(true, Guid.NewGuid(), "u1", "user", Array.Empty<string>(), scopes);
}

public sealed class NoteScopeFilterQueryableTests
{
    [Fact]
    public void Facility_scoped_user_only_sees_own_facility_notes()
    {
        using var db = NoteTestFixtures.CreateDb();
        db.Regions.Add(new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" });
        db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1" });
        var reporter = NoteTestFixtures.AddUser(db);
        db.SaveChanges();

        db.OperationalNotes.AddRange(
            NoteTestFixtures.NewNote(ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA1, reference: "OBS-00000001"),
            NoteTestFixtures.NewNote(ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityB1, reference: "OBS-00000002"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, db), current, db);

        var visible = scope.FilterQueryable(db.OperationalNotes).ToList();
        Assert.Single(visible);
        Assert.Equal(SeedIds.FacilityA1, visible[0].FacilityId);
    }

    [Fact]
    public void Region_scoped_user_sees_all_facilities_in_region()
    {
        using var db = NoteTestFixtures.CreateDb();
        db.Regions.Add(new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" });
        db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityA2, RegionId = SeedIds.RegionA, Code = "A2", NameAr = "أ2" });
        var reporter = NoteTestFixtures.AddUser(db);
        db.SaveChanges();

        db.OperationalNotes.AddRange(
            NoteTestFixtures.NewNote(ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA1, reference: "OBS-00000001"),
            NoteTestFixtures.NewNote(ScopeType.Facility, reporter.Id, facilityId: SeedIds.FacilityA2, reference: "OBS-00000002"),
            NoteTestFixtures.NewNote(ScopeType.Region, reporter.Id, regionId: SeedIds.RegionA, reference: "OBS-00000003"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null)]);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, db), current, db);

        var visible = scope.FilterQueryable(db.OperationalNotes).ToList();
        Assert.Equal(3, visible.Count);
    }

    [Fact]
    public void Global_scope_sees_headquarters_and_all_regions()
    {
        using var db = NoteTestFixtures.CreateDb();
        var reporter = NoteTestFixtures.AddUser(db);
        db.SaveChanges();
        db.OperationalNotes.AddRange(
            NoteTestFixtures.NewNote(ScopeType.Headquarters, reporter.Id, reference: "OBS-00000001"),
            NoteTestFixtures.NewNote(ScopeType.Global, reporter.Id, reference: "OBS-00000002"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, db), current, db);

        Assert.Equal(2, scope.FilterQueryable(db.OperationalNotes).Count());
    }

    [Fact]
    public void Headquarters_scope_alone_cannot_see_regional_notes()
    {
        using var db = NoteTestFixtures.CreateDb();
        db.Regions.Add(new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" });
        var reporter = NoteTestFixtures.AddUser(db);
        db.SaveChanges();
        db.OperationalNotes.AddRange(
            NoteTestFixtures.NewNote(ScopeType.Headquarters, reporter.Id, reference: "OBS-00000001"),
            NoteTestFixtures.NewNote(ScopeType.Region, reporter.Id, regionId: SeedIds.RegionA, reference: "OBS-00000002"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Headquarters, null, null, null)]);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, db), current, db);

        var visible = scope.FilterQueryable(db.OperationalNotes).ToList();
        Assert.Single(visible);
        Assert.Equal(ScopeType.Headquarters, visible[0].ScopeType);
    }

    [Fact]
    public void Unauthenticated_or_scopeless_user_sees_nothing()
    {
        using var db = NoteTestFixtures.CreateDb();
        var reporter = NoteTestFixtures.AddUser(db);
        db.OperationalNotes.Add(NoteTestFixtures.NewNote(ScopeType.Headquarters, reporter.Id));
        db.SaveChanges();

        var current = FakeUser([]);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, db), current, db);

        Assert.Empty(scope.FilterQueryable(db.OperationalNotes).ToList());
    }

    private static ICurrentUser FakeUser(IReadOnlyCollection<UserScopeSnapshot> scopes) =>
        new FakeCurrentUser(true, Guid.NewGuid(), "u1", "user", Array.Empty<string>(), scopes);
}
