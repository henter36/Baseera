namespace Baseera.Application.Workforce;

using Baseera.Domain.Workforce;

public sealed record WorkforceCoverageInputs(
    int Required,
    int MinimumSafe,
    int Assigned,
    int Scheduled,
    int Present,
    int OperationallyAvailable,
    int Qualified,
    int Unqualified,
    int Absent,
    int OnLeave,
    int InTraining,
    int Restricted,
    int Overtime,
    bool HasRequirementBaseline,
    bool HasPresenceSignal);

public sealed record WorkforceCoverageResult(
    int Gap,
    int SafeGap,
    decimal? CoverageRate,
    decimal? QualificationCoverage,
    WorkforceCoverageStatus Status);

public static class WorkforceReadinessPolicy
{
    public static bool IsEmploymentOperationallyEligible(EmploymentStatus status, bool isOperational) =>
        isOperational
        && status is EmploymentStatus.Active or EmploymentStatus.SecondedIn;

    public static bool IsAvailabilityBlocking(AvailabilityType type, bool affectsOperationalAvailability)
    {
        if (!affectsOperationalAvailability)
        {
            return false;
        }

        return type is AvailabilityType.AnnualLeave
            or AvailabilityType.SickLeave
            or AvailabilityType.Training
            or AvailabilityType.ExternalAssignment
            or AvailabilityType.Suspended
            or AvailabilityType.EmergencyLeave
            or AvailabilityType.UnexcusedAbsence;
    }

    public static bool IsQualificationValidForRole(
        QualificationStatus status,
        Guid? qualificationRoleDefinitionId,
        Guid requiredRoleDefinitionId,
        DateTimeOffset asOfUtc,
        DateTimeOffset? expiresAtUtc)
    {
        if (qualificationRoleDefinitionId.HasValue && qualificationRoleDefinitionId.Value != requiredRoleDefinitionId)
        {
            return false;
        }

        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= asOfUtc)
        {
            return false;
        }

        return status is QualificationStatus.Valid or QualificationStatus.ExpiringSoon;
    }

    public static bool IsWithinShiftWindow(
        TimeOnly localTime,
        TimeOnly startLocalTime,
        TimeOnly endLocalTime,
        bool crossesMidnight)
    {
        if (crossesMidnight)
        {
            return localTime >= startLocalTime || localTime < endLocalTime;
        }

        return localTime >= startLocalTime && localTime < endLocalTime;
    }

    public static WorkforceCoverageResult Calculate(WorkforceCoverageInputs inputs)
    {
        if (!inputs.HasRequirementBaseline)
        {
            return new WorkforceCoverageResult(0, 0, null, null, WorkforceCoverageStatus.Unknown);
        }

        var gap = inputs.Required > 0 ? Math.Max(0, inputs.Required - inputs.OperationallyAvailable) : 0;
        var safeGap = Math.Max(0, inputs.MinimumSafe - inputs.OperationallyAvailable);
        decimal? coverageRate = inputs.Required > 0
            ? Rate(inputs.OperationallyAvailable, inputs.Required)
            : null;
        var qualifiedDenominator = inputs.Qualified + inputs.Unqualified;
        decimal? qualificationCoverage = qualifiedDenominator > 0
            ? Rate(inputs.Qualified, qualifiedDenominator)
            : null;

        var status = ResolveCoverageStatus(coverageRate, inputs.HasPresenceSignal || inputs.Scheduled > 0 || inputs.Assigned > 0);
        return new WorkforceCoverageResult(gap, safeGap, coverageRate, qualificationCoverage, status);
    }

    public static WorkforceCoverageStatus ResolveCoverageStatus(decimal? coverageRate, bool hasOperationalSignal)
    {
        if (coverageRate is null || !hasOperationalSignal)
        {
            return WorkforceCoverageStatus.Unknown;
        }

        if (coverageRate >= 1.0m)
        {
            return WorkforceCoverageStatus.Ready;
        }

        if (coverageRate >= 0.85m)
        {
            return WorkforceCoverageStatus.Attention;
        }

        if (coverageRate >= 0.70m)
        {
            return WorkforceCoverageStatus.Critical;
        }

        return WorkforceCoverageStatus.Unsafe;
    }

    public static string ResolveFreshness(int totalMembers, int staleRecords)
    {
        if (totalMembers == 0)
        {
            return "missing";
        }

        if (staleRecords > 0)
        {
            return "partial";
        }

        return "current";
    }

    public static string ResolveConfidence(int totalMembers, int mediumSignalCount)
    {
        if (totalMembers == 0)
        {
            return "unknown";
        }

        if (mediumSignalCount > 0)
        {
            return "medium";
        }

        return "high";
    }

    private static decimal Rate(int numerator, int denominator) =>
        denominator <= 0
            ? 0m
            : Math.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero);
}
