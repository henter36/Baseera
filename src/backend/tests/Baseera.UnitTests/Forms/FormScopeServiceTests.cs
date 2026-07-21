using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms;

public sealed class FormScopeShapeValidationTests : IDisposable
{
    private readonly BaseeraDbContext _db = FormTestFixtures.CreateDb();
    private readonly IFormScopeService _service;

    public FormScopeShapeValidationTests()
    {
        var user = FakeUser([]);
        _service = new FormScopeService(new OrganizationalScopeService(user, _db), user, _db);
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(ScopeType.MultipleRegions)]
    [InlineData(ScopeType.MultipleFacilities)]
    public void Unsupported_scope_types_are_rejected(ScopeType scopeType) =>
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(scopeType, null, null, null));

    [Fact]
    public void Global_scope_rejects_any_id() =>
        Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateScopeShape(ScopeType.Global, Guid.NewGuid(), null, null));

    [Fact]
    public void Headquarters_scope_rejects_any_id() =>
        Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateScopeShape(ScopeType.Headquarters, null, Guid.NewGuid(), null));

    [Fact]
    public void Global_scope_with_no_ids_is_valid() =>
        _service.ValidateScopeShape(ScopeType.Global, null, null, null);

    [Fact]
    public void Region_scope_requires_region_only()
    {
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Region, null, null, null));
        Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateScopeShape(ScopeType.Region, Guid.NewGuid(), Guid.NewGuid(), null));
        _service.ValidateScopeShape(ScopeType.Region, Guid.NewGuid(), null, null);
    }

    [Fact]
    public void Facility_scope_requires_facility_without_unit()
    {
        Assert.Throws<InvalidOperationException>(() => _service.ValidateScopeShape(ScopeType.Facility, null, null, null));
        Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateScopeShape(ScopeType.Facility, null, Guid.NewGuid(), Guid.NewGuid()));
        _service.ValidateScopeShape(ScopeType.Facility, Guid.NewGuid(), Guid.NewGuid(), null);
    }

    [Fact]
    public void FacilityUnit_scope_requires_all_ids()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateScopeShape(ScopeType.FacilityUnit, null, Guid.NewGuid(), null));
        Assert.Throws<InvalidOperationException>(() =>
            _service.ValidateScopeShape(ScopeType.FacilityUnit, null, null, Guid.NewGuid()));
        _service.ValidateScopeShape(ScopeType.FacilityUnit, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    private static ICurrentUser FakeUser(IReadOnlyCollection<UserScopeSnapshot> scopes) =>
        new FakeCurrentUser(true, Guid.NewGuid(), "u1", "user", Array.Empty<string>(), scopes);
}

public sealed class FormScopeFilterQueryableTests
{
    [Fact]
    public void Facility_scoped_user_only_sees_own_facility_forms()
    {
        using var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var creator = FormTestFixtures.AddUser(db);
        db.FormDefinitions.AddRange(
            FormTestFixtures.NewForm(creator.Id, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, code: "FAC-A1"),
            FormTestFixtures.NewForm(creator.Id, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, code: "FAC-B1"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);
        var scope = new FormScopeService(new OrganizationalScopeService(current, db), current, db);

        var visible = scope.FilterQueryable(db.FormDefinitions).ToList();
        Assert.Single(visible);
        Assert.Equal(SeedIds.FacilityA1, visible[0].FacilityId);
    }

    [Fact]
    public void Region_scoped_user_sees_all_facilities_in_region()
    {
        using var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var creator = FormTestFixtures.AddUser(db);
        db.FormDefinitions.AddRange(
            FormTestFixtures.NewForm(creator.Id, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, code: "REG-A1"),
            FormTestFixtures.NewForm(creator.Id, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA2, code: "REG-A2"),
            FormTestFixtures.NewForm(creator.Id, ScopeType.Region, SeedIds.RegionA, code: "REG-ONLY"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null)]);
        var scope = new FormScopeService(new OrganizationalScopeService(current, db), current, db);

        Assert.Equal(3, scope.FilterQueryable(db.FormDefinitions).Count());
    }

    [Fact]
    public void Global_scope_sees_all_forms()
    {
        using var db = FormTestFixtures.CreateDb();
        var creator = FormTestFixtures.AddUser(db);
        db.FormDefinitions.AddRange(
            FormTestFixtures.NewForm(creator.Id, ScopeType.Headquarters, code: "HQ-001"),
            FormTestFixtures.NewForm(creator.Id, ScopeType.Global, code: "GL-001"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var scope = new FormScopeService(new OrganizationalScopeService(current, db), current, db);

        Assert.Equal(2, scope.FilterQueryable(db.FormDefinitions).Count());
    }

    [Fact]
    public void Unauthenticated_user_sees_nothing()
    {
        using var db = FormTestFixtures.CreateDb();
        var creator = FormTestFixtures.AddUser(db);
        db.FormDefinitions.Add(FormTestFixtures.NewForm(creator.Id));
        db.SaveChanges();

        var current = new FakeCurrentUser(false, null, null, null, [], []);
        var scope = new FormScopeService(new OrganizationalScopeService(current, db), current, db);

        Assert.Empty(scope.FilterQueryable(db.FormDefinitions).ToList());
    }

    [Fact]
    public void Headquarters_scope_sees_headquarters_forms_only()
    {
        using var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var creator = FormTestFixtures.AddUser(db);
        db.FormDefinitions.AddRange(
            FormTestFixtures.NewForm(creator.Id, ScopeType.Headquarters, code: "HQ-FORM"),
            FormTestFixtures.NewForm(creator.Id, ScopeType.Region, SeedIds.RegionA, code: "REG-FORM"));
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Headquarters, null, null, null)]);
        var scope = new FormScopeService(new OrganizationalScopeService(current, db), current, db);

        var visible = scope.FilterQueryable(db.FormDefinitions).ToList();
        Assert.Single(visible);
        Assert.Equal(ScopeType.Headquarters, visible[0].ScopeType);
    }

    private static ICurrentUser FakeUser(IReadOnlyCollection<UserScopeSnapshot> scopes) =>
        new FakeCurrentUser(true, Guid.NewGuid(), "u1", "user", Array.Empty<string>(), scopes);
}
