namespace Baseera.Application.Forms.Responses;

using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;

public interface IFormResponseAccessCoordinator
{
    Guid UserId { get; }
    void EnsureRespondentWorkspacePermission();
    void EnsureReviewDetailPermission();
    void EnsureRespondPermission();
    void EnsureReviewPermission();
    void EnsureApprovePermission();
    void EnsureClosePermission();
    bool CanViewSensitiveResponses();
    bool HasReviewerSidePermission();
    IQueryable<Facility> FilterFacilities(IQueryable<Facility> facilities);
    Task EnsureFacilityInScopeAsync(Guid facilityId, CancellationToken cancellationToken);
    Task EnsureFormCapabilityAsync(Guid formDefinitionId, FormAccessCapability capability, CancellationToken cancellationToken);
    bool IsRespondentOwner(FormResponse response);
    bool CanActAsFacilityRespondent(Guid facilityId);
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

    public void EnsureRespondentWorkspacePermission() => EnsureRespondPermission();

    public void EnsureReviewDetailPermission()
    {
        if (!HasReviewerSidePermission())
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض تفاصيل المراجعة.");
        }
    }

    public bool HasReviewerSidePermission() =>
        currentUser.HasPermission(PermissionCodes.FormsViewResponses)
        || currentUser.HasPermission(PermissionCodes.FormsReviewResponses)
        || currentUser.HasPermission(PermissionCodes.FormsApproveResponses)
        || currentUser.HasPermission(PermissionCodes.FormsCloseResponses);

    public void EnsureReviewPermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsReviewResponses);

    public void EnsureApprovePermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsApproveResponses);

    public void EnsureClosePermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsCloseResponses);

    public bool CanViewSensitiveResponses() =>
        currentUser.HasPermission(PermissionCodes.FormsViewSensitiveResponses)
        || currentUser.HasPermission(PermissionCodes.FormsViewSensitive);

    public IQueryable<Facility> FilterFacilities(IQueryable<Facility> facilities) =>
        orgScope.FilterFacilities(facilities);

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

    public bool CanActAsFacilityRespondent(Guid facilityId) =>
        currentUser.HasPermission(PermissionCodes.FormsRespond)
        && orgScope.CanAccessFacility(facilityId);

    public bool IsRespondentOwner(FormResponse response)
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsRespond)
            || !orgScope.CanAccessFacility(response.FacilityId))
        {
            return false;
        }

        var userId = currentUser.UserId;
        if (userId is null)
        {
            return false;
        }

        if (response.SubmittedByUserId == userId)
        {
            return true;
        }

        if (response.Status is FormResponseStatus.Draft or FormResponseStatus.Returned
            && response.LastSavedByUserId == userId)
        {
            return true;
        }

        return false;
    }
}
