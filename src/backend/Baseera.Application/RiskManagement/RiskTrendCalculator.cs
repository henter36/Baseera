namespace Baseera.Application.RiskManagement;

using Baseera.Domain.RiskManagement;

public readonly record struct RiskAssessmentComparisonInput(decimal Score, int LikelihoodNumericValue, int MaxImpactNumericValue, string RatingBandCode);

/// <summary>
/// Deterministic trend rules — never AI/heuristic. Trend and its Arabic reason are derived only from the
/// last two *approved* assessments plus whether new source links appeared since the previous one.
/// </summary>
public static class RiskTrendCalculator
{
    public static (RiskTrend Trend, string ReasonAr) Calculate(
        RiskAssessmentComparisonInput? previous,
        RiskAssessmentComparisonInput current,
        bool hasNewSourceLinksSincePrevious)
    {
        if (previous is null)
        {
            return (RiskTrend.Unknown, "لا يوجد تقييم معتمد سابق للمقارنة.");
        }

        var prev = previous.Value;
        if (current.Score > prev.Score)
        {
            return (RiskTrend.Increasing, $"ارتفعت الدرجة المحسوبة من {prev.Score} إلى {current.Score} مقارنة بآخر تقييم معتمد.");
        }

        if (current.Score < prev.Score)
        {
            return (RiskTrend.Decreasing, $"انخفضت الدرجة المحسوبة من {prev.Score} إلى {current.Score} مقارنة بآخر تقييم معتمد.");
        }

        if (current.LikelihoodNumericValue != prev.LikelihoodNumericValue || current.MaxImpactNumericValue != prev.MaxImpactNumericValue)
        {
            return (RiskTrend.Increasing, "تغيّرت مكوّنات الاحتمالية أو الأثر رغم ثبات الدرجة الإجمالية، مما يستدعي المتابعة.");
        }

        if (hasNewSourceLinksSincePrevious)
        {
            return (RiskTrend.Increasing, "ظهرت مصادر أو أدلة جديدة مرتبطة بالخطر منذ آخر تقييم رغم ثبات الدرجة.");
        }

        return (RiskTrend.Stable, "الدرجة والتصنيف دون تغيير منذ آخر تقييم معتمد، ولا توجد مصادر جديدة.");
    }
}
