namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

internal static class AssignmentTargetValidator
{
    public static async Task EnsureUserCanReceiveAsync(
        IBaseeraDbContext db,
        Guid userId,
        OperationalNote note,
        IReadOnlyCollection<string> workPermissionCodes,
        string workPermissionMessage,
        string scopeMessage,
        CancellationToken cancellationToken)
    {
        var user = await db.UsersIncludingDeleted.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            throw new KeyNotFoundException("المستخدم غير موجود.");
        }

        if (!user.IsActive || user.ProvisioningStatus != UserProvisioningStatus.Active)
        {
            throw new InvalidOperationException("المستخدم غير نشط أو غير مُهيّأ لاستلام التكليف.");
        }

        await EnsureHasWorkPermissionAsync(db, userId, workPermissionCodes, workPermissionMessage, cancellationToken);
        await EnsureScopeIntersectsAsync(db, userId, note, scopeMessage, cancellationToken);
    }

    public static async Task EnsureDepartmentExistsAsync(IBaseeraDbContext db, Guid departmentId, CancellationToken cancellationToken)
    {
        if (!await db.Departments.AnyAsync(d => d.Id == departmentId && !d.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("الإدارة غير موجودة.");
        }
    }

    private static async Task EnsureHasWorkPermissionAsync(
        IBaseeraDbContext db,
        Guid userId,
        IReadOnlyCollection<string> permissionCodes,
        string message,
        CancellationToken cancellationToken)
    {
        var hasWorkPermission = await (
            from userRole in db.UserRoles
            join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
            join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == userId && permissionCodes.Contains(permission.Code)
            select permission.Code).AnyAsync(cancellationToken);

        if (!hasWorkPermission)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static async Task EnsureScopeIntersectsAsync(
        IBaseeraDbContext db,
        Guid userId,
        OperationalNote note,
        string message,
        CancellationToken cancellationToken)
    {
        var snapshots = await db.UserScopes
            .Where(scope => scope.UserId == userId && scope.IsActive && !scope.IsDeleted)
            .Select(scope => new UserScopeSnapshot(scope.ScopeType, scope.RegionId, scope.FacilityId, scope.FacilityUnitId))
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException(message);
        }

        if (snapshots.Any(scope => scope.ScopeType == ScopeType.Global))
        {
            return;
        }

        if (await NoteAssigneeScopeIntersection.IntersectsAsync(db, snapshots, note, cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
