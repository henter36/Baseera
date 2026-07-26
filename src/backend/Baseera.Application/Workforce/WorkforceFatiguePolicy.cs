namespace Baseera.Application.Workforce;

public static class WorkforceFatiguePolicy
{
    public const int QualificationExpiringSoonDays = 30;
    public const decimal ExcessiveOvertimeHoursThreshold = 12m;
    public const int ConsecutiveShiftsWithoutRestThreshold = 3;

    public const string ExcessiveOvertimeHours = "ExcessiveOvertimeHours";
    public const string ConsecutiveShiftsWithoutRest = "ConsecutiveShiftsWithoutRest";
    public const string SinglePointOfFailure = "SinglePointOfFailure";
    public const string QualificationExpiringSoon = "QualificationExpiringSoonDays";

    public sealed record FatigueIndicatorInputs(
        decimal OvertimeHoursInWindow,
        int ConsecutiveShiftCount,
        int CriticalRoleCoverageCount,
        int CriticalRoleRequiredCount,
        DateTimeOffset? NearestQualificationExpiryUtc,
        DateTimeOffset AsOfUtc);

    public static IReadOnlyList<string> Evaluate(FatigueIndicatorInputs inputs)
    {
        var indicators = new List<string>(4);
        if (inputs.OvertimeHoursInWindow >= ExcessiveOvertimeHoursThreshold)
        {
            indicators.Add(ExcessiveOvertimeHours);
        }

        if (inputs.ConsecutiveShiftCount >= ConsecutiveShiftsWithoutRestThreshold)
        {
            indicators.Add(ConsecutiveShiftsWithoutRest);
        }

        if (inputs.CriticalRoleRequiredCount > 0 && inputs.CriticalRoleCoverageCount <= 1)
        {
            indicators.Add(SinglePointOfFailure);
        }

        if (inputs.NearestQualificationExpiryUtc is { } expiry
            && expiry > inputs.AsOfUtc
            && expiry <= inputs.AsOfUtc.AddDays(QualificationExpiringSoonDays))
        {
            indicators.Add(QualificationExpiringSoon);
        }

        return indicators;
    }
}
