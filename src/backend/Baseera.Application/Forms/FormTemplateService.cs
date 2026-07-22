namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Application.Forms.Schema;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormTemplateService
{
    Task<IReadOnlyList<FormTemplateListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<FormTemplateListItemDto> CreateFromLockedVersionAsync(CreateFormTemplateRequest request, CancellationToken cancellationToken = default);
    Task<FormDetailDto> CreateFormFromTemplateAsync(Guid templateId, CreateFormFromTemplateRequest request, CancellationToken cancellationToken = default);
}

public sealed class FormTemplateService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IFormSchemaCanonicalizer canonicalizer,
    IAuditService audit,
    IFormQueryService queries) : IFormTemplateService
{
    public async Task<IReadOnlyList<FormTemplateListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsView);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var canSensitive = FormAccessHelper.CanViewSensitive(currentUser);
        var scopedFormIds = await formScope.FilterQueryable(db.FormDefinitions)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);
        var accessibleDepartmentIds = await formScope.FilterQueryable(db.FormDefinitions)
            .Where(f => f.OwnerDepartmentId != null)
            .Select(f => f.OwnerDepartmentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query = db.FormTemplates.AsNoTracking();
        if (!canSensitive)
        {
            query = query.Where(t => t.Classification < ClassificationLevel.Confidential);
        }

        query = query.Where(t =>
            t.Visibility == FormTemplateVisibility.Organization
            || (t.Visibility == FormTemplateVisibility.Private && t.OwnerUserId == userId)
            || (t.Visibility == FormTemplateVisibility.Department
                && t.OwnerDepartmentId != null
                && (t.OwnerUserId == userId || accessibleDepartmentIds.Contains(t.OwnerDepartmentId.Value))));

        query = query.Where(t => !t.SourceFormDefinitionId.HasValue || scopedFormIds.Contains(t.SourceFormDefinitionId.Value));

        var templates = await query.OrderBy(t => t.NameAr).ToListAsync(cancellationToken);
        return templates.Select(Map).ToList();
    }

    public async Task<FormTemplateListItemDto> CreateFromLockedVersionAsync(
        CreateFormTemplateRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageTemplates);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, request.FormDefinitionId, cancellationToken: cancellationToken);
        var version = await db.FormVersions.FirstOrDefaultAsync(
            v => v.Id == request.FormVersionId && v.FormDefinitionId == form.Id, cancellationToken)
            ?? throw new KeyNotFoundException("إصدار النموذج غير موجود.");
        if (version.Status != FormVersionStatus.Locked || version.SnapshotId is null)
        {
            throw new InvalidOperationException("يمكن حفظ القوالب من إصدار مقفل فقط.");
        }

        var snapshot = await db.FormSchemaSnapshots.AsNoTracking()
            .FirstAsync(s => s.Id == version.SnapshotId, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var template = new FormTemplate
        {
            Code = request.Code.Trim(),
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn,
            Description = request.Description.Trim(),
            Category = request.Category.Trim(),
            Classification = form.Classification,
            Visibility = request.Visibility,
            OwnerDepartmentId = request.OwnerDepartmentId ?? form.OwnerDepartmentId,
            OwnerUserId = userId,
            SourceFormDefinitionId = form.Id,
            SourceFormVersionId = version.Id,
            SchemaFormatVersion = snapshot.SchemaFormatVersion,
            CanonicalSchemaJson = snapshot.CanonicalSchemaJson,
            SchemaHash = snapshot.SchemaHash,
            SchemaSizeBytes = snapshot.SchemaSizeBytes,
            PageCount = snapshot.PageCount,
            SectionCount = snapshot.SectionCount,
            FieldCount = snapshot.FieldCount
        };
        db.Add(template);
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormTemplateCreated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormTemplate),
            EntityId = template.Id.ToString("D"),
            NewValues = new { template.Code, template.SchemaHash, FormDefinitionId = form.Id, FormVersionId = version.Id, version.VersionNumber }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Map(template);
    }

    public async Task<FormDetailDto> CreateFormFromTemplateAsync(
        Guid templateId, CreateFormFromTemplateRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageTemplates);
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsCreate);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            var template = await db.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct)
                ?? throw new KeyNotFoundException("القالب غير موجود.");

            var canonical = canonicalizer.Canonicalize(template.CanonicalSchemaJson, requireMinimumContent: true);
            if (!canonical.IsValid)
            {
                throw new ArgumentException(canonical.Issues.FirstOrDefault()?.MessageAr ?? "مخطط القالب غير صالح.");
            }

            if (!string.Equals(template.SchemaHash, canonical.SchemaHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("تجزئة مخطط القالب لا تطابق المحتوى المخزن.");
            }

            formScope.ValidateScopeShape(request.ScopeType, request.RegionId, request.FacilityId, request.FacilityUnitId);
            await formScope.EnsureOrgEntitiesActiveAsync(
                request.ScopeType, request.RegionId, request.FacilityId, request.FacilityUnitId, ct);

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
                !await db.Departments.AnyAsync(d => d.Id == request.OwnerDepartmentId.Value && !d.IsDeleted, ct))
            {
                throw new KeyNotFoundException("الإدارة غير موجودة.");
            }

            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            if (await db.FormDefinitions.AnyAsync(f => f.Code == normalizedCode, ct))
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
            await db.SaveChangesAsync(ct);

            var nextNumber = await db.AllocateFormVersionNumberAsync(form.Id, ct);
            var version = new FormVersion
            {
                FormDefinitionId = form.Id,
                VersionNumber = nextNumber,
                Status = FormVersionStatus.Draft,
                DraftSchemaJson = canonical.CanonicalJson,
                DraftSchemaHash = canonical.SchemaHash,
                SchemaFormatVersion = FormSchemaValidator.CurrentSchemaFormatVersion,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                LastSavedAtUtc = DateTimeOffset.UtcNow
            };
            db.Add(version);

            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormCreated",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormDefinition),
                EntityId = form.Id.ToString("D"),
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
            }, ct);
            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormVersionCreatedFromTemplate",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormDefinition),
                EntityId = form.Id.ToString("D"),
                NewValues = new { templateId, FormDefinitionId = form.Id, FormVersionId = version.Id, template.SchemaHash }
            }, ct);

            await db.SaveChangesAsync(ct);
            return (await queries.GetDetailAsync(form.Id, ct))!;
        }, cancellationToken);
    }

    private static FormTemplateListItemDto Map(FormTemplate template) => new(
        template.Id, template.Code, template.NameAr, template.NameEn, template.Description, template.Category,
        template.Classification, template.Visibility, template.OwnerDepartmentId, template.SchemaHash,
        template.PageCount, template.SectionCount, template.FieldCount, template.CreatedAtUtc);
}
