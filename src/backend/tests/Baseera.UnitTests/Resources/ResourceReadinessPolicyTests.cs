namespace Baseera.UnitTests.Resources;

using Baseera.Application.Resources;
using Baseera.Domain.Common;
using Baseera.Domain.Resources;

public sealed class ResourceReadinessPolicyTests
{
    [Fact]
    public void Calculate_UsesOperationalStatusesForReadiness()
    {
        var result = ResourceReadinessPolicy.Calculate(new ResourceReadinessInputs(
            TotalRegistered: 10,
            Available: 3,
            Standby: 2,
            InUse: 2,
            Reserved: 1,
            UnderMaintenance: 1,
            OutOfService: 1,
            AwaitingParts: 0,
            Unknown: 0,
            Retired: 0,
            Transferred: 0,
            Required: 8,
            MissingDataRecords: 0));

        Assert.Equal(8, result.Operational);
        Assert.Equal(0, result.Gap);
        Assert.Equal(1.0m, result.ReadinessRate);
        Assert.Equal(0.5m, result.AvailabilityRate);
    }

    [Fact]
    public void Calculate_ExcludesRetiredAndTransferredFromAvailabilityDenominator()
    {
        var result = ResourceReadinessPolicy.Calculate(new ResourceReadinessInputs(
            TotalRegistered: 10,
            Available: 4,
            Standby: 0,
            InUse: 0,
            Reserved: 0,
            UnderMaintenance: 0,
            OutOfService: 2,
            AwaitingParts: 0,
            Unknown: 0,
            Retired: 2,
            Transferred: 2,
            Required: 6,
            MissingDataRecords: 1));

        Assert.Equal(4, result.Operational);
        Assert.Equal(2, result.Gap);
        Assert.Equal(0.6667m, result.ReadinessRate);
        Assert.Equal(0.6667m, result.AvailabilityRate);
        Assert.Equal(0.8333m, result.DataCompletenessRate);
    }

    [Fact]
    public void Calculate_ZeroRequirementDoesNotReportFullReadiness()
    {
        var result = ResourceReadinessPolicy.Calculate(new ResourceReadinessInputs(
            TotalRegistered: 2,
            Available: 2,
            Standby: 0,
            InUse: 0,
            Reserved: 0,
            UnderMaintenance: 0,
            OutOfService: 0,
            AwaitingParts: 0,
            Unknown: 0,
            Retired: 0,
            Transferred: 0,
            Required: 0,
            MissingDataRecords: 0));

        Assert.Null(result.ReadinessRate);
        Assert.Equal(0, result.Gap);
    }

    [Theory]
    [InlineData(0, 0, "missing")]
    [InlineData(0, 5, "missing")]
    [InlineData(3, 1, "partial")]
    [InlineData(3, 0, "current")]
    public void ResolveFreshnessStatus_UsesTotalAndStalePriority(int total, int stale, string expected) =>
        Assert.Equal(expected, ResourceReadinessPolicy.ResolveFreshnessStatus(total, stale));

    [Theory]
    [InlineData(0, 0, "unknown")]
    [InlineData(0, 5, "unknown")]
    [InlineData(3, 1, "medium")]
    [InlineData(3, 0, "high")]
    public void ResolveConfidenceLevel_UsesTotalAndMediumSignalPriority(int total, int mediumSignal, string expected) =>
        Assert.Equal(expected, ResourceReadinessPolicy.ResolveConfidenceLevel(total, mediumSignal));

    [Fact]
    public void ResourceAsset_ScopeType_PrefersFacilityUnitThenFacilityThenHeadquarters()
    {
        var unitScoped = new ResourceAsset
        {
            OperationalFacilityId = Guid.NewGuid(),
            OperationalFacilityUnitId = Guid.NewGuid()
        };
        var facilityScoped = new ResourceAsset
        {
            OperationalFacilityId = Guid.NewGuid(),
            OperationalFacilityUnitId = null
        };
        var headquartersScoped = new ResourceAsset
        {
            OperationalFacilityId = null,
            OperationalFacilityUnitId = null
        };

        Assert.Equal(ScopeType.FacilityUnit, unitScoped.ScopeType);
        Assert.Equal(ScopeType.Facility, facilityScoped.ScopeType);
        Assert.Equal(ScopeType.Headquarters, headquartersScoped.ScopeType);
    }

    [Fact]
    public void StateMachine_BlocksRetiredAssetReturningToServiceWithoutReactivation()
    {
        Assert.False(ResourceStatusStateMachine.CanTransition(ResourceStatus.Retired, ResourceStatus.InUse, hasMaintenanceReason: true));
        Assert.True(ResourceStatusStateMachine.CanTransition(ResourceStatus.Retired, ResourceStatus.Unknown, hasMaintenanceReason: false));
    }

    [Fact]
    public void StateMachine_RequiresReasonForMaintenanceStates()
    {
        Assert.False(ResourceStatusStateMachine.CanTransition(ResourceStatus.Available, ResourceStatus.UnderMaintenance, hasMaintenanceReason: false));
        Assert.True(ResourceStatusStateMachine.CanTransition(ResourceStatus.Available, ResourceStatus.UnderMaintenance, hasMaintenanceReason: true));
        Assert.False(ResourceStatusStateMachine.CanTransition(ResourceStatus.Available, ResourceStatus.AwaitingParts, hasMaintenanceReason: false));
    }
}
