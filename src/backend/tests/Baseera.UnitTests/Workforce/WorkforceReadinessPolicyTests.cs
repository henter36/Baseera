namespace Baseera.UnitTests.Workforce;

using System.Globalization;
using Baseera.Application.Workforce;
using Baseera.Domain.Workforce;

public sealed class WorkforceReadinessPolicyTests
{
    [Fact]
    public void Calculate_RequiredZero_ReturnsNullCoverageRate()
    {
        var result = WorkforceReadinessPolicy.Calculate(new WorkforceCoverageInputs(
            Required: 0,
            MinimumSafe: 0,
            Assigned: 2,
            Scheduled: 2,
            Present: 2,
            OperationallyAvailable: 2,
            Qualified: 2,
            Unqualified: 0,
            Absent: 0,
            OnLeave: 0,
            InTraining: 0,
            Restricted: 0,
            Overtime: 0,
            HasRequirementBaseline: true,
            HasPresenceSignal: true));

        Assert.Null(result.CoverageRate);
        Assert.Equal(0, result.Gap);
        Assert.Equal(WorkforceCoverageStatus.Unknown, result.Status);
    }

    [Fact]
    public void Calculate_AppliesCoverageStatusThresholds()
    {
        Assert.Equal(WorkforceCoverageStatus.Ready, WorkforceReadinessPolicy.ResolveCoverageStatus(1.0m, true));
        Assert.Equal(WorkforceCoverageStatus.Attention, WorkforceReadinessPolicy.ResolveCoverageStatus(0.90m, true));
        Assert.Equal(WorkforceCoverageStatus.Critical, WorkforceReadinessPolicy.ResolveCoverageStatus(0.75m, true));
        Assert.Equal(WorkforceCoverageStatus.Unsafe, WorkforceReadinessPolicy.ResolveCoverageStatus(0.50m, true));
        Assert.Equal(WorkforceCoverageStatus.Unknown, WorkforceReadinessPolicy.ResolveCoverageStatus(null, true));
        Assert.Equal(WorkforceCoverageStatus.Unknown, WorkforceReadinessPolicy.ResolveCoverageStatus(1.0m, false));
    }

    [Fact]
    public void Calculate_ComputesGapAndSafeGap()
    {
        var result = WorkforceReadinessPolicy.Calculate(new WorkforceCoverageInputs(
            Required: 10,
            MinimumSafe: 8,
            Assigned: 6,
            Scheduled: 6,
            Present: 5,
            OperationallyAvailable: 6,
            Qualified: 5,
            Unqualified: 1,
            Absent: 1,
            OnLeave: 2,
            InTraining: 0,
            Restricted: 0,
            Overtime: 0,
            HasRequirementBaseline: true,
            HasPresenceSignal: true));

        Assert.Equal(4, result.Gap);
        Assert.Equal(2, result.SafeGap);
        Assert.Equal(0.6m, result.CoverageRate);
        Assert.Equal(WorkforceCoverageStatus.Unsafe, result.Status);
    }

    [Theory]
    [InlineData(EmploymentStatus.Active, true, true)]
    [InlineData(EmploymentStatus.SecondedIn, true, true)]
    [InlineData(EmploymentStatus.Suspended, true, false)]
    [InlineData(EmploymentStatus.LongLeave, true, false)]
    [InlineData(EmploymentStatus.Active, false, false)]
    public void IsEmploymentOperationallyEligible_FiltersStatuses(EmploymentStatus status, bool isOperational, bool expected) =>
        Assert.Equal(expected, WorkforceReadinessPolicy.IsEmploymentOperationallyEligible(status, isOperational));

    [Theory]
    [InlineData(AvailabilityType.AnnualLeave, true, true)]
    [InlineData(AvailabilityType.SickLeave, true, true)]
    [InlineData(AvailabilityType.Training, true, true)]
    [InlineData(AvailabilityType.Available, true, false)]
    [InlineData(AvailabilityType.AnnualLeave, false, false)]
    public void IsAvailabilityBlocking_ExcludesLeaveWhenAffecting(AvailabilityType type, bool affects, bool expected) =>
        Assert.Equal(expected, WorkforceReadinessPolicy.IsAvailabilityBlocking(type, affects));

