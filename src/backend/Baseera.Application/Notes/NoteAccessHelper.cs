namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;

internal static class NoteAccessHelper
{
    public static void EnsurePermission(ICurrentUser currentUser, string permissionCode)
    {
        if (!currentUser.HasPermission(permissionCode))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    public static bool CanViewSensitive(ICurrentUser currentUser) =>
        currentUser.HasPermission(PermissionCodes.NotesViewSensitive);

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

    public static OperationalNote LoadInScopeOrNotFound(
        IBaseeraDbContext db,
        INoteScopeService noteScope,
        Guid id,
        bool includeDeleted = false)
    {
        var query = includeDeleted ? db.OperationalNotesIncludingDeleted : db.OperationalNotes;
        var note = query.FirstOrDefault(n => n.Id == id);
        if (note is null || !noteScope.CanAccess(note))
        {
            throw new KeyNotFoundException("الملاحظة غير موجودة.");
        }

        return note;
    }

    public static string RedactedTitle => "[محجوب]";
    public static string RedactedDescription => "[محتوى حساس — يتطلب صلاحية عرض]";
}
