namespace Baseera.Application.Identity;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using FluentValidation;

public sealed record UserDto(Guid Id, string ExternalSubject, string UserName, string DisplayNameAr, string? Email, bool IsActive, IReadOnlyList<string> Roles);
public sealed record RoleDto(Guid Id, string Code, string NameAr);
public sealed record UserScopeDto(Guid Id, ScopeType ScopeType, Guid? RegionId, Guid? FacilityId, Guid? FacilityUnitId, bool IsActive);
public sealed record AssignScopeRequest(ScopeType ScopeType, Guid? RegionId, Guid? FacilityId, Guid? FacilityUnitId);
public sealed record AssignRoleRequest(string RoleCode);
public sealed record MeDto(Guid Id, string DisplayNameAr, string? Email, IReadOnlyList<string> Permissions, IReadOnlyList<UserScopeDto> Scopes);

public sealed class AssignScopeRequestValidator : AbstractValidator<AssignScopeRequest>
{
    public AssignScopeRequestValidator()
    {
        RuleFor(x => x.ScopeType).IsInEnum();
        RuleFor(x => x)
            .Must(x => x.ScopeType is not (ScopeType.Region or ScopeType.MultipleRegions) || x.RegionId.HasValue)
            .WithMessage("معرف المنطقة مطلوب لهذا النوع من النطاق.");
        RuleFor(x => x)
            .Must(x => x.ScopeType is not (ScopeType.Facility or ScopeType.MultipleFacilities) || x.FacilityId.HasValue)
            .WithMessage("معرف السجن مطلوب لهذا النوع من النطاق.");
        RuleFor(x => x)
            .Must(x => x.ScopeType != ScopeType.FacilityUnit || (x.FacilityId.HasValue && x.FacilityUnitId.HasValue))
            .WithMessage("معرف السجن والوحدة مطلوبان لنطاق الوحدة.");
    }
}

public interface IUserAdminService
{
    Task<MeDto> GetMeAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<UserDto>> ListUsersAsync(PagedQuery query, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task AssignRoleAsync(Guid userId, AssignRoleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserScopeDto>> ListScopesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserScopeDto> AssignScopeAsync(Guid userId, AssignScopeRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default);
}

public sealed class UserAdminService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IUserAdminService
{
    public Task<MeDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        }

        var user = db.Users.First(u => u.Id == currentUser.UserId.Value);
        var scopes = db.UserScopes.Where(s => s.UserId == user.Id && s.IsActive && !s.IsDeleted)
            .Select(s => new UserScopeDto(s.Id, s.ScopeType, s.RegionId, s.FacilityId, s.FacilityUnitId, s.IsActive))
            .ToList();

        return Task.FromResult(new MeDto(
            user.Id,
            user.DisplayNameAr,
            user.Email,
            currentUser.Permissions.ToList(),
            scopes));
    }

    public Task<PagedResult<UserDto>> ListUsersAsync(PagedQuery query, CancellationToken cancellationToken = default)
    {
        Ensure("Users.View");
        var q = db.Users.Where(u => !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(u => u.DisplayNameAr.Contains(term) || u.UserName.Contains(term));
        }

        var total = q.Count();
        var page = q.OrderBy(u => u.DisplayNameAr).Skip(query.Skip).Take(query.Take).ToList();
        var userIds = page.Select(u => u.Id).ToList();
        var roles = db.UserRoles.Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Code })
            .ToList()
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Code).ToList());

        var items = page.Select(u => new UserDto(
            u.Id,
            u.ExternalSubject,
            u.UserName,
            u.DisplayNameAr,
            u.Email,
            u.IsActive,
            roles.GetValueOrDefault(u.Id, Array.Empty<string>()))).ToList();

        return Task.FromResult(new PagedResult<UserDto> { Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total });
    }

    public Task<UserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure("Users.View");
        var u = db.Users.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (u is null)
        {
            return Task.FromResult<UserDto?>(null);
        }

        var roleCodes = db.UserRoles.Where(ur => ur.UserId == id)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Code)
            .ToList();

        return Task.FromResult<UserDto?>(new UserDto(u.Id, u.ExternalSubject, u.UserName, u.DisplayNameAr, u.Email, u.IsActive, roleCodes));
    }

    public async Task AssignRoleAsync(Guid userId, AssignRoleRequest request, CancellationToken cancellationToken = default)
    {
        Ensure("Roles.Manage");
        var user = db.Users.FirstOrDefault(u => u.Id == userId && !u.IsDeleted)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");
        var role = db.Roles.FirstOrDefault(r => r.Code == request.RoleCode && !r.IsDeleted)
            ?? throw new KeyNotFoundException("الدور غير موجود.");

        if (db.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == role.Id))
        {
            return;
        }

        db.Add(new UserRole
        {
            UserId = userId,
            RoleId = role.Id,
            AssignedBy = currentUser.ExternalSubject
        });
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "AssignRole",
            Module = "Identity",
            EntityType = nameof(User),
            EntityId = userId.ToString(),
            NewValues = new { role.Code },
            Reason = "تعيين دور"
        }, cancellationToken);
    }

    public Task<IReadOnlyList<UserScopeDto>> ListScopesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Ensure("Scopes.Manage");
        var items = db.UserScopes.Where(s => s.UserId == userId && !s.IsDeleted)
            .Select(s => new UserScopeDto(s.Id, s.ScopeType, s.RegionId, s.FacilityId, s.FacilityUnitId, s.IsActive))
            .ToList();
        return Task.FromResult<IReadOnlyList<UserScopeDto>>(items);
    }

    public async Task<UserScopeDto> AssignScopeAsync(Guid userId, AssignScopeRequest request, CancellationToken cancellationToken = default)
    {
        Ensure("Scopes.Manage");
        if (!db.Users.Any(u => u.Id == userId && !u.IsDeleted))
        {
            throw new KeyNotFoundException("المستخدم غير موجود.");
        }

        var scope = new UserScope
        {
            UserId = userId,
            ScopeType = request.ScopeType,
            RegionId = request.RegionId,
            FacilityId = request.FacilityId,
            FacilityUnitId = request.FacilityUnitId,
            CreatedBy = currentUser.ExternalSubject
        };
        db.Add(scope);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "AssignScope",
            Module = "Identity",
            EntityType = nameof(UserScope),
            EntityId = scope.Id.ToString(),
            NewValues = request,
            Reason = "تعيين نطاق تنظيمي"
        }, cancellationToken);

        return new UserScopeDto(scope.Id, scope.ScopeType, scope.RegionId, scope.FacilityId, scope.FacilityUnitId, scope.IsActive);
    }

    public Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        Ensure("Users.View");
        var items = db.Roles.Where(r => !r.IsDeleted)
            .OrderBy(r => r.Code)
            .Select(r => new RoleDto(r.Id, r.Code, r.NameAr))
            .ToList();
        return Task.FromResult<IReadOnlyList<RoleDto>>(items);
    }

    private void Ensure(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }
}