    [Fact]
    public void IsQualificationValidForRole_RequiresMatchingRoleAndNonExpired()
    {
        var roleId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-07-25T00:00:00Z", CultureInfo.InvariantCulture);
        Assert.True(WorkforceReadinessPolicy.IsQualificationValidForRole(
            QualificationStatus.Valid, roleId, roleId, now, now.AddDays(10)));
        Assert.False(WorkforceReadinessPolicy.IsQualificationValidForRole(
            QualificationStatus.Valid, Guid.NewGuid(), roleId, now, now.AddDays(10)));
        Assert.False(WorkforceReadinessPolicy.IsQualificationValidForRole(
            QualificationStatus.Valid, roleId, roleId, now, now.AddDays(-1)));
        Assert.False(WorkforceReadinessPolicy.IsQualificationValidForRole(
            QualificationStatus.Expired, roleId, roleId, now, now.AddDays(10)));
    }

    [Fact]
    public void IsWithinShiftWindow_SupportsMidnightCrossing()
    {
        Assert.True(WorkforceReadinessPolicy.IsWithinShiftWindow(new TimeOnly(20, 0), new TimeOnly(18, 0), new TimeOnly(6, 0), true));
        Assert.True(WorkforceReadinessPolicy.IsWithinShiftWindow(new TimeOnly(2, 0), new TimeOnly(18, 0), new TimeOnly(6, 0), true));
        Assert.False(WorkforceReadinessPolicy.IsWithinShiftWindow(new TimeOnly(12, 0), new TimeOnly(18, 0), new TimeOnly(6, 0), true));
        Assert.True(WorkforceReadinessPolicy.IsWithinShiftWindow(new TimeOnly(10, 0), new TimeOnly(6, 0), new TimeOnly(18, 0), false));
        Assert.False(WorkforceReadinessPolicy.IsWithinShiftWindow(new TimeOnly(19, 0), new TimeOnly(6, 0), new TimeOnly(18, 0), false));
    }

    [Fact]
    public void FatiguePolicy_EmitsDeterministicIndicators()
    {
        var now = DateTimeOffset.Parse("2026-07-25T00:00:00Z", CultureInfo.InvariantCulture);
        var indicators = WorkforceFatiguePolicy.Evaluate(new WorkforceFatiguePolicy.FatigueIndicatorInputs(
            OvertimeHoursInWindow: 14,
            ConsecutiveShiftsWithoutRest: 3,
            CriticalRoleCoverageCount: 1,
            CriticalRoleRequiredCount: 2,
            NearestQualificationExpiryUtc: now.AddDays(10),
            AsOfUtc: now));

        Assert.Contains(WorkforceFatiguePolicy.ExcessiveOvertimeHours, indicators);
        Assert.Contains(WorkforceFatiguePolicy.ConsecutiveShiftsWithoutRest, indicators);
        Assert.Contains(WorkforceFatiguePolicy.SinglePointOfFailure, indicators);
        Assert.Contains(WorkforceFatiguePolicy.QualificationExpiringSoon, indicators);
    }

    [Fact]
    public void SourceResolver_PrefersPublishedRoster_AndAssignmentIsNotPresent()
    {
        var resolver = new WorkforceSourceResolver();
        Assert.Equal(
            WorkforcePresenceSource.PublishedRoster,
            resolver.Resolve(true, RosterAssignmentStatus.Present, true, true, true));
        Assert.Equal(
            WorkforcePresenceSource.AttendanceOrImport,
            resolver.Resolve(false, null, true, true, true));
        Assert.Equal(
            WorkforcePresenceSource.AvailabilityEvent,
            resolver.Resolve(false, null, false, true, true));
        Assert.Equal(
            WorkforcePresenceSource.Assignment,
            resolver.Resolve(false, null, false, false, true));
        Assert.Equal(
            WorkforcePresenceSource.Unknown,
            resolver.Resolve(false, null, false, false, false));
    }
}
