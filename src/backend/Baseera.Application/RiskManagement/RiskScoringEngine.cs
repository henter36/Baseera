namespace Baseera.Application.RiskManagement;

using Baseera.Domain.RiskManagement;

public readonly record struct RiskImpactInput(Guid ImpactDimensionId, int NumericValue);

/// <summary>
/// Deterministic, versioned score calculation. The score is always computed here, on the server, from a
/// specific matrix's likelihood value and per-dimension impact values — never accepted from a client and
/// never evaluated as free-form script/expression (principle: no ambiguous or manual risk score).
/// </summary>
public static class RiskScoringEngine
{
    public static decimal CalculateScore(
        ScoreFormulaType formula,
        int likelihoodNumericValue,
        IReadOnlyList<RiskImpactInput> impacts,
        IReadOnlyDictionary<Guid, decimal>? dimensionWeights = null)
    {
        if (likelihoodNumericValue <= 0)
        {
            throw new InvalidOperationException("قيمة الاحتمالية يجب أن تكون أكبر من صفر.");
        }

        if (impacts.Count == 0)
        {
            throw new InvalidOperationException("يلزم تقييم بُعد أثر واحد على الأقل لحساب الدرجة.");
        }

        var impactFactor = formula switch
        {
            ScoreFormulaType.LikelihoodTimesMaximumImpact => impacts.Max(i => i.NumericValue),
            ScoreFormulaType.LikelihoodTimesWeightedImpact => WeightedAverageImpact(impacts, dimensionWeights),
            _ => throw new NotSupportedException($"صيغة الاحتساب {formula} غير مدعومة.")
        };

        return likelihoodNumericValue * impactFactor;
    }

    private static decimal WeightedAverageImpact(IReadOnlyList<RiskImpactInput> impacts, IReadOnlyDictionary<Guid, decimal>? weights)
    {
        if (weights is null || weights.Count == 0)
        {
            throw new InvalidOperationException("أوزان الأبعاد مطلوبة لاستخدام صيغة الأثر الموزون.");
        }

        decimal weightedSum = 0;
        decimal totalWeight = 0;
        foreach (var impact in impacts)
        {
            if (!weights.TryGetValue(impact.ImpactDimensionId, out var weight))
            {
                throw new InvalidOperationException("لا يوجد وزن معرّف لأحد أبعاد الأثر المقيّمة.");
            }

            weightedSum += impact.NumericValue * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0)
        {
            throw new InvalidOperationException("مجموع أوزان الأبعاد يجب أن يكون أكبر من صفر.");
        }

        return weightedSum / totalWeight;
    }

    /// <summary>
    /// Rating bands must be contiguous and non-overlapping (validated at matrix activation time), so exactly
    /// one band always matches. Scores are not assumed to be integers — the weighted-impact formula can
    /// produce fractional values — so every band but the last treats its upper bound as exclusive; the last
    /// band's upper bound stays inclusive so the top of the range is always covered.
    /// </summary>
    public static RiskRatingBand SelectRatingBand(IReadOnlyList<RiskRatingBand> bands, decimal score)
    {
        var ordered = bands.OrderBy(b => b.MinimumScore).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var isLast = i == ordered.Count - 1;
            var upperInclusive = isLast || score < ordered[i + 1].MinimumScore;
            if (score >= ordered[i].MinimumScore && upperInclusive)
            {
                return ordered[i];
            }
        }

        throw new InvalidOperationException("لا يوجد نطاق تصنيف يغطي هذه الدرجة ضمن المصفوفة الحالية. يجب مراجعة إعداد المصفوفة.");
    }
}

/// <summary>Structural validation applied before a matrix can move to PendingApproval/Active.</summary>
public static class RiskMatrixValidation
{
    public static void ValidateRatingBands(IReadOnlyList<RiskRatingBand> bands)
    {
        if (bands.Count == 0)
        {
            throw new InvalidOperationException("يجب تعريف نطاق تصنيف واحد على الأقل قبل اعتماد المصفوفة.");
        }

        var ordered = bands.OrderBy(b => b.MinimumScore).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].MinimumScore > ordered[i].MaximumScore)
            {
                throw new InvalidOperationException($"نطاق التصنيف {ordered[i].Code} غير صالح: الحد الأدنى أكبر من الحد الأعلى.");
            }

            // Bands must touch with no overlap; SelectRatingBand treats every band's upper bound as exclusive
            // except the last, so a fractional score (from the weighted-impact formula) at a shared boundary
            // still resolves to exactly one band.
            if (i > 0 && ordered[i].MinimumScore != ordered[i - 1].MaximumScore)
            {
                throw new InvalidOperationException("نطاقات التصنيف يجب أن تكون متلامسة دون تداخل أو فجوة (الحد الأدنى للنطاق التالي = الحد الأعلى للنطاق السابق).");
            }
        }
    }

    public static void ValidateWeights(IReadOnlyDictionary<Guid, decimal> weights, IReadOnlyList<Guid> dimensionIds)
    {
        foreach (var dimensionId in dimensionIds)
        {
            if (!weights.TryGetValue(dimensionId, out var weight) || weight <= 0)
            {
                throw new InvalidOperationException("يجب تحديد وزن أكبر من صفر لكل بُعد أثر معرّف في المصفوفة.");
            }
        }
    }
}
