namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;

/// <summary>
/// Resolves whether the current caller may see or act on a given <see cref="FormDefinition"/>,
/// combining organizational scope (<see cref="IFormScopeService"/>) with fine-grained capability
/// grants (<see cref="IFormEffectiveAccessService"/>) — the two checks <see cref="FormVersionService"/>
/// always performs together. Extracted so the service depends on one coherent access collaborator
/// instead of two separate service parameters.
/// </summary>
public interface IFormVersionAccessGuard
{
    Task<FormDefinition> LoadInScopeAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<FormDefinition> LoadViewableAsync(Guid formId, CancellationToken cancellationToken = default);
    Task EnsureCapabilityAsync(FormDefinition form, FormAccessCapability capability, CancellationToken cancellationToken = default);
    Task<bool> HasCapabilityAsync(FormDefinition form, FormAccessCapability capability, CancellationToken cancellationToken = default);
}

public sealed class FormVersionAccessGuard(
    IBaseeraDbContext db,
    IFormScopeService formScope,
    IFormEffectiveAccessService effectiveAccess) : IFormVersionAccessGuard
{
    public Task<FormDefinition> LoadInScopeAsync(Guid formId, CancellationToken cancellationToken = default) =>
        FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);

    public async Task<FormDefinition> LoadViewableAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await LoadInScopeAsync(formId, cancellationToken);
        if (!await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.View, cancellationToken))
        {
            throw new KeyNotFoundException("النموذج غير موجود.");
        }

        return form;
    }

    public Task EnsureCapabilityAsync(FormDefinition form, FormAccessCapability capability, CancellationToken cancellationToken = default) =>
        effectiveAccess.EnsureCapabilityAsync(form, capability, cancellationToken);

    public Task<bool> HasCapabilityAsync(FormDefinition form, FormAccessCapability capability, CancellationToken cancellationToken = default) =>
        effectiveAccess.HasCapabilityAsync(form, capability, cancellationToken);
}
