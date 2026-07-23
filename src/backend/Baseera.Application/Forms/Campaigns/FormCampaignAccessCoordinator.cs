namespace Baseera.Application.Forms.Campaigns;

using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Microsoft.EntityFrameworkCore;

public interface IFormCampaignAccessCoordinator
{
    Guid? UserId { get; }
    string? DisplayName { get; }

    void EnsureAnyViewPermission();
    void EnsurePreviewPermission();
    void EnsurePermission(string permissionCode);
    bool HasPermission(string permissionCode);
    bool CanViewSensitiveViaRole { get; }
    void EnsureCanViewSensitiveForm(FormDefinition form);

    Task<IQueryable<FormDefinition>> FilterScopedFormsAsync(CancellationToken cancellationToken = default);
    Task<FormDefinition> LoadInScopeFormAsync(Guid formDefinitionId, CancellationToken cancellationToken = default);

    Task<bool> CanViewCampaignAsync(FormCampaign campaign, CancellationToken cancellationToken = default);
    Task EnsureViewCapabilityAsync(FormDefinition form, CancellationToken cancellationToken = default);
    Task EnsurePublishCapabilityAsync(FormDefinition form, CancellationToken cancellationToken = default);
    Task<bool> HasPublishCapabilityAsync(FormDefinition form, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, bool>> ResolveViewCapabilitiesAsync(
        IReadOnlyList<Guid> formDefinitionIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, bool>> ResolvePublishCapabilitiesAsync(
        IReadOnlyList<FormDefinition> forms,
        CancellationToken cancellationToken = default);

    IQueryable<Region> FilterRegions(IQueryable<Region> query);
    IQueryable<Facility> FilterFacilities(IQueryable<Facility> query);
}

public sealed class FormCampaignAccessCoordinator(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IFormEffectiveAccessService effectiveAccess,
    IOrganizationalScopeService orgScope,
    TimeProvider timeProvider) : IFormCampaignAccessCoordinator
{
    public Guid? UserId => currentUser.UserId;
    public string? DisplayName => currentUser.DisplayName;

    public bool CanViewSensitiveViaRole =>
        FormAccessHelper.CanViewSensitive(currentUser)
        || currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
        || currentUser.HasPermission(PermissionCodes.AuditView);

    public void EnsureAnyViewPermission()
    {
        if (!(currentUser.HasPermission(PermissionCodes.FormsView)
            || currentUser.HasPermission(PermissionCodes.FormsPublish)
            || currentUser.HasPermission(PermissionCodes.FormsManageCampaigns)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorRegion)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
            || currentUser.HasPermission(PermissionCodes.AuditView)))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض الحملات.");
        }
    }

    public void EnsurePreviewPermission()
    {
        if (!(currentUser.HasPermission(PermissionCodes.FormsPreviewTargets)
            || currentUser.HasPermission(PermissionCodes.FormsPublish)
            || currentUser.HasPermission(PermissionCodes.FormsManageCampaigns)))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية معاينة الاستهداف.");
        }
    }

    public void EnsurePermission(string permissionCode) =>
        FormAccessHelper.EnsurePermission(currentUser, permissionCode);

    public bool HasPermission(string permissionCode) =>
        currentUser.HasPermission(permissionCode);

    public void EnsureCanViewSensitiveForm(FormDefinition form)
    {
        if (FormAccessHelper.RequiresSensitive(form.Classification) && !FormAccessHelper.CanViewSensitive(currentUser))
        {
            throw new KeyNotFoundException("النموذج غير موجود.");
        }
    }

    public Task<IQueryable<FormDefinition>> FilterScopedFormsAsync(CancellationToken cancellationToken = default) =>
        formScope.FilterQueryableAsync(db.FormDefinitions.AsNoTracking(), cancellationToken);

    public Task<FormDefinition> LoadInScopeFormAsync(Guid formDefinitionId, CancellationToken cancellationToken = default) =>
        FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formDefinitionId, cancellationToken: cancellationToken);

    public async Task<bool> CanViewCampaignAsync(FormCampaign campaign, CancellationToken cancellationToken = default)
    {
        if (!formScope.CanAccess(campaign.FormDefinition))
        {
            return false;
        }

        if (!await effectiveAccess.HasCapabilityAsync(campaign.FormDefinition, FormAccessCapability.View, cancellationToken))
        {
            return false;
        }

        if (FormAccessHelper.RequiresSensitive(campaign.FormDefinition.Classification)
            && !FormAccessHelper.CanViewSensitive(currentUser)
            && !currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
            && !currentUser.HasPermission(PermissionCodes.AuditView))
        {
            return false;
        }

        return currentUser.HasPermission(PermissionCodes.FormsView)
            || currentUser.HasPermission(PermissionCodes.FormsPublish)
            || currentUser.HasPermission(PermissionCodes.FormsManageCampaigns)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorRegion)
            || currentUser.HasPermission(PermissionCodes.FormsMonitorHeadquarters)
            || currentUser.HasPermission(PermissionCodes.AuditView);
    }

    public Task EnsureViewCapabilityAsync(FormDefinition form, CancellationToken cancellationToken = default) =>
        effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.View, cancellationToken);

    public Task EnsurePublishCapabilityAsync(FormDefinition form, CancellationToken cancellationToken = default) =>
        effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Publish, cancellationToken);

    public Task<bool> HasPublishCapabilityAsync(FormDefinition form, CancellationToken cancellationToken = default) =>
        effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Publish, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, bool>> ResolveViewCapabilitiesAsync(
        IReadOnlyList<Guid> formDefinitionIds,
        CancellationToken cancellationToken = default)
    {
        var allowedFormIds = new Dictionary<Guid, bool>(formDefinitionIds.Count);
        if (formDefinitionIds.Count == 0 || currentUser.UserId is not { } listUserId)
        {
            return allowedFormIds;
        }

        var roleIds = await db.UserRoles.AsNoTracking()
            .Where(r => r.UserId == listUserId)
            .Select(r => r.RoleId)
            .ToListAsync(cancellationToken);
        var grantsByFormId = await db.FormAccessGrants.AsNoTracking()
            .Where(g => formDefinitionIds.Contains(g.FormDefinitionId))
            .GroupBy(g => g.FormDefinitionId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);
        var forms = await db.FormDefinitions.AsNoTracking()
            .Where(f => formDefinitionIds.Contains(f.Id))
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var form in forms)
        {
            grantsByFormId.TryGetValue(form.Id, out var grants);
            var decision = FormGrantResolver.ResolveEffectiveGrant(
                grants ?? [],
                FormAccessCapability.View,
                listUserId,
                roleIds,
                form,
                now);
            allowedFormIds[form.Id] = decision is not false;
        }

        return allowedFormIds;
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> ResolvePublishCapabilitiesAsync(
        IReadOnlyList<FormDefinition> forms,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsPublish) || forms.Count == 0)
        {
            return new Dictionary<Guid, bool>();
        }

        var map = new Dictionary<Guid, bool>(forms.Count);
        foreach (var form in forms)
        {
            map[form.Id] = await effectiveAccess.HasCapabilityAsync(
                form,
                FormAccessCapability.Publish,
                cancellationToken);
        }

        return map;
    }

    public IQueryable<Region> FilterRegions(IQueryable<Region> query) =>
        orgScope.FilterRegions(query);

    public IQueryable<Facility> FilterFacilities(IQueryable<Facility> query) =>
        orgScope.FilterFacilities(query);
}
