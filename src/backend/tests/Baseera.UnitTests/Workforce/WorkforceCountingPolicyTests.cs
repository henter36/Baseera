using Baseera.Application.Workforce;
using Baseera.Domain.Workforce;

namespace Baseera.UnitTests.Workforce;

public class WorkforceCountingPolicyTests
{
    [Fact]
    public void Replacement_replaces_and_does_not_add_to_headcount()
    {
        var original = Guid.NewGuid();
        var replacement = Guid.NewGuid();
        var count = WorkforceCountingPolicy.CountRosterHeadcount(
        [
            (original, null),
            (replacement, original)
        ]);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Overlapping_members_count_once()
    {
        var member = Guid.NewGuid();
        Assert.Equal(1, WorkforceCountingPolicy.CountMembersWithoutOverlapDoubleCount([member, member]));
    }

    [Fact]
    public void Unknown_availability_is_not_available()
    {
        Assert.False(WorkforceCountingPolicy.CountsAsAvailable(
            EmploymentStatus.Active,
            isOperational: true,
            availabilityType: null,
            affectsOperationalAvailability: true,
            availabilityKnown: false));
    }

    [Fact]
    public void Restricted_duty_blocks_driver_role()
    {
        Assert.True(WorkforceCountingPolicy.IsRoleBlockedByRestriction(
            OperationalRestrictionCode.CannotDrive,
            "VehicleDriver"));
        Assert.False(WorkforceCountingPolicy.IsRoleBlockedByRestriction(
            OperationalRestrictionCode.CannotDrive,
            "SecurityOfficer"));
    }

    [Fact]
    public void Expired_qualification_blocks_qualified_coverage()
    {
        var role = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.False(WorkforceCountingPolicy.CountsAsQualified(
            QualificationStatus.Valid, role, role, now, now.AddMinutes(-1)));
        Assert.True(WorkforceCountingPolicy.CountsAsQualified(
            QualificationStatus.Valid, role, role, now, now.AddDays(30)));
    }

    [Fact]
    public void Shift_window_crosses_midnight_with_timezone()
    {
        var (start, end) = WorkforceShiftPolicy.ResolveUtcWindow(
            new DateOnly(2026, 7, 25),
            new TimeOnly(22, 0),
            new TimeOnly(6, 0),
            crossesMidnight: true,
            timezoneId: "UTC");
        Assert.True(end > start);
        Assert.Equal(8, (end - start).TotalHours);
    }

    [Fact]
    public void Assignment_alone_does_not_imply_presence()
    {
        Assert.False(WorkforceCountingPolicy.IsPresentSignal(
            hasPublishedRosterPresence: false,
            hasActiveAssignment: true));
        Assert.True(WorkforceCountingPolicy.IsPresentSignal(
            hasPublishedRosterPresence: true,
            hasActiveAssignment: false));
    }
}
