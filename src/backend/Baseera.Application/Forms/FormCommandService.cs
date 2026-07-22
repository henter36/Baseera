namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormCommandService
{
    Task<FormDetailDto> CreateDraftAsync(CreateFormRequest request, CancellationToken cancellationToken = default);
    Task<FormDetailDto> UpdateAsync(Guid id, UpdateFormRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default);
}

public sealed class FormCommandService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IFormRetentionPolicyService retention,
    IFormEffectiveAccessService effectiveAccess,
    IAuditService audit,
    IFormQueryService queries) : IFormCommandService
{
    private const string UnauthenticatedUserMessage = "المستخدم غير مصادق.";

    public async Task<FormDetailDto> CreateDraftAsync(CreateFormRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsCreate);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException(UnauthenticatedUserMessage);

        formScope.ValidateScopeShape(request.ScopeType, request.RegionId, request.FacilityId, request.FacilityUnitId);
        await formScope.EnsureOrgEntitiesActiveAsync(
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            cancellationToken);

        var probe = new FormDefinition
        {
            ScopeType = request.ScopeType,
            RegionId = request.RegionId,
            FacilityId = request.FacilityId,
            FacilityUnitId = request.FacilityUnitId
        };
        if (!formScope.CanAccess(probe))
        {
            throw new UnauthorizedAccessException("لا صلاحية على نطاق هذا النموذج.");
        }

        if (request.OwnerDepartmentId.HasValue &&
            !await db.Departments.AnyAsync(d => d.Id == request.OwnerDepartmentId.Value && !d.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("الإدارة غير موجودة.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await db.FormDefinitions.AnyAsync(f => f.Code == normalizedCode, cancellationToken))
        {
            throw new InvalidOperationException("رمز النموذج مستخدم مسبقًا.");
        }

        var form = new FormDefinition
        {
            Code = normalizedCode,
            NameAr = request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            Description = request.Description.Trim(),
            Classification = request.Classification,
            ScopeType = request.ScopeType,
            RegionId = request.RegionId,
            FacilityId = request.FacilityId,
            FacilityUnitId = request.FacilityUnitId,
            OwnerDepartmentId = request.OwnerDepartmentId,
            Status = FormDefinitionStatus.Draft,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId,
            CreatedBy = currentUser.ExternalSubject
        };

        db.Add(form);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormCreated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormDefinition),
            EntityId = form.Id.ToString(),
            NewValues = new
            {
                form.Code,
                form.NameAr,
                form.Status,
                form.Classification,
                form.ScopeType,
                form.RegionId,
                form.FacilityId,
                form.FacilityUnitId
            }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(form.Id, cancellationToken))!;
    }

    public async Task<FormDetailDto> UpdateAsync(Guid id, UpdateFormRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsUpdateDraft);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, id, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Design, cancellationToken);
        FormAccessHelper.EnsureRowVersion(form.RowVersion, request.RowVersion);

        if (!FormDefinitionStateMachine.IsEditable(form.Status))
        {
            throw new InvalidOperationException("لا يمكن تعديل النموذج إلا في حالة مسودة أو تعديلات مطلوبة.");
        }

        if (request.OwnerDepartmentId.HasValue &&
            !await db.Departments.AnyAsync(d => d.Id == request.OwnerDepartmentId.Value && !d.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("الإدارة غير موجودة.");
        }

        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException(UnauthenticatedUserMessage);
        var old = new
        {
            form.NameAr,
            form.NameEn,
            form.Description,
            form.Classification,
            form.OwnerDepartmentId
        };

        form.NameAr = request.NameAr.Trim();
        form.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        form.Description = request.Description.Trim();
        form.Classification = request.Classification;
        form.OwnerDepartmentId = request.OwnerDepartmentId;
        form.UpdatedByUserId = userId;
        form.LastModifiedByUserId = userId;
        form.UpdatedAtUtc = DateTimeOffset.UtcNow;
        form.UpdatedBy = currentUser.ExternalSubject;
        db.Update(form);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormUpdated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormDefinition),
            EntityId = form.Id.ToString(),
            OldValues = old,
            NewValues = new
            {
                form.NameAr,
                form.NameEn,
                form.Description,
                form.Classification,
                form.OwnerDepartmentId
            },
            Reason = "تحديث حقول النموذج"
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(form.Id, cancellationToken))!;
    }

    public async Task ArchiveAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsArchive);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, id, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Archive, cancellationToken);
        FormAccessHelper.EnsureRowVersion(form.RowVersion, request.RowVersion);

        var policy = await db.FormGovernancePolicies.AsNoTracking().OrderBy(p => p.CreatedAtUtc).FirstAsync(cancellationToken);
        if (policy.RequireReasonForArchive && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("سبب الأرشفة مطلوب.");
        }

        if (form.Status == FormDefinitionStatus.Approved &&
            !await retention.IsEligibleForArchiveAsync(form, cancellationToken))
        {
            throw new InvalidOperationException("النموذج غير مؤهل للأرشفة وفق سياسة الاحتفاظ.");
        }

        FormDefinitionStateMachine.EnsureAllowed(form.Status, FormDefinitionStatus.Archived);

        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException(UnauthenticatedUserMessage);
        var from = form.Status;
        var now = DateTimeOffset.UtcNow;
        form.Status = FormDefinitionStatus.Archived;
        form.ArchivedAtUtc = now;
        form.ArchivedByUserId = userId;
        form.UpdatedAtUtc = now;
        form.UpdatedBy = currentUser.ExternalSubject;
        form.UpdatedByUserId = userId;
        db.Update(form);

        var archiveReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        FormReviewDecisionWriter.Append(
            db,
            new FormReviewDecisionWriteRequest(
                form.Id,
                FormReviewDecisionType.Archive,
                from,
                FormDefinitionStatus.Archived,
                userId,
                archiveReason,
                false));
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormArchived",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormDefinition),
            EntityId = form.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = FormDefinitionStatus.Archived },
            Reason = archiveReason
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsRestore);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, id, includeDeleted: true, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Restore, cancellationToken);
        FormAccessHelper.EnsureRowVersion(form.RowVersion, request.RowVersion);

        if (form.Status != FormDefinitionStatus.Archived)
        {
            throw new InvalidOperationException("لا يمكن استعادة النموذج إلا إذا كان في حالة مؤرشف.");
        }

        var archiveDecision = await db.FormReviewDecisions
            .Where(d => d.FormDefinitionId == form.Id && d.Decision == FormReviewDecisionType.Archive)
            .OrderByDescending(d => d.ReviewedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("لا يمكن استعادة النموذج: لا يوجد قرار أرشفة موثوق.");

        var priorStatus = archiveDecision.FromStatus;
        if (priorStatus == FormDefinitionStatus.Archived || !FormDefinitionStateMachine.CanRestore(priorStatus))
        {
            throw new InvalidOperationException("لا يمكن استعادة النموذج: الحالة السابقة للأرشفة غير صالحة للاستعادة.");
        }

        FormDefinitionStateMachine.EnsureAllowed(FormDefinitionStatus.Archived, priorStatus);

        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException(UnauthenticatedUserMessage);
        var from = form.Status;
        var now = DateTimeOffset.UtcNow;
        var wasSoftDeleted = form.IsDeleted;

        if (form.IsDeleted)
        {
            form.IsDeleted = false;
            form.DeletedAtUtc = null;
            form.DeletedBy = null;
            form.DeletedByUserId = null;
        }

        form.ArchivedAtUtc = null;
        form.ArchivedByUserId = null;
        form.Status = priorStatus;
        form.UpdatedAtUtc = now;
        form.UpdatedBy = currentUser.ExternalSubject;
        form.UpdatedByUserId = userId;
        db.Update(form);

        FormReviewDecisionWriter.Append(
            db,
            new FormReviewDecisionWriteRequest(
                form.Id,
                FormReviewDecisionType.Restore,
                from,
                priorStatus,
                userId,
                request.Reason.Trim(),
                false));
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormRestored",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormDefinition),
            EntityId = form.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = priorStatus, WasSoftDeleted = wasSoftDeleted },
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
