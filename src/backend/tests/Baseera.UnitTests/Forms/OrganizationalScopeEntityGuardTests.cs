using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms;

public sealed class OrganizationalScopeEntityGuardTests : IDisposable
{
    private readonly BaseeraDbContext _db = FormTestFixtures.CreateDb();

    public OrganizationalScopeEntityGuardTests() => FormTestFixtures.SeedOrgGraph(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Region_scope_requires_active_region()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                ScopeType.Region,
                Guid.NewGuid(),
                null,
                null));

        await OrganizationalScopeEntityGuard.EnsureActiveAsync(
            _db,
            ScopeType.Region,
            SeedIds.RegionA,
            null,
            null);
    }

    [Fact]
    public async Task Facility_must_exist_and_match_region()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                ScopeType.Facility,
                SeedIds.RegionA,
                Guid.NewGuid(),
                null));

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                ScopeType.Facility,
                SeedIds.RegionB,
                SeedIds.FacilityA1,
                null));
        Assert.Contains("المنطقة لا تطابق", mismatch.Message, StringComparison.Ordinal);

        await OrganizationalScopeEntityGuard.EnsureActiveAsync(
            _db,
            ScopeType.Facility,
            SeedIds.RegionA,
            SeedIds.FacilityA1,
            null);
    }

    [Fact]
    public async Task Unit_requires_matching_facility()
    {
        var unitId = Guid.NewGuid();
        _db.FacilityUnits.Add(new FacilityUnit
        {
            Id = unitId,
            FacilityId = SeedIds.FacilityA1,
            Code = "U1",
            NameAr = "وحدة",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                ScopeType.FacilityUnit,
                SeedIds.RegionA,
                null,
                unitId));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                ScopeType.FacilityUnit,
                SeedIds.RegionA,
                SeedIds.FacilityA1,
                Guid.NewGuid()));

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                ScopeType.FacilityUnit,
                SeedIds.RegionA,
                SeedIds.FacilityA2,
                unitId));
        Assert.Contains("الوحدة لا تتبع", mismatch.Message, StringComparison.Ordinal);

        await OrganizationalScopeEntityGuard.EnsureActiveAsync(
            _db,
            ScopeType.FacilityUnit,
            SeedIds.RegionA,
            SeedIds.FacilityA1,
            unitId);
    }
}
