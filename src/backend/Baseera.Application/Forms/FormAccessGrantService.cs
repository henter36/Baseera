namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormAccessGrantService
{
    Task<IReadOnlyList<FormAccessGrantDto>> ListGrantsAsync(Guid formDefinitionId, CancellationToken cancellationToken = default);
    Task<FormAccessGrantDto> CreateGrantAsync(Guid formDefinitionId, CreateFormAccessGrantRequest request, CancellationToken cancellationToken = default);
    Task RevokeGrantAsync(Guid formDefinitionId, Guid grantId, FormTransitionRequest request, CancellationToken cancellationToken = default);
}

public sealed class FormAccessGrantService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IOrganizationalScopeService orgScope,
    IFormSeparationOfDutiesService sod,
    IAuditService audit) : IFormAccessGrantService
{
    public async Task<IReadOnlyList<FormAccessGrantDto>> ListGrantsAsync(
        Guid formDefinitionId,
        CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageAccess);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formDefinitionId, cancellationToken: cancellationToken);

        var rows = await db.FormAccessGrants
            .Where(g => g.FormDefinitionId == form.Id)
            .OrderByDescending(g => g.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await MapGrantsAsync(rows, cancellationToken);
    }

    public async Task<FormAccessGrantDto> CreateGrantAsync(
        Guid formDefinitionId,
        CreateFormAccessGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageAccess);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formDefinitionId, cancellationToken: cancellationToken);
        var grantorId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");

        await sod.EnforceGrantAsync(form, grantorId, null, cancellationToken);
        await EnsurePrincipalExistsAsync(request.PrincipalType, request.PrincipalId, cancellationToken);
        ValidateGrantScopeShape(request.ScopeType, request.RegionId, request.FacilityId);

        var grantProbe = new FormAccessGrant
        {
            ScopeType = request.ScopeType,
            RegionId = request.RegionId,
            FacilityId = request.FacilityId
        };
        if (!FormGrantResolver.GrantScopeWithinGrantorScope(grantProbe, orgScope))
        {
            throw new UnauthorizedAccessException("نطاق المنح يتجاوز نطاق المستخدم.");
        }

        if (request.ScopeType.HasValue)
        {
            formScope.ValidateScopeShape(request.ScopeType.Value, request.RegionId, request.FacilityId, null);
            await formScope.EnsureOrgEntitiesActiveAsync(request.ScopeType.Value, request.RegionId, request.FacilityId, null, cancellationToken);
        }

        if (request.ValidFromUtc.HasValue && request.ValidToUtc.HasValue && request.ValidToUtc <= request.ValidFromUtc)
        {
            throw new InvalidOperationException("ValidTo يجب أن يكون بعد ValidFrom.");
        }

        var grant = new FormAccessGrant
        {
            FormDefinitionId = form.Id,
            PrincipalType = request.PrincipalType,
            PrincipalId = request.PrincipalId,
            Capability = request.Capability,
            Effect = request.Effect,
            ScopeType = request.ScopeType,
            RegionId = request.RegionId,
            FacilityId = request.FacilityId,
            ScopeKey = FormAccessGrant.BuildScopeKey(request.ScopeType, request.RegionId, request.FacilityId),
            ValidFromUtc = request.ValidFromUtc,
            ValidToUtc = request.ValidToUtc,
            Reason = request.Reason.Trim(),
            CreatedByUserId = grantorId,
            CreatedBy = currentUser.ExternalSubject
        };

        db.Add(grant);
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormAccessGrantCreated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormAccessGrant),
            EntityId = grant.Id.ToString(),
            NewValues = new
            {
                grant.FormDefinitionId,
                grant.PrincipalType,
                grant.PrincipalId,
                grant.Capability,
                grant.Effect,
                grant.ScopeType,
                grant.RegionId,
                grant.FacilityId,
                grant.ValidFromUtc,
                grant.ValidToUtc
            },
            Reason = grant.Reason
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await MapGrantsAsync([grant], cancellationToken)).Single();
    }

    public async Task RevokeGrantAsync(
        Guid formDefinitionId,
        Guid grantId,
        FormTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageAccess);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formDefinitionId, cancellationToken: cancellationToken);
        var grant = await db.FormAccessGrantsIncludingDeleted.FirstOrDefaultAsync(
            g => g.Id == grantId && g.FormDefinitionId == form.Id,
            cancellationToken)
            ?? throw new KeyNotFoundException("منح الوصول غير موجود.");

        FormAccessHelper.EnsureRowVersion(grant.RowVersion, request.RowVersion);
        if (grant.IsDeleted)
        {
            throw new InvalidOperationException("منح الوصول ملغى مسبقًا.");
        }

        var revokerId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        var now = DateTimeOffset.UtcNow;
        grant.IsDeleted = true;
        grant.DeletedAtUtc = now;
        grant.DeletedBy = currentUser.ExternalSubject;
        grant.RevokedAtUtc = now;
        grant.RevokedByUserId = revokerId;
        grant.UpdatedAtUtc = now;
        grant.UpdatedBy = currentUser.ExternalSubject;
        db.Update(grant);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormAccessGrantRevoked",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormAccessGrant),
            EntityId = grant.Id.ToString(),
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePrincipalExistsAsync(
        FormAccessGrantPrincipalType principalType,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        var exists = principalType switch
        {
            FormAccessGrantPrincipalType.User => await db.Users.AnyAsync(u => u.Id == principalId && !u.IsDeleted, cancellationToken),
            FormAccessGrantPrincipalType.Role => await db.Roles.AnyAsync(r => r.Id == principalId && !r.IsDeleted, cancellationToken),
            _ => false
        };

        if (!exists)
        {
            throw new KeyNotFoundException("الجهة الممنوحة غير موجودة.");
        }
    }

    private static void ValidateGrantScopeShape(ScopeType? scopeType, Guid? regionId, Guid? facilityId)
    {
        if (!scopeType.HasValue)
        {
            if (regionId.HasValue || facilityId.HasValue)
            {
                throw new InvalidOperationException("نطاق المنح غير محدد لا يقبل معرفات.");
            }

            return;
        }

        switch (scopeType.Value)
        {
            case ScopeType.Global:
            case ScopeType.Headquarters:
                if (regionId.HasValue || facilityId.HasValue)
                {
                    throw new InvalidOperationException("نطاق Global/Headquarters لا يقبل معرفات.");
                }

                break;
            case ScopeType.Region:
                if (!regionId.HasValue || facilityId.HasValue)
                {
                    throw new InvalidOperationException("نطاق المنطقة يتطلب RegionId فقط.");
                }

                break;
            case ScopeType.Facility:
                if (!facilityId.HasValue)
                {
                    throw new InvalidOperationException("نطاق السجن يتطلب FacilityId.");
                }

                break;
            default:
                throw new InvalidOperationException("نطاق المنح غير مدعوم.");
        }
    }

    private async Task<IReadOnlyList<FormAccessGrantDto>> MapGrantsAsync(
        IReadOnlyList<FormAccessGrant> rows,
        CancellationToken cancellationToken)
    {
        var userIds = rows.Where(r => r.PrincipalType == FormAccessGrantPrincipalType.User).Select(r => r.PrincipalId).ToHashSet();
        var roleIds = rows.Where(r => r.PrincipalType == FormAccessGrantPrincipalType.Role).Select(r => r.PrincipalId).ToHashSet();
        var creatorIds = rows.Select(r => r.CreatedByUserId).ToHashSet();

        var users = await db.UsersIncludingDeleted.Where(u => userIds.Union(creatorIds).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameAr, cancellationToken);
        var roles = await db.RolesIncludingDeleted.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.NameAr, cancellationToken);

        return rows.Select(g => new FormAccessGrantDto(
            g.Id,
            g.PrincipalType,
            g.PrincipalId,
            g.PrincipalType == FormAccessGrantPrincipalType.User
                ? users.GetValueOrDefault(g.PrincipalId)
                : roles.GetValueOrDefault(g.PrincipalId),
            g.Capability,
            FormDisplay.CapabilityAr(g.Capability),
            g.Effect,
            g.ScopeType,
            g.RegionId,
            g.FacilityId,
            g.ValidFromUtc,
            g.ValidToUtc,
            g.Reason,
            g.CreatedByUserId,
            users.GetValueOrDefault(g.CreatedByUserId),
            g.CreatedAtUtc,
            Convert.ToBase64String(g.RowVersion))).ToList();
    }
}
