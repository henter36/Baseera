using Baseera.Application.Security;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;

namespace Baseera.UnitTests.Forms;

public sealed class OrganizationalScopeEntityGuardTests : IDisposable
{
    private readonly BaseeraDbContext _db = FormTestFixtures.CreateDb();

    public OrganizationalScopeEntityGuardTests() => FormTestFixtures.SeedOrgGraph(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Region_with_active_region_succeeds()
    {
        await OrganizationalScopeEntityGuard.EnsureActiveAsync(
            _db,
            SeedIds.RegionA,
            null,
            null);
    }

    [Fact]
    public async Task Region_with_inactive_region_throws_key_not_found()
    {
        var region = await _db.Regions.FindAsync(SeedIds.RegionA);
        Assert.NotNull(region);
        region!.IsActive = false;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionA,
                null,
                null));
        Assert.Equal("المنطقة غير موجودة.", ex.Message);
    }

    [Fact]
    public async Task Facility_with_inactive_region_and_active_facility_throws()
    {
        var region = await _db.Regions.FindAsync(SeedIds.RegionA);
        Assert.NotNull(region);
        region!.IsActive = false;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionA,
                SeedIds.FacilityA1,
                null));
        Assert.Equal("المنطقة غير موجودة.", ex.Message);
    }

    [Fact]
    public async Task FacilityUnit_with_soft_deleted_region_throws()
    {
        var region = await _db.Regions.FindAsync(SeedIds.RegionA);
        Assert.NotNull(region);
        region!.IsDeleted = true;
        await _db.SaveChangesAsync();

        var unitId = await SeedUnitAsync();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionA,
                SeedIds.FacilityA1,
                unitId));
        Assert.Equal("المنطقة غير موجودة.", ex.Message);
    }

    [Fact]
    public async Task Facility_with_active_region_and_matching_facility_succeeds()
    {
        await OrganizationalScopeEntityGuard.EnsureActiveAsync(
            _db,
            SeedIds.RegionA,
            SeedIds.FacilityA1,
            null);
    }

    [Fact]
    public async Task Facility_region_mismatch_throws_invalid_operation()
    {
        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionB,
                SeedIds.FacilityA1,
                null));
        Assert.Contains("المنطقة لا تطابق", mismatch.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unit_facility_mismatch_throws_invalid_operation()
    {
        var unitId = await SeedUnitAsync();

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionA,
                SeedIds.FacilityA2,
                unitId));
        Assert.Contains("الوحدة لا تتبع", mismatch.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Notes_facility_shape_without_regionId_still_validates_facility()
    {
        await OrganizationalScopeEntityGuard.EnsureActiveAsync(
            _db,
            null,
            SeedIds.FacilityA1,
            null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                null,
                Guid.NewGuid(),
                null));
    }

    [Fact]
    public async Task CancellationToken_is_honored_by_queries()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionA,
                SeedIds.FacilityA1,
                null,
                cts.Token));
    }

    [Fact]
    public async Task Missing_facility_throws_key_not_found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionA,
                Guid.NewGuid(),
                null));
    }

    [Fact]
    public async Task Unit_without_facilityId_throws_invalid_operation()
    {
        var unitId = await SeedUnitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OrganizationalScopeEntityGuard.EnsureActiveAsync(
                _db,
                SeedIds.RegionA,
                null,
                unitId));
    }

    [Fact]
    public async Task Matching_unit_succeeds()
    {
        var unitId = await SeedUnitAsync();

        await OrganizationalScopeEntityGuard.EnsureActiveAsync(
            _db,
            SeedIds.RegionA,
            SeedIds.FacilityA1,
            unitId);
    }

    private async Task<Guid> SeedUnitAsync()
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
        return unitId;
    }
}
