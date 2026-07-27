namespace Baseera.UnitTests.RiskManagement;

using Baseera.Application.RiskManagement;
using Baseera.Domain.RiskManagement;

public sealed class RiskTrendCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsUnknown_WhenNoPreviousAssessment()
    {
        var current = new RiskAssessmentComparisonInput(10, 2, 5, "MED");
        var (trend, reason) = RiskTrendCalculator.Calculate(null, current, false);

        Assert.Equal(RiskTrend.Unknown, trend);
        Assert.Contains("لا يوجد تقييم", reason);
    }

    [Fact]
    public void Calculate_ReturnsIncreasing_WhenScoreRises()
    {
        var previous = new RiskAssessmentComparisonInput(10, 2, 5, "MED");
        var current = new RiskAssessmentComparisonInput(16, 4, 4, "HIGH");
        var (trend, reason) = RiskTrendCalculator.Calculate(previous, current, false);

        Assert.Equal(RiskTrend.Increasing, trend);
        Assert.Contains("10", reason);
        Assert.Contains("16", reason);
    }

    [Fact]
    public void Calculate_ReturnsDecreasing_WhenScoreFalls()
    {
        var previous = new RiskAssessmentComparisonInput(20, 4, 5, "HIGH");
        var current = new RiskAssessmentComparisonInput(8, 2, 4, "MED");
        var (trend, _) = RiskTrendCalculator.Calculate(previous, current, false);

        Assert.Equal(RiskTrend.Decreasing, trend);
    }

    [Fact]
    public void Calculate_ReturnsStable_WhenScoreAndComponentsUnchangedAndNoNewSources()
    {
        var previous = new RiskAssessmentComparisonInput(12, 3, 4, "MED");
        var current = new RiskAssessmentComparisonInput(12, 3, 4, "MED");
        var (trend, _) = RiskTrendCalculator.Calculate(previous, current, false);

        Assert.Equal(RiskTrend.Stable, trend);
    }

    [Fact]
    public void Calculate_ReturnsIncreasing_WhenScoreStableButNewSourcesAppeared()
    {
        var previous = new RiskAssessmentComparisonInput(12, 3, 4, "MED");
        var current = new RiskAssessmentComparisonInput(12, 3, 4, "MED");
        var (trend, reason) = RiskTrendCalculator.Calculate(previous, current, true);

        Assert.Equal(RiskTrend.Increasing, trend);
        Assert.Contains("مصادر", reason);
    }

    [Fact]
    public void Calculate_ReturnsIncreasing_WhenComponentsShiftDespiteSameScore()
    {
        var previous = new RiskAssessmentComparisonInput(12, 3, 4, "MED");
        var current = new RiskAssessmentComparisonInput(12, 4, 3, "MED");
        var (trend, reason) = RiskTrendCalculator.Calculate(previous, current, false);

        Assert.Equal(RiskTrend.Increasing, trend);
        Assert.Contains("تغيّرت", reason);
    }
}
