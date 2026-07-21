namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

internal static class FormAccessHelper
{
    public const string ModuleName = "Forms";

    public static void EnsurePermission(ICurrentUser currentUser, string permissionCode)
    {
        if (!currentUser.HasPermission(permissionCode))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    public static bool CanViewSensitive(ICurrentUser currentUser) =>
        currentUser.HasPermission(PermissionCodes.FormsViewSensitive);

    public static bool RequiresSensitive(ClassificationLevel classification) =>
        classification >= ClassificationLevel.Confidential;

    public static void EnsureRowVersion(byte[] current, string incomingBase64)
    {
        byte[] incoming;
        try
        {
            incoming = Convert.FromBase64String(incomingBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("إصدار السجل غير صالح.");
        }

        if (!current.SequenceEqual(incoming))
        {
            throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.");
        }
    }

    public static async Task<FormDefinition> LoadInScopeOrNotFoundAsync(
        IBaseeraDbContext db,
        IFormScopeService formScope,
        Guid id,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = includeDeleted ? db.FormDefinitionsIncludingDeleted : db.FormDefinitions;
        var form = await query.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (form is null || !formScope.CanAccess(form))
        {
            throw new KeyNotFoundException("النموذج غير موجود.");
        }

        return form;
    }

    public static string RedactedTitle => "[محجوب]";
    public static string RedactedDescription => "[محتوى حساس — يتطلب صلاحية عرض]";
}
