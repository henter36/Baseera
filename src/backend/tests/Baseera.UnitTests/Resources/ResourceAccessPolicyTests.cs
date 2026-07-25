namespace Baseera.UnitTests.Resources;

using System.Globalization;
using Baseera.Application.Resources;
using Baseera.Domain.Identity;
using Baseera.Domain.Resources;

public sealed class ResourceAccessPolicyTests
{
    [Theory]
    [InlineData(ResourceType.Vehicle, PermissionCodes.ResourcesViewVehicles)]
    [InlineData(ResourceType.CommunicationDevice, PermissionCodes.ResourcesViewCommunicationDevices)]
    [InlineData(ResourceType.OperationalEquipment, PermissionCodes.ResourcesViewEquipment)]
    [InlineData(ResourceType.SecurityEquipment, PermissionCodes.ResourcesViewEquipment)]
    [InlineData(ResourceType.FacilityAsset, PermissionCodes.ResourcesViewFacilityAssets)]
    public void ViewPermissionFor_maps_resource_types(ResourceType type, string expected) =>
        Assert.Equal(expected, ResourceAccessPolicy.ViewPermissionFor(type));

    [Fact]
    public void ViewableResourceTypes_returns_only_granted_types_in_enum_order()
    {
        var vehiclesOnly = new[] { PermissionCodes.ResourcesViewVehicles };
        Assert.Equal([ResourceType.Vehicle], ResourceAccessPolicy.ViewableResourceTypes(vehiclesOnly));

        var equipmentOnly = new[] { PermissionCodes.ResourcesViewEquipment };
        Assert.Equal(
            [ResourceType.OperationalEquipment, ResourceType.SecurityEquipment],
            ResourceAccessPolicy.ViewableResourceTypes(equipmentOnly));

        var multiple = new[]
        {
            PermissionCodes.ResourcesViewVehicles,
            PermissionCodes.ResourcesViewCommunicationDevices,
            PermissionCodes.ResourcesViewFacilityAssets
        };
        Assert.Equal(
            [ResourceType.Vehicle, ResourceType.CommunicationDevice, ResourceType.FacilityAsset],
            ResourceAccessPolicy.ViewableResourceTypes(multiple));

        Assert.Empty(ResourceAccessPolicy.ViewableResourceTypes([]));
    }

    [Fact]
    public void CanViewResourceType_and_ViewableResourceTypes_respect_grants()
    {
        var permissions = new[]
        {
            PermissionCodes.ResourcesViewVehicles,
            PermissionCodes.ResourcesViewFacilityAssets
        };

        Assert.True(ResourceAccessPolicy.CanViewResourceType(permissions, ResourceType.Vehicle));
        Assert.False(ResourceAccessPolicy.CanViewResourceType(permissions, ResourceType.CommunicationDevice));
        Assert.Equal(
            [ResourceType.Vehicle, ResourceType.FacilityAsset],
            ResourceAccessPolicy.ViewableResourceTypes(permissions));
    }

    [Fact]
    public void NormalizeAssetCode_trims_and_uppercases() =>
        Assert.Equal("VEH-001", ResourceAccessPolicy.NormalizeAssetCode("  veh-001 "));

    [Theory]
    [InlineData("2026-01-01", null, "2026-06-01", null, true)]
    [InlineData("2026-01-01", "2026-03-01", "2026-03-01", "2026-06-01", false)]
    [InlineData("2026-01-01", "2026-03-01", "2026-02-01", "2026-02-15", true)]
    [InlineData("2026-01-01", "2026-02-01", "2026-02-01", null, false)]
    public void PeriodsOverlap_uses_half_open_interval_semantics(
        string existingFrom,
        string? existingTo,
        string candidateFrom,
        string? candidateTo,
        bool expected)
    {
        Assert.Equal(
            expected,
            ResourceAccessPolicy.PeriodsOverlap(
                DateTimeOffset.Parse(existingFrom, CultureInfo.InvariantCulture),
                existingTo is null ? null : DateTimeOffset.Parse(existingTo, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(candidateFrom, CultureInfo.InvariantCulture),
                candidateTo is null ? null : DateTimeOffset.Parse(candidateTo, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void IsValidImportBatchState_accepts_only_supported_statuses()
    {
        Assert.True(ResourceAccessPolicy.IsValidImportBatchState(ResourceImportBatchStatuses.Confirmed, 3, DateTimeOffset.UtcNow));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState(ResourceImportBatchStatuses.Confirmed, 3, null));
        Assert.True(ResourceAccessPolicy.IsValidImportBatchState(ResourceImportBatchStatuses.Previewed, 0, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState(ResourceImportBatchStatuses.Previewed, 1, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState(ResourceImportBatchStatuses.Previewed, 0, DateTimeOffset.UtcNow));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState("Pending", 0, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState("Completed", 0, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState("", 0, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState("confirmed", 1, DateTimeOffset.UtcNow));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchState("previewed", 0, null));
    }

    [Fact]
    public void IsValidImportBatchCounts_enforces_totals_and_status_invariants()
    {
        Assert.True(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 0, ResourceImportBatchStatuses.Previewed, null));
        Assert.True(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 3, ResourceImportBatchStatuses.Confirmed, DateTimeOffset.UtcNow));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 0, 0, ResourceImportBatchStatuses.Previewed, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 4, ResourceImportBatchStatuses.Confirmed, DateTimeOffset.UtcNow));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 3, ResourceImportBatchStatuses.Confirmed, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 1, ResourceImportBatchStatuses.Previewed, null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 0, ResourceImportBatchStatuses.Previewed, DateTimeOffset.UtcNow));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 0, "Unknown", null));
        Assert.False(ResourceAccessPolicy.IsValidImportBatchCounts(5, 3, 1, 1, 0, "Pending", null));
    }

    [Fact]
    public void IsResourceAssetsUniqueViolation_detects_index_and_sql_codes()
    {
        Assert.True(ResourceAccessPolicy.IsResourceAssetsUniqueViolation(
            new InvalidOperationException("Violation of UNIQUE KEY constraint 'IX_ResourceAssets_OrganizationId_AssetCode'.")));
        Assert.True(ResourceAccessPolicy.IsResourceAssetsUniqueViolation(
            new Exception("Cannot insert duplicate key in ResourceAssets AssetCode", new Exception("Error 2627"))));
        Assert.False(ResourceAccessPolicy.IsResourceAssetsUniqueViolation(
            new InvalidOperationException("Some other failure")));
    }

    [Fact]
    public void IsResourceImportBatchesUniqueViolation_detects_batch_index()
    {
        Assert.True(ResourceAccessPolicy.IsResourceImportBatchesUniqueViolation(
            new InvalidOperationException(
                "Cannot insert duplicate key row in object 'dbo.ResourceImportBatches' with unique index 'IX_ResourceImportBatches_FacilityId_SourceSystem_SourceReference_FileHash'.")));
        Assert.False(ResourceAccessPolicy.IsResourceImportBatchesUniqueViolation(
            new InvalidOperationException("IX_ResourceAssets_OrganizationId_AssetCode")));
    }
}
