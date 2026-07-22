namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Application.Forms.Schema;
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
    IFormCommandService formCommands,
    IFormVersionService versions,
    IFormSchemaCanonicalizer canonicalizer,
    IAuditService audit) : IFormTemplateService
{
    public async Task<IReadOnlyList<FormTemplateListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsView);
        var templates = await db.FormTemplates.AsNoTracking()
            .OrderBy(t => t.NameAr)
            .ToListAsync(cancellationToken);
        var userId = currentUser.UserId;
        return templates
            .Where(t => t.Visibility == FormTemplateVisibility.Organization
                        || (t.Visibility == FormTemplateVisibility.Private && t.OwnerUserId == userId)
                        || t.Visibility == FormTemplateVisibility.Department)
            .Select(Map)
            .ToList();
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
        var template = await db.FormTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            ?? throw new KeyNotFoundException("القالب غير موجود.");
        _ = canonicalizer.Canonicalize(template.CanonicalSchemaJson);

        var form = await formCommands.CreateDraftAsync(new CreateFormRequest(
            request.Code,
            request.NameAr,
            request.NameEn,
            request.Description,
            request.Classification,
            request.ScopeType,
            request.RegionId,
            request.FacilityId,
            request.FacilityUnitId,
            request.OwnerDepartmentId), cancellationToken);

        var version = await versions.CreateAsync(form.Id, new CreateFormVersionRequest(null), cancellationToken);
        await versions.SaveSchemaAsync(form.Id, version.Id, new SaveFormSchemaRequest(template.CanonicalSchemaJson, version.RowVersion), autosave: false, cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormVersionCreatedFromTemplate",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormDefinition),
            EntityId = form.Id.ToString("D"),
            NewValues = new { templateId, FormDefinitionId = form.Id, FormVersionId = version.Id, template.SchemaHash }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return form;
    }

    private static FormTemplateListItemDto Map(FormTemplate template) => new(
        template.Id, template.Code, template.NameAr, template.NameEn, template.Description, template.Category,
        template.Classification, template.Visibility, template.OwnerDepartmentId, template.SchemaHash,
        template.PageCount, template.SectionCount, template.FieldCount, template.CreatedAtUtc);
}
