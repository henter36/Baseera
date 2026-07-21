namespace Baseera.Application.Forms;

using Baseera.Domain.Forms;

public static class FormDefinitionStateMachine
{
    public static bool CanTransition(FormDefinitionStatus from, FormDefinitionStatus to) =>
        (from, to) switch
        {
            (FormDefinitionStatus.Draft, FormDefinitionStatus.InReview) => true,
            (FormDefinitionStatus.InReview, FormDefinitionStatus.Approved) => true,
            (FormDefinitionStatus.InReview, FormDefinitionStatus.ChangesRequested) => true,
            (FormDefinitionStatus.InReview, FormDefinitionStatus.Rejected) => true,
            (FormDefinitionStatus.ChangesRequested, FormDefinitionStatus.InReview) => true,
            (FormDefinitionStatus.Approved, FormDefinitionStatus.Archived) => true,
            (FormDefinitionStatus.Rejected, FormDefinitionStatus.Draft) => true,
            (FormDefinitionStatus.Rejected, FormDefinitionStatus.Archived) => true,
            (FormDefinitionStatus.Archived, FormDefinitionStatus.Approved) => true,
            _ => false
        };

    public static void EnsureAllowed(FormDefinitionStatus from, FormDefinitionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"انتقال حالة النموذج من {FormDisplay.StatusAr(from)} إلى {FormDisplay.StatusAr(to)} غير مسموح.");
        }
    }

    public static bool IsEditable(FormDefinitionStatus status) =>
        status is FormDefinitionStatus.Draft or FormDefinitionStatus.ChangesRequested;

    public static bool IsTerminalLocked(FormDefinitionStatus status) =>
        status is FormDefinitionStatus.Approved or FormDefinitionStatus.Archived;
}

public static class FormDisplay
{
    public static string StatusAr(FormDefinitionStatus status) => status switch
    {
        FormDefinitionStatus.Draft => "مسودة",
        FormDefinitionStatus.InReview => "قيد المراجعة",
        FormDefinitionStatus.ChangesRequested => "تعديلات مطلوبة",
        FormDefinitionStatus.Approved => "معتمد",
        FormDefinitionStatus.Rejected => "مرفوض",
        FormDefinitionStatus.Archived => "مؤرشف",
        _ => status.ToString()
    };

    public static string CapabilityAr(FormAccessCapability capability) => capability switch
    {
        FormAccessCapability.View => "عرض",
        FormAccessCapability.Design => "تصميم",
        FormAccessCapability.Review => "مراجعة",
        FormAccessCapability.Approve => "اعتماد",
        FormAccessCapability.Archive => "أرشفة",
        FormAccessCapability.Restore => "استعادة",
        FormAccessCapability.ViewSensitive => "عرض حساس",
        FormAccessCapability.ManageAccess => "إدارة الوصول",
        FormAccessCapability.ManageRetention => "إدارة الاحتفاظ",
        _ => capability.ToString()
    };
}
