namespace Baseera.UnitTests.RiskManagement;

using Baseera.Application.RiskManagement;
using Baseera.Domain.RiskManagement;

public sealed class RiskScoringEngineTests
{
    [Fact]
    public void CalculateScore_MaximumImpactFormula_UsesLikelihoodTimesHighestImpact()
    {
        var impacts = new[]
        {
            new RiskImpactInput(Guid.NewGuid(), 2),
            new RiskImpactInput(Guid.NewGuid(), 5),
            new RiskImpactInput(Guid.NewGuid(), 3)
        };

        var score = RiskScoringEngine.CalculateScore(ScoreFormulaType.LikelihoodTimesMaximumImpact, 4, impacts);

        Assert.Equal(20m, score);
    }

    [Fact]
    public void CalculateScore_WeightedImpactFormula_UsesWeightedAverage()
    {
        var dimensionA = Guid.NewGuid();
        var dimensionB = Guid.NewGuid();
        var impacts = new[]
        {
            new RiskImpactInput(dimensionA, 4),
            new RiskImpactInput(dimensionB, 2)
        };
        var weights = new Dictionary<Guid, decimal> { [dimensionA] = 0.75m, [dimensionB] = 0.25m };

        // weighted average impact = (4*0.75 + 2*0.25) / (0.75+0.25) = 3.5
        var score = RiskScoringEngine.CalculateScore(ScoreFormulaType.LikelihoodTimesWeightedImpact, 3, impacts, weights);

        Assert.Equal(10.5m, score);
    }

    [Fact]
    public void CalculateScore_WeightedImpactFormula_RequiresWeights()
    {
        var impacts = new[] { new RiskImpactInput(Guid.NewGuid(), 4) };
        Assert.Throws<InvalidOperationException>(() =>
            RiskScoringEngine.CalculateScore(ScoreFormulaType.LikelihoodTimesWeightedImpact, 3, impacts));
    }

    [Fact]
    public void CalculateScore_WeightedImpactFormula_RequiresWeightForEveryAssessedDimension()
    {
        var dimensionA = Guid.NewGuid();
        var dimensionB = Guid.NewGuid();
        var impacts = new[] { new RiskImpactInput(dimensionA, 4), new RiskImpactInput(dimensionB, 2) };
        var weights = new Dictionary<Guid, decimal> { [dimensionA] = 1m };

        Assert.Throws<InvalidOperationException>(() =>
            RiskScoringEngine.CalculateScore(ScoreFormulaType.LikelihoodTimesWeightedImpact, 3, impacts, weights));
    }

    [Fact]
    public void CalculateScore_RejectsNonPositiveLikelihood() =>
        Assert.Throws<InvalidOperationException>(() =>
            RiskScoringEngine.CalculateScore(ScoreFormulaType.LikelihoodTimesMaximumImpact, 0, [new RiskImpactInput(Guid.NewGuid(), 3)]));

    [Fact]
    public void CalculateScore_RejectsEmptyImpactList() =>
        Assert.Throws<InvalidOperationException>(() =>
            RiskScoringEngine.CalculateScore(ScoreFormulaType.LikelihoodTimesMaximumImpact, 3, []));

    // Overflow contract exercised throughout the tests below: every band but the last is
    // [MinimumScore, MaximumScore) (min inclusive, max exclusive); the last band is
    // [MinimumScore, MaximumScore] (min AND max inclusive); anything above the last band's
    // MaximumScore is rejected outright as a matrix configuration defect, not silently
    // absorbed into the top band.
    private static readonly List<RiskRatingBand> FourBandMatrix =
    [
        new() { Code = "LOW", MinimumScore = 1, MaximumScore = 5 },
        new() { Code = "MED", MinimumScore = 5, MaximumScore = 12 },
        new() { Code = "HIGH", MinimumScore = 12, MaximumScore = 20 },
        new() { Code = "CRIT", MinimumScore = 20, MaximumScore = 25 }
    ];

