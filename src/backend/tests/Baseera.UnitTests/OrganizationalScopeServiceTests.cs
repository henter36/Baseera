using Baseera.Application.Security;
using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class EnvironmentSecurityGuardTests
{
    [Theory]
    [InlineData("Production", true, false)]
    [InlineData("Staging", true, false)]
    [InlineData("Production", false, true)]
    public void Restricted_environment_rejects_test_features(string env, bool testAuth, bool seed)
    {
        Assert.Throws<InvalidOperationException>(() =>
            EnvironmentSecurityGuard.EnsureSafeConfiguration(env, testAuth, seed));
    }

    [Fact]
    public void Development_allows_test_auth_when_flag_true()
    {
        EnvironmentSecurityGuard.EnsureSafeConfiguration("Development", true, true);
        Assert.True(EnvironmentSecurityGuard.CanEnableTestAuth("Development", true));
        Assert.False(EnvironmentSecurityGuard.CanEnableTestAuth("Production", true));
    }
}

public sealed class OrganizationalScopeServiceTests
{
    [Fact]
    public void Facility_scope_cannot_access_other_facility()
    {
        using var db = CreateDb();
        db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1" });
        db.SaveChanges();

        var current = FakeUser([new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);
        var service = new OrganizationalScopeService(current, db);
        Assert.True(service.CanAccessFacility(SeedIds.FacilityA1));
        Assert.False(service.CanAccessFacility(SeedIds.FacilityB1));
    }

    [Fact]
    public void Authenticated_user_without_scope_cannot_access_global_or_hq_entities()
    {
        using var db = CreateDb();
        var current = FakeUser([]);
        var service = new OrganizationalScopeService(current, db);
        var hq = new Region { Id = Guid.NewGuid(), OrganizationId = SeedIds.Organization, Code = "HQ-REC", NameAr = "سجل رئيسي" };
        // fabricate scoped entity via anonymous IScopedEntity adapter
        Assert.False(service.CanAccess(new ScopedStub(ScopeType.Global, null, null, null)));
        Assert.False(service.CanAccess(new ScopedStub(ScopeType.Headquarters, null, null, null)));
    }

    [Fact]
    public void Headquarters_scope_can_access_hq_but_not_act_as_global()
    {
        using var db = CreateDb();
        db.Facilities.Add(new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" });
        db.SaveChanges();
        var current = FakeUser([new UserScopeSnapshot(ScopeType.Headquarters, null, null, null)]);
        var service = new OrganizationalScopeService(current, db);
        Assert.True(service.CanAccess(new ScopedStub(ScopeType.Headquarters, null, null, null)));
        Assert.False(service.CanAccess(new ScopedStub(ScopeType.Global, null, null, null)));
        Assert.False(service.CanAccessFacility(SeedIds.FacilityA1));
    }

    [Fact]
    public void Global_scope_can_access_all()
    {
        using var db = CreateDb();
        db.Facilities.Add(new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1" });
        db.SaveChanges();
        var current = FakeUser([new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var service = new OrganizationalScopeService(current, db);
        Assert.True(service.CanAccess(new ScopedStub(ScopeType.Global, null, null, null)));
        Assert.True(service.CanAccess(new ScopedStub(ScopeType.Headquarters, null, null, null)));
        Assert.True(service.CanAccessFacility(SeedIds.FacilityB1));
    }

    private static BaseeraDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BaseeraDbContext(options);
    }

    private static ICurrentUser FakeUser(IReadOnlyCollection<UserScopeSnapshot> scopes) =>
        new FakeCurrentUser(true, Guid.NewGuid(), "u1", "user", Array.Empty<string>(), scopes);

    private sealed class ScopedStub(ScopeType type, Guid? regionId, Guid? facilityId, Guid? unitId) : IScopedEntity
    {
        public ScopeType ScopeType { get; } = type;
        public Guid? RegionId { get; } = regionId;
        public Guid? FacilityId { get; } = facilityId;
        public Guid? FacilityUnitId { get; } = unitId;
    }

}
