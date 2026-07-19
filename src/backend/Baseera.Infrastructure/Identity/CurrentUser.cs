namespace Baseera.Infrastructure.Identity;

using System.Security.Claims;
using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private UserPrincipalState? _state;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated => State.IsAuthenticated;
    public Guid? UserId => State.UserId;
    public string? ExternalSubject => State.ExternalSubject;
    public string? DisplayName => State.DisplayName;
    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items.TryGetValue("CorrelationId", out var value) == true
            ? value?.ToString()
            : _httpContextAccessor.HttpContext?.TraceIdentifier;
    public IReadOnlyCollection<string> Permissions => State.Permissions;
    public IReadOnlyCollection<UserScopeSnapshot> Scopes => State.Scopes;
    public bool IsGlobalScope => Scopes.Any(s => s.ScopeType is ScopeType.Global or ScopeType.Headquarters);

    public bool HasPermission(string permissionCode) =>
        Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);

    public void SetState(UserPrincipalState state) => _state = state;

    private UserPrincipalState State =>
        _state ?? UserPrincipalState.Anonymous;
}

public sealed record UserPrincipalState(
    bool IsAuthenticated,
    Guid? UserId,
    string? ExternalSubject,
    string? DisplayName,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<UserScopeSnapshot> Scopes)
{
    public static UserPrincipalState Anonymous { get; } = new(
        false, null, null, null, Array.Empty<string>(), Array.Empty<UserScopeSnapshot>());
}

public sealed class UserProvisioningService(BaseeraDbContext db)
{
    public async Task<UserPrincipalState> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return UserPrincipalState.Anonymous;
        }

        var subject = principal.FindFirstValue("oid")
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return UserPrincipalState.Anonymous;
        }

        var displayName = principal.FindFirstValue("name")
                          ?? principal.FindFirstValue(ClaimTypes.Name)
                          ?? subject;
        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("preferred_username");

        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalSubject == subject, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                ExternalSubject = subject,
                UserName = email ?? subject,
                DisplayNameAr = displayName,
                Email = email,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            user.LastLoginAtUtc = DateTimeOffset.UtcNow;
            user.DisplayNameAr = displayName;
            if (!string.IsNullOrWhiteSpace(email))
            {
                user.Email = email;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var permissions = await (
            from ur in db.UserRoles
            join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == user.Id
            select p.Code).Distinct().ToListAsync(cancellationToken);

        var scopes = await db.UserScopes
            .Where(s => s.UserId == user.Id && s.IsActive && !s.IsDeleted)
            .Select(s => new UserScopeSnapshot(s.ScopeType, s.RegionId, s.FacilityId, s.FacilityUnitId))
            .ToListAsync(cancellationToken);

        return new UserPrincipalState(true, user.Id, subject, displayName, permissions, scopes);
    }
}
