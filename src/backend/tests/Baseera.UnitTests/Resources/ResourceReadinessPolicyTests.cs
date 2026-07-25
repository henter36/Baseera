namespace Baseera.UnitTests.Resources;

using Baseera.Application.Resources;
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
