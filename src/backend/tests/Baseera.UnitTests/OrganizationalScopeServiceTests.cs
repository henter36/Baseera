using Baseera.Application.Abstractions;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

public sealed class OrganizationalScopeServiceTests
{
    [Fact]
    public void Facility_scope_cannot_access_other_facility()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new BaseeraDbContext(options);
        db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1" });
        db.SaveChanges();

        var current = new FakeCurrentUser(
            true,
            Guid.NewGuid(),
            "u1",
            "user",
            Array.Empty<string>(),
            [new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);

        var service = new OrganizationalScopeService(current, db);
        Assert.True(service.CanAccessFacility(SeedIds.FacilityA1));
        Assert.False(service.CanAccessFacility(SeedIds.FacilityB1));
    }

    private sealed class FakeCurrentUser(
        bool isAuthenticated,
        Guid? userId,
        string? externalSubject,
        string? displayName,
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<UserScopeSnapshot> scopes) : ICurrentUser
    {
        public bool IsAuthenticated { get; } = isAuthenticated;
        public Guid? UserId { get; } = userId;
        public string? ExternalSubject { get; } = externalSubject;
        public string? DisplayName { get; } = displayName;
        public string? IpAddress => null;
        public string? CorrelationId => "test";
        public IReadOnlyCollection<string> Permissions { get; } = permissions;
        public IReadOnlyCollection<UserScopeSnapshot> Scopes { get; } = scopes;
        public bool IsGlobalScope => Scopes.Any(s => s.ScopeType is ScopeType.Global or ScopeType.Headquarters);
        public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode);
    }
}
