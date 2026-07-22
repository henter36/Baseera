namespace Baseera.Application.Forms;

using Baseera.Domain.Forms;

public static class FormVersionStateMachine
{
    private static readonly Dictionary<FormVersionStatus, HashSet<FormVersionStatus>> Allowed = new()
    {
        [FormVersionStatus.Draft] = [FormVersionStatus.InReview],
        [FormVersionStatus.InReview] =
        [
            FormVersionStatus.ChangesRequested,
            FormVersionStatus.Rejected,
            FormVersionStatus.Locked
        ],
        [FormVersionStatus.ChangesRequested] = [FormVersionStatus.InReview],
        [FormVersionStatus.Rejected] = [FormVersionStatus.Draft],
        [FormVersionStatus.Locked] = []
    };

    public static bool CanTransition(FormVersionStatus from, FormVersionStatus to) =>
        Allowed.TryGetValue(from, out var set) && set.Contains(to);

    public static void EnsureAllowed(FormVersionStatus from, FormVersionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException("انتقال حالة إصدار النموذج غير مسموح.");
        }
    }

    public static bool IsEditable(FormVersionStatus status) =>
        status is FormVersionStatus.Draft or FormVersionStatus.ChangesRequested;

    public static void EnsureEditable(FormVersionStatus status)
    {
        if (!IsEditable(status))
        {
            throw new InvalidOperationException("لا يمكن تعديل إصدار مقفل أو قيد المراجعة.");
        }
    }
}
