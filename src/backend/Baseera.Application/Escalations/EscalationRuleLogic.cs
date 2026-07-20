namespace Baseera.Application.Escalations;

using Baseera.Application.Common;
using Baseera.Domain.Escalations;

public static class EscalationRuleLogic
{
    public static bool IsDueSoon(DateTimeOffset dueAtUtc, DateTimeOffset nowUtc, int thresholdDays) =>
        dueAtUtc >= nowUtc && dueAtUtc <= nowUtc.AddDays(thresholdDays);

    public static bool IsOverdue(DateTimeOffset dueAtUtc, DateTimeOffset nowUtc)
    {
        var dueSaudiDate = TimeZones.ToSaudi(dueAtUtc).Date;
        var nowSaudiDate = TimeZones.ToSaudi(nowUtc).Date;
        return dueSaudiDate < nowSaudiDate;
    }

    public static string TargetCycleKey(EscalationTargetType targetType, Guid targetId, DateTimeOffset cycleStartedAtUtc) =>
        $"{targetType}:{targetId:N}:{cycleStartedAtUtc.UtcTicks}";

    public static string OccurrenceKey(Guid ruleId, EscalationTargetType targetType, Guid targetId, string targetCycleKey, int occurrenceNumber) =>
        $"{ruleId:N}:{targetType}:{targetId:N}:{targetCycleKey}:{occurrenceNumber}";

    public static string DeduplicationKey(string occurrenceKey, Guid userId) =>
        $"{occurrenceKey}:user:{userId:N}:inapp";
}
