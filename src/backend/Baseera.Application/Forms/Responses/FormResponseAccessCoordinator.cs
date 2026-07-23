namespace Baseera.Application.Forms.Responses;

using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormResponseAccessCoordinator
{
    Guid UserId { get; }
    void EnsureRespondPermission();
    void EnsureViewResponsesPermission();
    void EnsureReviewPermission();
    void EnsureApprovePermission();
    void EnsureClosePermission();
    bool CanViewSensitiveResponses();
    Task EnsureFacilityInScopeAsync(Guid facilityId, CancellationToken cancellationToken);
    Task EnsureFormCapabilityAsync(Guid formDefinitionId, FormAccessCapability capability, CancellationToken cancellationToken);
}

public sealed class FormResponseAccessCoordinator(
    ICurrentUser currentUser,
    IOrganizationalScopeService orgScope,
    IFormEffectiveAccessService formAccess,
    IBaseeraDbContext db) : IFormResponseAccessCoordinator
{
    public Guid UserId => currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير معروف.");

    public void EnsureRespondPermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsRespond);

    public void EnsureViewResponsesPermission()
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsViewResponses)
            && !currentUser.HasPermission(PermissionCodes.FormsRespond)
            && !currentUser.HasPermission(PermissionCodes.FormsReviewResponses)
            && !currentUser.HasPermission(PermissionCodes.FormsApproveResponses))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض الردود.");
        }
    }

    public void EnsureReviewPermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsReviewResponses);

    public void EnsureApprovePermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsApproveResponses);

    public void EnsureClosePermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsCloseResponses);

    public bool CanViewSensitiveResponses() =>
        currentUser.HasPermission(PermissionCodes.FormsViewSensitiveResponses)
        || currentUser.HasPermission(PermissionCodes.FormsViewSensitive);

    public Task EnsureFacilityInScopeAsync(Guid facilityId, CancellationToken cancellationToken)
    {
        if (!orgScope.CanAccessFacility(facilityId))
        {
            throw new KeyNotFoundException("الاستحقاق غير موجود.");
        }

        return Task.CompletedTask;
    }

    public async Task EnsureFormCapabilityAsync(
        Guid formDefinitionId,
        FormAccessCapability capability,
        CancellationToken cancellationToken)
    {
        var form = await db.FormDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == formDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException("النموذج غير موجود.");
        await formAccess.EnsureCapabilityAsync(form, capability, cancellationToken);
    }
}
