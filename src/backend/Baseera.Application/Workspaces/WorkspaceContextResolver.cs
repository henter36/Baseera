namespace Baseera.Application.Workspaces;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public sealed class WorkspaceContextResolver(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IOrganizationalScopeService scopeService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DefaultRange = TimeSpan.FromDays(30);

    public async Task<WorkspaceContext> ResolveAsync(WorkspaceDefinition definition, WorkspaceRequest request, CancellationToken cancellationToken)
    {
        var level = request.Level ?? ResolveDefaultLevel(definition);
        if (!definition.SupportedLevels.Contains(level))
        {
            throw new ArgumentException("مستوى مساحة العمل غير مدعوم.");
        }

        ValidateWorkspacePermission(definition);
        await ValidateScopeAsync(level, request.RegionId, request.FacilityId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var from = request.FromUtc ?? now.Subtract(DefaultRange);
        var to = request.ToUtc ?? now;
        if (from > to)
        {
            throw new ArgumentException("نطاق التاريخ غير صحيح.");
        }

        return new WorkspaceContext(
            definition.Key,
            level,
            await ResolveOrganizationIdAsync(cancellationToken),
            request.RegionId,
            request.FacilityId,
            request.EntityId,
            scopeService.SummarizeScopes(),
            from,
            to,
            NormalizeLocale(request.Locale),
            NormalizeTimeZone(request.TimeZone),
            currentUser.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase),
            false);
    }

    private static WorkspaceLevel ResolveDefaultLevel(WorkspaceDefinition definition)
    {
        return definition.SupportedLevels.Contains(WorkspaceLevel.Domain)
            ? WorkspaceLevel.Domain
            : definition.SupportedLevels.OrderBy(level => level).First();
    }

    private void ValidateWorkspacePermission(WorkspaceDefinition definition)
    {
        if (definition.RequiredPermissions.Count == 0)
        {
            return;
        }

        if (!definition.RequiredPermissions.Any(currentUser.HasPermission))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية مساحة العمل.");
        }
    }

    private async Task ValidateScopeAsync(WorkspaceLevel level, Guid? regionId, Guid? facilityId, CancellationToken cancellationToken)
    {
        if (level == WorkspaceLevel.Headquarters && !currentUser.HasPermission(PermissionCodes.WorkspacesViewHeadquarters))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية مساحة عمل المركز.");
        }

        if (level == WorkspaceLevel.Region)
        {
            if (!currentUser.HasPermission(PermissionCodes.WorkspacesViewRegion))
            {
                throw new UnauthorizedAccessException("ليست لديك صلاحية مساحة عمل المنطقة.");
            }

            if (regionId is null)
            {
                throw new ArgumentException("regionId مطلوب لمساحة عمل المنطقة.");
            }

            var exists = await db.Regions.AnyAsync(region => region.Id == regionId && !region.IsDeleted, cancellationToken);
            if (!exists || !scopeService.CanAccessRegion(regionId.Value))
            {
                throw new KeyNotFoundException("لم يتم العثور على المنطقة ضمن نطاقك.");
            }
        }

        if (level == WorkspaceLevel.Facility)
        {
            if (!currentUser.HasPermission(PermissionCodes.WorkspacesViewFacility))
            {
                throw new UnauthorizedAccessException("ليست لديك صلاحية مساحة عمل المنشأة.");
            }

            if (facilityId is null)
            {
                throw new ArgumentException("facilityId مطلوب لمساحة عمل المنشأة.");
            }

            var exists = await db.Facilities.AnyAsync(facility => facility.Id == facilityId && !facility.IsDeleted, cancellationToken);
            if (!exists || !scopeService.CanAccessFacility(facilityId.Value))
            {
                throw new KeyNotFoundException("لم يتم العثور على المنشأة ضمن نطاقك.");
            }
        }
    }

    private async Task<Guid?> ResolveOrganizationIdAsync(CancellationToken cancellationToken)
    {
        return await db.Organizations
            .Where(org => !org.IsDeleted)
            .OrderBy(org => org.CreatedAtUtc)
            .Select(org => (Guid?)org.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string NormalizeLocale(string? locale)
    {
        return string.IsNullOrWhiteSpace(locale) ? "ar-SA" : locale.Trim();
    }

    private static string NormalizeTimeZone(string? timeZone)
    {
        return string.IsNullOrWhiteSpace(timeZone) ? "Asia/Riyadh" : timeZone.Trim();
    }
}
