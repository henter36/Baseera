namespace Baseera.Application.Workspaces;

internal static class FacilityWorkspaceRules
{
    public const int CriticalThreshold = 5;
    public const int InterventionThreshold = 3;
    public const int FollowUpThreshold = 1;

    public static FacilityStatusResult ClassifyStatus(FacilityWorkspaceMetrics metrics)
    {
        var score = PriorityIssueCount(metrics);
        if (metrics.Alerts.CriticalEscalations > 0 || metrics.Notes.CriticalNotes >= CriticalThreshold)
        {
            return new FacilityStatusResult("critical", "حرجة");
        }

        if (score >= InterventionThreshold)
        {
            return new FacilityStatusResult("intervention", "تتطلب تدخلاً");
        }

        if (score >= FollowUpThreshold)
        {
            return new FacilityStatusResult("follow-up", "تحتاج متابعة");
        }

        return new FacilityStatusResult("stable", "مستقرة");
    }

    public static int PriorityIssueCount(FacilityWorkspaceMetrics metrics) =>
        metrics.Notes.CriticalNotes +
        metrics.Notes.OverdueNotes +
        metrics.CorrectiveActions.OverdueActions +
        metrics.Alerts.CriticalEscalations +
        metrics.FormCompliance.OverdueForms;

    public static string TopDriver(FacilityWorkspaceMetrics metrics)
    {
        var drivers = new[]
        {
            ("الملاحظات المتأخرة", metrics.Notes.OverdueNotes),
            ("الملاحظات الحرجة", metrics.Notes.CriticalNotes),
            ("الإجراءات التصحيحية المتأخرة", metrics.CorrectiveActions.OverdueActions),
            ("التصعيدات الحرجة", metrics.Alerts.CriticalEscalations),
            ("النماذج المتأخرة", metrics.FormCompliance.OverdueForms)
        };

        var top = drivers.OrderByDescending(driver => driver.Item2).First();
        return top.Item2 == 0 ? "لا يوجد عامل ضاغط بارز ضمن البيانات الحالية." : $"{top.Item1}: {top.Item2}";
    }

    public static string ChangeSummary(FacilityWorkspaceMetrics metrics) =>
        metrics.Notes.NewInPeriod == 0
            ? "لم تسجل ملاحظات جديدة ضمن الفترة المحددة."
            : $"سجلت {metrics.Notes.NewInPeriod} ملاحظة جديدة ضمن الفترة المحددة.";

    public static string TopPendingAction(FacilityWorkspaceMetrics metrics)
    {
        if (metrics.CorrectiveActions.OverdueActions > 0)
        {
            return "متابعة الإجراءات التصحيحية المتأخرة.";
        }

        if (metrics.Notes.UnassignedNotes > 0)
        {
            return "إسناد الملاحظات المفتوحة غير المسندة.";
        }

        if (metrics.FormCompliance.OverdueForms > 0)
        {
            return "معالجة النماذج المتأخرة.";
        }

        return "لا يوجد إجراء معلق بارز.";
    }

    public static ConfidenceLevel Confidence(FacilityWorkspaceMetrics metrics)
    {
        if (metrics.FormCompliance.TargetedForms == 0)
        {
            return ConfidenceLevel.Medium;
        }

        return ConfidenceReasons(metrics).Count == 0 ? ConfidenceLevel.High : ConfidenceLevel.Medium;
    }

    public static IReadOnlyList<string> ConfidenceReasons(FacilityWorkspaceMetrics metrics)
    {
        var reasons = new List<string>();
        if (metrics.FormCompliance.TargetedForms == 0)
        {
            reasons.Add("لا توجد نماذج مستهدفة ضمن الفترة، لذلك لا يؤثر الالتزام بالنماذج في التقييم.");
        }

        if (metrics.Alerts.OpenEscalations == 0 && metrics.Alerts.PersonalUnreadNotifications > 0)
        {
            reasons.Add("التنبيهات الشخصية لا تكفي وحدها لتمثيل تنبيهات تشغيلية على مستوى السجن.");
        }

        return reasons;
    }
}

internal sealed record FacilityStatusResult(string Code, string LabelAr);

