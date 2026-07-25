namespace Baseera.Application.Resources;

using Baseera.Domain.Resources;

public sealed record ResourceReadinessInputs(
    int TotalRegistered,
    int Available,
    int Standby,
    int InUse,
    int Reserved,
    int UnderMaintenance,
    int OutOfService,
    int AwaitingParts,
    int Unknown,
    int Retired,
    int Transferred,
    int Required,
    int MissingDataRecords);

public sealed record ResourceReadinessResult(
    int Operational,
    int Gap,
    int Surplus,
    decimal? ReadinessRate,
    decimal? AvailabilityRate,
    decimal DataCompletenessRate);

public static class ResourceReadinessPolicy
{
    public static bool IsOperational(ResourceStatus status) =>
        status is ResourceStatus.Available
            or ResourceStatus.InUse
            or ResourceStatus.Standby
            or ResourceStatus.Reserved;

    public static bool IsAvailable(ResourceStatus status) =>
        status is ResourceStatus.Available or ResourceStatus.Standby;

    public static bool IsInScopeDenominator(ResourceStatus status) =>
        status is not ResourceStatus.Retired and not ResourceStatus.Transferred;

    public static ResourceReadinessResult Calculate(ResourceReadinessInputs inputs)
    {
        var operational = inputs.Available + inputs.Standby + inputs.InUse + inputs.Reserved;
        var inScope = inputs.TotalRegistered - inputs.Retired - inputs.Transferred;
        var gap = inputs.Required > 0 ? Math.Max(0, inputs.Required - operational) : 0;
        var surplus = inputs.Required > 0 ? Math.Max(0, operational - inputs.Required) : 0;
        decimal? readinessRate = inputs.Required > 0 ? Rate(operational, inputs.Required) : null;
        decimal? availabilityRate = inScope > 0 ? Rate(inputs.Available + inputs.Standby, inScope) : null;
        var completeness = inScope > 0
            ? Math.Round((decimal)(inScope - inputs.MissingDataRecords) / inScope, 4, MidpointRounding.AwayFromZero)
            : 0m;

        return new ResourceReadinessResult(
            operational,
            gap,
            surplus,
            readinessRate,
            availabilityRate,
            Math.Clamp(completeness, 0m, 1m));
    }

    private static decimal Rate(int numerator, int denominator) =>
        denominator <= 0
            ? 0m
            : Math.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero);
}

public static class ResourceStatusStateMachine
{
    public static bool CanTransition(ResourceStatus from, ResourceStatus to, bool hasMaintenanceReason)
    {
        if (from == to)
        {
            return true;
        }

        if (from == ResourceStatus.Retired && to != ResourceStatus.Unknown)
        {
            return false;
        }

        if (to is ResourceStatus.UnderMaintenance or ResourceStatus.AwaitingParts)
        {
            return hasMaintenanceReason;
        }

        return true;
    }

    public static string StatusAr(ResourceStatus status) =>
        status switch
        {
            ResourceStatus.Available => "متاح",
            ResourceStatus.InUse => "قيد الاستخدام",
            ResourceStatus.Standby => "احتياطي",
            ResourceStatus.Reserved => "محجوز",
            ResourceStatus.UnderInspection => "تحت الفحص",
            ResourceStatus.UnderMaintenance => "تحت الصيانة",
            ResourceStatus.OutOfService => "خارج الخدمة",
            ResourceStatus.AwaitingParts => "بانتظار قطع",
            ResourceStatus.Lost => "مفقود",
            ResourceStatus.Transferred => "منقول",
            ResourceStatus.Retired => "متقاعد",
            _ => "غير معروف"
        };

    public static string TypeAr(ResourceType type) =>
        type switch
        {
            ResourceType.Vehicle => "المركبات",
            ResourceType.CommunicationDevice => "أجهزة الاتصال",
            ResourceType.OperationalEquipment => "المعدات التشغيلية",
            ResourceType.SecurityEquipment => "المعدات الأمنية غير الأسلحة",
            ResourceType.FacilityAsset => "المرافق والأصول الثابتة",
            _ => "موارد"
        };
}
