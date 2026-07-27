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

    [Fact]
    public void SelectRatingBand_PicksBandContainingScore()
    {
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 1, MaximumScore = 5 },
            new() { Code = "MED", MinimumScore = 5, MaximumScore = 12 },
            new() { Code = "HIGH", MinimumScore = 12, MaximumScore = 20 },
            new() { Code = "CRIT", MinimumScore = 20, MaximumScore = 25 }
        };

        Assert.Equal("MED", RiskScoringEngine.SelectRatingBand(bands, 10).Code);
        Assert.Equal("CRIT", RiskScoringEngine.SelectRatingBand(bands, 25).Code);
    }

    [Fact]
    public void SelectRatingBand_TreatsSharedBoundaryAsBelongingToTheHigherBand()
    {
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 1, MaximumScore = 5 },
            new() { Code = "MED", MinimumScore = 5, MaximumScore = 12 }
        };

        Assert.Equal("LOW", RiskScoringEngine.SelectRatingBand(bands, 4.99m).Code);
        Assert.Equal("MED", RiskScoringEngine.SelectRatingBand(bands, 5m).Code);
    }

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
        // The last band's upper bound is intentionally open-ended (it always covers "at or above its
        // minimum"), so only a score below the lowest band's minimum can be unmatched.
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 10, MaximumScore = 20 }
        };

        Assert.Throws<InvalidOperationException>(() => RiskScoringEngine.SelectRatingBand(bands, 1));
    }

    [Fact]
    public void SelectRatingBand_LastBandCoversAnyScoreAtOrAboveItsMinimum()
    {
        var bands = new List<RiskRatingBand>
        {
            new() { Code = "LOW", MinimumScore = 1, MaximumScore = 5 },
            new() { Code = "CRIT", MinimumScore = 5, MaximumScore = 25 }
        };

        Assert.Equal("CRIT", RiskScoringEngine.SelectRatingBand(bands, 999).Code);
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
