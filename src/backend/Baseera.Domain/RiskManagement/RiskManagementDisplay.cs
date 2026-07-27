namespace Baseera.Domain.RiskManagement;

/// <summary>
/// Shared *display-only* label constants reused across otherwise-unrelated enum mappings in
/// <see cref="RiskManagementDisplay"/>. This shares only the rendered Arabic string, not any Domain
/// semantics — RiskStatus.Draft, AssessmentStatus.Draft, TreatmentPlanStatus.Draft, and
/// RiskTreatmentActionStatus.Draft remain four independent states that happen to render identically.
/// </summary>
internal static class RiskDisplayLabels
{
    public const string DraftAr = "مسودة";
}

public static class RiskManagementDisplay
{
    public static string StatusAr(RiskStatus status) => status switch
    {
        RiskStatus.Draft => RiskDisplayLabels.DraftAr,
        RiskStatus.UnderAssessment => "قيد التقييم",
        RiskStatus.PendingReview => "بانتظار المراجعة",
        RiskStatus.Active => "نشط",
        RiskStatus.UnderTreatment => "قيد المعالجة",
        RiskStatus.Monitoring => "قيد المتابعة",
        RiskStatus.PendingAcceptance => "بانتظار القبول",
        RiskStatus.Accepted => "مقبول",
        RiskStatus.PendingClosure => "بانتظار الإغلاق",
        RiskStatus.Closed => "مغلق",
        RiskStatus.Reopened => "معاد فتحه",
        RiskStatus.Archived => "مؤرشف",
        _ => status.ToString()
    };

    public static string RiskTypeAr(RiskType type) => type switch
    {
        RiskType.Security => "أمني",
        RiskType.Operational => "تشغيلي",
        RiskType.Safety => "سلامة",
        RiskType.Health => "صحي",
        RiskType.Capacity => "طاقة استيعابية",
        RiskType.Workforce => "قوى بشرية",
        RiskType.Resource => "موارد",
        RiskType.Technology => "تقني",
        RiskType.InformationSecurity => "أمن معلومات",
        RiskType.Compliance => "امتثال",
        RiskType.Project => "مشروع",
        RiskType.Financial => "مالي",
        RiskType.Reputation => "سمعة",
        RiskType.Emergency => "طوارئ",
        RiskType.Strategic => "استراتيجي",
        _ => "أخرى"
    };

    public static string TreatmentStrategyAr(TreatmentStrategy strategy) => strategy switch
    {
        TreatmentStrategy.Avoid => "تجنب",
        TreatmentStrategy.Reduce => "تقليل",
        TreatmentStrategy.Transfer => "نقل",
        TreatmentStrategy.Accept => "قبول",
        TreatmentStrategy.Contingency => "خطة طوارئ",
        TreatmentStrategy.Monitor => "متابعة",
        _ => strategy.ToString()
    };

    public static string RatingSeverityAr(RiskRatingSeverity severity) => severity switch
    {
        RiskRatingSeverity.Low => "منخفضة",
        RiskRatingSeverity.Medium => "متوسطة",
        RiskRatingSeverity.High => "عالية",
        RiskRatingSeverity.Critical => "حرجة",
        _ => severity.ToString()
    };

    public static string AssessmentTypeAr(AssessmentType type) => type switch
    {
        AssessmentType.Inherent => "أصلي",
        AssessmentType.Current => "حالي",
        AssessmentType.Residual => "متبقٍ",
        AssessmentType.PostIncident => "بعد وقوعة",
        AssessmentType.PeriodicReview => "مراجعة دورية",
        AssessmentType.Closure => "إغلاق",
        _ => type.ToString()
    };

