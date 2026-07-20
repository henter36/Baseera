namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

internal static class CorrectiveActionAccessHelper
{
    public const string ModuleName = "CorrectiveActions";
    public const string RedactedTitle = "[محجوب]";
    public const string RedactedDescription = "[محتوى حساس — يتطلب صلاحية عرض]";

    public static void EnsurePermission(ICurrentUser user, string permission)
    {
        if (!user.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("لا تملك الصلاحية المطلوبة.");
        }
    }

    public static bool CanViewSensitive(ICurrentUser user) =>
        user.HasPermission(PermissionCodes.CorrectiveActionsViewSensitive);

    public static bool RequiresSensitive(ClassificationLevel classification) =>
        classification >= ClassificationLevel.Confidential;

    public static void EnsureRowVersion(byte[] actual, string expected)
    {
        byte[] parsed;
        try
        {
            parsed = Convert.FromBase64String(expected);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("إصدار السجل غير صالح.", ex);
        }

        if (!actual.SequenceEqual(parsed))
        {
            throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.");
        }
    }

    public static async Task<CorrectiveAction> LoadInScopeOrNotFoundAsync(
        IBaseeraDbContext db,
        ICorrectiveActionScopeService scope,
        Guid id,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = includeDeleted ? db.CorrectiveActionsIncludingDeleted : db.CorrectiveActions;
        var item = await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (item is null || !await scope.CanAccessAsync(item, cancellationToken))
        {
            throw new KeyNotFoundException("الإجراء التصحيحي غير موجود.");
        }

        return item;
    }
}
