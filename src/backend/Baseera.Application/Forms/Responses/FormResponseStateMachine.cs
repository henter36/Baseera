namespace Baseera.Application.Forms.Responses;

using Baseera.Domain.Forms;

public static class FormResponseStateMachine
{
    public static bool CanTransition(FormResponseStatus? from, FormResponseStatus to) =>
        (from, to) switch
        {
            (null, FormResponseStatus.Draft) => true,
            (FormResponseStatus.Draft, FormResponseStatus.Submitted) => true,
            (FormResponseStatus.Returned, FormResponseStatus.Draft) => true,
            (FormResponseStatus.Returned, FormResponseStatus.Submitted) => true,
            (FormResponseStatus.Submitted, FormResponseStatus.UnderReview) => true,
            (FormResponseStatus.Submitted, FormResponseStatus.Approved) => true,
            (FormResponseStatus.Submitted, FormResponseStatus.Closed) => true,
            (FormResponseStatus.UnderReview, FormResponseStatus.Returned) => true,
            (FormResponseStatus.UnderReview, FormResponseStatus.Approved) => true,
            (FormResponseStatus.UnderReview, FormResponseStatus.Rejected) => true,
            (FormResponseStatus.UnderReview, FormResponseStatus.UnderReview) => true,
            (FormResponseStatus.Approved, FormResponseStatus.Closed) => true,
            _ => false
        };

    public static void EnsureCanTransition(FormResponseStatus? from, FormResponseStatus to)
    {
        if (!CanTransition(from, to))
        {
            var fromLabel = from is null ? "بدون رد" : FormResponseDisplay.StatusAr(from.Value);
            throw new InvalidOperationException(
                $"انتقال حالة الرد من {fromLabel} إلى {FormResponseDisplay.StatusAr(to)} غير مسموح.");
        }
    }

    public static FormResponseStatus ResolveSubmissionTargetStatus(FormReviewMode reviewMode) =>
        reviewMode == FormReviewMode.None
            ? FormResponseStatus.Submitted
            : FormResponseStatus.UnderReview;

    public static FormResponseStatus ResolveApprovalTargetStatus(
        int currentLevel,
        int requiredLevels) =>
        currentLevel >= requiredLevels
            ? FormResponseStatus.Approved
            : FormResponseStatus.UnderReview;

    public static bool CanEditDraft(FormResponseStatus status) =>
        status is FormResponseStatus.Draft or FormResponseStatus.Returned;

    public static bool CanClose(FormResponseStatus status, FormReviewMode reviewMode) =>
        status == FormResponseStatus.Approved
        || (status == FormResponseStatus.Submitted && reviewMode == FormReviewMode.None);
}

public static class FormResponseDisplay
{
    public static string StatusAr(FormResponseStatus status) => status switch
    {
        FormResponseStatus.Draft => "مسودة",
        FormResponseStatus.Submitted => "مُرسل",
        FormResponseStatus.UnderReview => "قيد المراجعة",
        FormResponseStatus.Returned => "مُعاد",
        FormResponseStatus.Approved => "معتمد",
        FormResponseStatus.Rejected => "مرفوض",
        FormResponseStatus.Closed => "مغلق",
        _ => status.ToString()
    };

    public static string WorkStatusAr(FormAssignmentWorkStatus status) => status switch
    {
        FormAssignmentWorkStatus.NotStarted => "لم يبدأ",
        FormAssignmentWorkStatus.Draft => "مسودة",
        FormAssignmentWorkStatus.Submitted => "مُرسل",
        FormAssignmentWorkStatus.UnderReview => "قيد المراجعة",
        FormAssignmentWorkStatus.Returned => "مُعاد",
        FormAssignmentWorkStatus.Approved => "معتمد",
        FormAssignmentWorkStatus.Rejected => "مرفوض",
        FormAssignmentWorkStatus.Closed => "مغلق",
        FormAssignmentWorkStatus.Overdue => "متأخر",
        _ => status.ToString()
    };
}
