namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Domain.CorrectiveActions;

internal static class CorrectiveActionServiceSupport
{
    public static Guid RequireUserId(ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");

    public static void AppendHistory(
        IBaseeraDbContext db,
        Guid actionId,
        CorrectiveActionStatus? from,
        CorrectiveActionStatus to,
        Guid userId,
        string? reason)
    {
        db.Add(new CorrectiveActionStatusHistory
        {
            CorrectiveActionId = actionId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            ChangedAtUtc = DateTimeOffset.UtcNow,
            Reason = reason
        });
    }

    public static Task WriteAuditAsync(
        IAuditService audit,
        string actionName,
        CorrectiveAction action,
        object? oldValues,
        object? newValues,
        string? reason,
        CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry
        {
            Action = actionName,
            Module = CorrectiveActionAccessHelper.ModuleName,
            EntityType = nameof(CorrectiveAction),
            EntityId = action.Id.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            Reason = reason
        }, cancellationToken);
}
