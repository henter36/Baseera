namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public interface IFormEffectiveAccessService
{
    Task EnsureCapabilityAsync(
        FormDefinition form,
        FormAccessCapability capability,
        CancellationToken cancellationToken = default);

    Task<bool> HasCapabilityAsync(
        FormDefinition form,
        FormAccessCapability capability,
        CancellationToken cancellationToken = default);
}

public sealed class FormEffectiveAccessService(
    IBaseeraDbContext db,
    ICurrentUser currentUser) : IFormEffectiveAccessService
{
    public async Task EnsureCapabilityAsync(
        FormDefinition form,
        FormAccessCapability capability,
        CancellationToken cancellationToken = default)
    {
        if (!await HasCapabilityAsync(form, capability, cancellationToken))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية على هذا النموذج.");
        }
    }

    public async Task<bool> HasCapabilityAsync(
        FormDefinition form,
        FormAccessCapability capability,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        if (!userId.HasValue)
        {
            return false;
        }

        var roleIds = await db.UserRoles
            .Where(r => r.UserId == userId.Value)
            .Select(r => r.RoleId)
            .ToListAsync(cancellationToken);

        var grants = await db.FormAccessGrants
            .Where(g => g.FormDefinitionId == form.Id)
            .ToListAsync(cancellationToken);

        var decision = FormGrantResolver.ResolveEffectiveGrant(
            grants,
            capability,
            userId.Value,
            roleIds,
            form,
            DateTimeOffset.UtcNow);

        return decision is not false;
    }
}