    public static string AssessmentStatusAr(AssessmentStatus status) => status switch
    {
        AssessmentStatus.Draft => RiskDisplayLabels.DraftAr,
        AssessmentStatus.PendingReview => "بانتظار المراجعة",
        AssessmentStatus.Reviewed => "تمت مراجعته",
        AssessmentStatus.Approved => "معتمد",
        AssessmentStatus.Rejected => "مرفوض",
        AssessmentStatus.Superseded => "تم استبداله",
        _ => status.ToString()
    };

    public static string ControlTypeAr(RiskControlType type) => type switch
    {
        RiskControlType.Preventive => "وقائي",
        RiskControlType.Detective => "كاشف",
        RiskControlType.Corrective => "تصحيحي",
        RiskControlType.Deterrent => "رادع",
        RiskControlType.Recovery => "تعافٍ",
        RiskControlType.Compensating => "تعويضي",
        _ => type.ToString()
    };

    public static string ControlEffectivenessAr(ControlEffectiveness effectiveness) => effectiveness switch
    {
        ControlEffectiveness.Effective => "فعّال",
        ControlEffectiveness.PartiallyEffective => "فعّال جزئيًا",
        ControlEffectiveness.Ineffective => "غير فعّال",
        ControlEffectiveness.NotTested => "لم يُختبر",
        ControlEffectiveness.Unknown => "غير معروف",
        _ => effectiveness.ToString()
    };

    public static string TreatmentPlanStatusAr(TreatmentPlanStatus status) => status switch
    {
        TreatmentPlanStatus.Draft => RiskDisplayLabels.DraftAr,
        TreatmentPlanStatus.PendingApproval => "بانتظار الاعتماد",
        TreatmentPlanStatus.Approved => "معتمدة",
        TreatmentPlanStatus.InProgress => "قيد التنفيذ",
        TreatmentPlanStatus.Blocked => "معطّلة",
        TreatmentPlanStatus.Overdue => "متأخرة",
        TreatmentPlanStatus.Completed => "مكتملة",
        TreatmentPlanStatus.Cancelled => "ملغاة",
        TreatmentPlanStatus.Rejected => "مرفوضة",
        _ => status.ToString()
    };

    public static string TreatmentActionStatusAr(RiskTreatmentActionStatus status) => status switch
    {
        RiskTreatmentActionStatus.Draft => RiskDisplayLabels.DraftAr,
        RiskTreatmentActionStatus.Assigned => "مسندة",
        RiskTreatmentActionStatus.InProgress => "قيد التنفيذ",
        RiskTreatmentActionStatus.Blocked => "معطّلة",
        RiskTreatmentActionStatus.PendingVerification => "بانتظار التحقق",
        RiskTreatmentActionStatus.Completed => "مكتملة",
        RiskTreatmentActionStatus.Cancelled => "ملغاة",
        _ => status.ToString()
    };

    public static string ReviewTypeAr(RiskReviewType type) => type switch
    {
        RiskReviewType.AssessmentReview => "مراجعة تقييم",
        RiskReviewType.TreatmentApproval => "اعتماد خطة معالجة",
        RiskReviewType.RiskAcceptance => "قبول خطر",
        RiskReviewType.ClosureApproval => "اعتماد إغلاق",
        RiskReviewType.PeriodicReview => "مراجعة دورية",
        RiskReviewType.ReopenReview => "مراجعة إعادة فتح",
        _ => type.ToString()
    };

    public static string ReviewDecisionAr(RiskReviewDecision decision) => decision switch
    {
        RiskReviewDecision.Approved => "معتمد",
        RiskReviewDecision.ApprovedWithConditions => "معتمد بشروط",
        RiskReviewDecision.Returned => "معاد",
        RiskReviewDecision.Rejected => "مرفوض",
        _ => decision.ToString()
    };

    public static string TrendAr(RiskTrend trend) => trend switch
    {
        RiskTrend.Increasing => "متصاعد",
        RiskTrend.Stable => "مستقر",
        RiskTrend.Decreasing => "متراجع",
        RiskTrend.Unknown => "غير معروف",
        _ => trend.ToString()
    };
}