    [Fact]
    public void SelectRatingBand_PicksBandContainingScore() =>
        Assert.Equal("MED", RiskScoringEngine.SelectRatingBand(FourBandMatrix, 10).Code);

    [Fact]
    public void SelectRatingBand_ScoreBelowFirstMinimum_Throws() =>
        Assert.Throws<InvalidOperationException>(() => RiskScoringEngine.SelectRatingBand(FourBandMatrix, 0.99m));

    [Fact]
    public void SelectRatingBand_ScoreExactlyFirstMinimum_ReturnsFirstBand() =>
        Assert.Equal("LOW", RiskScoringEngine.SelectRatingBand(FourBandMatrix, 1m).Code);

    [Fact]
    public void SelectRatingBand_TreatsSharedBoundaryAsBelongingToTheHigherBand()
    {
        Assert.Equal("LOW", RiskScoringEngine.SelectRatingBand(FourBandMatrix, 4.99m).Code);
        Assert.Equal("MED", RiskScoringEngine.SelectRatingBand(FourBandMatrix, 5m).Code);
    }

    [Fact]
    public void SelectRatingBand_ScoreExactlyFinalMaximum_ReturnsFinalBand() =>
        Assert.Equal("CRIT", RiskScoringEngine.SelectRatingBand(FourBandMatrix, 25m).Code);

    [Fact]
    public void SelectRatingBand_ScoreAboveFinalMaximum_Throws() =>
        Assert.Throws<InvalidOperationException>(() => RiskScoringEngine.SelectRatingBand(FourBandMatrix, 25.01m));

    [Fact]
    public void SelectRatingBand_ResolvesFractionalScoreFromWeightedFormula()
    {
        // Likelihood 3 x weighted-average impact 2.33 = 6.99, exercising the exact scenario the
        // integer "+1" contiguity rule used to miss for LikelihoodTimesWeightedImpact.
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 1, MaximumScore = 5 },
            new() { Code = "MED", MinimumScore = 5, MaximumScore = 12 }
        };

        Assert.Equal("MED", RiskScoringEngine.SelectRatingBand(bands, 6.99m).Code);
    }

    [Fact]
    public void SelectRatingBand_ThrowsWhenScoreIsBelowEveryBand()
    {
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 10, MaximumScore = 20 }
        };

        Assert.Throws<InvalidOperationException>(() => RiskScoringEngine.SelectRatingBand(bands, 1));
    }

    [Fact]
    public void ValidateRatingBands_RejectsOverlaps()
    {
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 1, MaximumScore = 10 },
            new() { Code = "MED", MinimumScore = 8, MaximumScore = 20 }
        };

        Assert.Throws<InvalidOperationException>(() => RiskMatrixValidation.ValidateRatingBands(bands));
    }

    [Fact]
    public void ValidateRatingBands_RejectsGaps()
    {
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 1, MaximumScore = 5 },
            new() { Code = "MED", MinimumScore = 10, MaximumScore = 20 }
        };

        Assert.Throws<InvalidOperationException>(() => RiskMatrixValidation.ValidateRatingBands(bands));
    }

    [Fact]
    public void ValidateRatingBands_RejectsEmptySet() =>
        Assert.Throws<InvalidOperationException>(() => RiskMatrixValidation.ValidateRatingBands([]));

    [Fact]
    public void ValidateRatingBands_AcceptsContiguousNonOverlappingBands()
    {
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 1, MaximumScore = 5 },
            new() { Code = "MED", MinimumScore = 5, MaximumScore = 12 }
        };

        RiskMatrixValidation.ValidateRatingBands(bands);
    }

    [Fact]
    public void ValidateWeights_RequiresPositiveWeightForEveryDimension()
    {
        var dimensionA = Guid.NewGuid();
        var dimensionB = Guid.NewGuid();
        var weights = new Dictionary<Guid, decimal> { [dimensionA] = 0.5m, [dimensionB] = 0m };

        Assert.Throws<InvalidOperationException>(() =>
            RiskMatrixValidation.ValidateWeights(weights, [dimensionA, dimensionB]));
    }
}
