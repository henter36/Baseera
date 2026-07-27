namespace Baseera.UnitTests.RiskManagement;

using Baseera.Application.RiskManagement;
using Baseera.Domain.RiskManagement;

public sealed class RiskRecurrenceDetectorTests
{
    [Fact]
    public void Build_IsCaseAndWhitespaceInsensitive()
    {
        var facilityId = Guid.NewGuid();
        var keyA = RiskRecurrenceKeyBuilder.Build("sec", facilityId, "  Fence   Breach  ");
        var keyB = RiskRecurrenceKeyBuilder.Build("SEC", facilityId, "fence breach");

        Assert.Equal(keyA, keyB);
    }

    [Fact]
    public void Build_DiffersAcrossFacilities()
    {
        var keyA = RiskRecurrenceKeyBuilder.Build("SEC", Guid.NewGuid(), "Fence Breach");
        var keyB = RiskRecurrenceKeyBuilder.Build("SEC", Guid.NewGuid(), "Fence Breach");

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Detect_ReturnsNone_WhenNoOtherRisksShareKey() =>
        Assert.Equal(RecurrencePatternKind.None, RiskRecurrenceDetector.Detect([]));

    [Fact]
    public void Detect_FlagsPotentialDuplicate_WhenAnOpenRiskSharesKey()
    {
        var statuses = new[] { RiskStatus.Active };
        Assert.Equal(RecurrencePatternKind.PotentialDuplicate, RiskRecurrenceDetector.Detect(statuses));
    }

    [Fact]
    public void Detect_FlagsRecurringPattern_WhenEnoughClosedRisksShareKey()
    {
        var statuses = new[] { RiskStatus.Closed, RiskStatus.Closed };
        Assert.Equal(RecurrencePatternKind.RecurringPattern, RiskRecurrenceDetector.Detect(statuses, recurrenceThreshold: 2));
    }

    [Fact]
    public void Detect_ReturnsNone_WhenBelowRecurrenceThreshold()
    {
        var statuses = new[] { RiskStatus.Closed };
        Assert.Equal(RecurrencePatternKind.None, RiskRecurrenceDetector.Detect(statuses, recurrenceThreshold: 2));
    }

    [Fact]
    public void Detect_PrefersDuplicateOverRecurring_WhenBothConditionsHold()
    {
        var statuses = new[] { RiskStatus.Closed, RiskStatus.Active };
        Assert.Equal(RecurrencePatternKind.PotentialDuplicate, RiskRecurrenceDetector.Detect(statuses, recurrenceThreshold: 1));
    }
}
