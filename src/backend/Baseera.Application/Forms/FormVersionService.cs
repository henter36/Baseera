using System.Text.Json;
namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Application.Forms.Schema;
using Baseera.Domain.Forms;
using Baseera.Domain.Forms.Schema;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormVersionService
{
    Task<IReadOnlyList<FormVersionListItemDto>> ListAsync(Guid formId, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> GetAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> CreateAsync(Guid formId, CreateFormVersionRequest request, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> CloneAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> SaveSchemaAsync(Guid formId, Guid versionId, SaveFormSchemaRequest request, bool autosave, CancellationToken cancellationToken = default);
    Task<FormVersionValidateResultDto> ValidateAsync(Guid formId, Guid versionId, SaveFormSchemaRequest request, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> SubmitForReviewAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> RequestChangesAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> RejectAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> ReopenAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormVersionDetailDto> ApproveAndLockAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormSchemaSnapshotDto> GetSnapshotAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormVersionReviewDecisionDto>> GetReviewDecisionsAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default);
}

public sealed class FormVersionService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IFormEffectiveAccessService effectiveAccess,
    IFormSeparationOfDutiesService sod,
    IFormSchemaCanonicalizer canonicalizer,
    IAuditService audit) : IFormVersionService
{
    public async Task<IReadOnlyList<FormVersionListItemDto>> ListAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        var versions = await db.FormVersions.AsNoTracking()
            .Where(v => v.FormDefinitionId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
        return versions.Select(MapListItem).ToList();
    }

    public async Task<FormVersionDetailDto> GetAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        var version = await LoadVersionAsync(formId, versionId, cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.View, cancellationToken);
        return MapDetail(version, form);
    }

    public async Task<FormVersionDetailDto> CreateAsync(Guid formId, CreateFormVersionRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsUpdateDraft);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Design, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            string schemaJson;
            Guid? basedOn = request.BasedOnVersionId;
            if (basedOn is Guid basedOnId)
            {
                var source = await db.FormVersions.FirstOrDefaultAsync(v => v.Id == basedOnId && v.FormDefinitionId == formId, ct)
                    ?? throw new KeyNotFoundException("الإصدار المصدر غير موجود.");
                schemaJson = source.Status == FormVersionStatus.Locked && source.SnapshotId is not null
                    ? (await db.FormSchemaSnapshots.AsNoTracking().FirstAsync(s => s.Id == source.SnapshotId, ct)).CanonicalSchemaJson
                    : source.DraftSchemaJson;
            }
            else
            {
                var latestLocked = await db.FormVersions
                    .Where(v => v.FormDefinitionId == formId && v.Status == FormVersionStatus.Locked)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefaultAsync(ct);
                if (latestLocked?.SnapshotId is Guid snapId)
                {
                    schemaJson = (await db.FormSchemaSnapshots.AsNoTracking().FirstAsync(s => s.Id == snapId, ct)).CanonicalSchemaJson;
                    basedOn = latestLocked.Id;
                }
                else
                {
                    schemaJson = DefaultSchemaJson();
                }
            }

            var nextNumber = await NextVersionNumberAsync(formId, ct);
            var canonical = canonicalizer.Canonicalize(schemaJson);
            var version = new FormVersion
            {
                FormDefinitionId = formId,
                VersionNumber = nextNumber,
                Status = FormVersionStatus.Draft,
                BasedOnVersionId = basedOn,
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
                Action = "FormVersionCreated",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormVersion),
                EntityId = version.Id.ToString("D"),
                NewValues = new { formId, version.VersionNumber, version.DraftSchemaHash }
            }, ct);
            await db.SaveChangesAsync(ct);
            return MapDetail(version, form);
        }, cancellationToken);
    }

    public async Task<FormVersionDetailDto> CloneAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsCloneVersion);
        return await CreateAsync(formId, new CreateFormVersionRequest(versionId), cancellationToken);
    }

    public async Task<FormVersionDetailDto> SaveSchemaAsync(
        Guid formId, Guid versionId, SaveFormSchemaRequest request, bool autosave, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsUpdateDraft);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Design, cancellationToken);
        var version = await LoadVersionAsync(formId, versionId, cancellationToken);
        FormVersionStateMachine.EnsureEditable(version.Status);
        FormAccessHelper.EnsureRowVersion(version.RowVersion, request.RowVersion);

        var canonical = canonicalizer.Canonicalize(request.SchemaJson);
        if (string.Equals(version.DraftSchemaHash, canonical.SchemaHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(version.DraftSchemaJson, canonical.CanonicalJson, StringComparison.Ordinal))
        {
            return MapDetail(version, form);
        }

        version.DraftSchemaJson = canonical.CanonicalJson;
        version.DraftSchemaHash = canonical.SchemaHash;
        version.SchemaFormatVersion = FormSchemaValidator.CurrentSchemaFormatVersion;
        version.UpdatedByUserId = currentUser.UserId;
        version.UpdatedAtUtc = DateTimeOffset.UtcNow;
        version.LastSavedAtUtc = DateTimeOffset.UtcNow;
        form.LastModifiedByUserId = currentUser.UserId;
        form.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await audit.WriteAsync(new AuditEntry
        {
            Action = autosave ? "FormVersionAutosaved" : "FormVersionSchemaSaved",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormVersion),
            EntityId = version.Id.ToString("D"),
            NewValues = JsonSerializer.Serialize(new
            {
                formId,
                version.VersionNumber,
                canonical.SchemaHash,
                canonical.PageCount,
                canonical.SectionCount,
                canonical.FieldCount
            })
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(version, form);
    }

    public async Task<FormVersionValidateResultDto> ValidateAsync(
        Guid formId, Guid versionId, SaveFormSchemaRequest request, CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        var version = await LoadVersionAsync(formId, versionId, cancellationToken);
        FormAccessHelper.EnsureRowVersion(version.RowVersion, request.RowVersion);
        var json = string.IsNullOrWhiteSpace(request.SchemaJson) ? version.DraftSchemaJson : request.SchemaJson;
        var canonical = canonicalizer.Canonicalize(json, requireMinimumContent: true);
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormVersionValidated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormVersion),
            EntityId = version.Id.ToString("D"),
            NewValues = new { formId, version.VersionNumber, canonical.SchemaHash, canonical.IsValid }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new FormVersionValidateResultDto(
            canonical.IsValid,
            canonical.SchemaHash,
            canonical.Issues,
            canonical.PageCount,
            canonical.SectionCount,
            canonical.FieldCount,
            canonical.CalculatedFieldCount,
            canonical.ConditionCount);
    }

    public Task<FormVersionDetailDto> SubmitForReviewAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(formId, versionId, request, PermissionCodes.FormsSubmitForReview, FormAccessCapability.Design,
            FormVersionStatus.InReview, FormVersionReviewDecisionType.SubmitForReview, "FormVersionSubmittedForReview",
            async (form, version, ct) =>
            {
                var canonical = canonicalizer.Canonicalize(version.DraftSchemaJson, requireMinimumContent: true);
                if (!canonical.IsValid)
                {
                    throw new ArgumentException(canonical.Issues.FirstOrDefault()?.MessageAr ?? "مخطط النموذج غير صالح.");
                }

                version.DraftSchemaJson = canonical.CanonicalJson;
                version.DraftSchemaHash = canonical.SchemaHash;
                await sod.EnforceSubmitForReviewAsync(form, currentUser.UserId!.Value, request.Reason, ct);
                version.SubmittedForReviewAtUtc = DateTimeOffset.UtcNow;
            }, cancellationToken);

    public Task<FormVersionDetailDto> RequestChangesAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(formId, versionId, request, PermissionCodes.FormsRequestChanges, FormAccessCapability.Review,
            FormVersionStatus.ChangesRequested, FormVersionReviewDecisionType.RequestChanges, "FormVersionChangesRequested",
            async (form, _, ct) => await sod.EnforceReviewAsync(form, currentUser.UserId!.Value, request.Reason, ct), cancellationToken);

    public Task<FormVersionDetailDto> RejectAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(formId, versionId, request, PermissionCodes.FormsReject, FormAccessCapability.Review,
            FormVersionStatus.Rejected, FormVersionReviewDecisionType.Reject, "FormVersionRejected",
            async (form, _, ct) => await sod.EnforceReviewAsync(form, currentUser.UserId!.Value, request.Reason, ct), cancellationToken);

    public Task<FormVersionDetailDto> ReopenAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(formId, versionId, request, PermissionCodes.FormsUpdateDraft, FormAccessCapability.Design,
            FormVersionStatus.Draft, FormVersionReviewDecisionType.Reopen, "FormVersionReopened",
            (_, _, _) => Task.CompletedTask, cancellationToken);

    public async Task<FormVersionDetailDto> ApproveAndLockAsync(
        Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsApprove);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: ct);
            await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Approve, ct);
            var version = await LoadVersionAsync(formId, versionId, ct);
            FormAccessHelper.EnsureRowVersion(version.RowVersion, request.RowVersion);
            FormVersionStateMachine.EnsureAllowed(version.Status, FormVersionStatus.Locked);
            await sod.EnforceApproveAsync(form, userId, request.Reason, ct);

            var canonical = canonicalizer.Canonicalize(version.DraftSchemaJson, requireMinimumContent: true);
            if (!canonical.IsValid)
            {
                throw new ArgumentException(canonical.Issues.FirstOrDefault()?.MessageAr ?? "مخطط النموذج غير صالح.");
            }

            var from = version.Status;
            var snapshot = new FormSchemaSnapshot
            {
                FormVersionId = version.Id,
                SchemaFormatVersion = FormSchemaValidator.CurrentSchemaFormatVersion,
                CanonicalSchemaJson = canonical.CanonicalJson,
                SchemaHash = canonical.SchemaHash,
                SchemaSizeBytes = canonical.SchemaSizeBytes,
                PageCount = canonical.PageCount,
                SectionCount = canonical.SectionCount,
                FieldCount = canonical.FieldCount,
                CalculatedFieldCount = canonical.CalculatedFieldCount,
                ConditionCount = canonical.ConditionCount,
                CreatedByUserId = userId
            };
            db.Add(snapshot);
            version.Snapshot = snapshot;
            version.SnapshotId = snapshot.Id;
            version.DraftSchemaJson = canonical.CanonicalJson;
            version.DraftSchemaHash = canonical.SchemaHash;
            version.Status = FormVersionStatus.Locked;
            version.ApprovedAtUtc = DateTimeOffset.UtcNow;
            version.ApprovedByUserId = userId;
            version.UpdatedByUserId = userId;
            version.UpdatedAtUtc = DateTimeOffset.UtcNow;
            form.CurrentLockedVersionId = version.Id;
            form.LastModifiedByUserId = userId;
            form.UpdatedAtUtc = DateTimeOffset.UtcNow;

            db.Add(new FormVersionReviewDecision
            {
                FormVersionId = version.Id,
                Decision = FormVersionReviewDecisionType.ApproveAndLock,
                Reason = request.Reason,
                ReviewedByUserId = userId,
                FromStatus = from,
                ToStatus = FormVersionStatus.Locked
            });

            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormSchemaSnapshotCreated",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormSchemaSnapshot),
                EntityId = snapshot.Id.ToString("D"),
                NewValues = JsonSerializer.Serialize(new
                {
                    formId,
                    versionId = version.Id,
                    version.VersionNumber,
                    snapshot.SchemaHash,
                    snapshot.PageCount,
                    snapshot.FieldCount
                })
            }, ct);
            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormVersionApprovedAndLocked",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormVersion),
                EntityId = version.Id.ToString("D"),
                Reason = request.Reason,
                OldValues = new { Status = from },
                NewValues = JsonSerializer.Serialize(new
                {
                    Status = FormVersionStatus.Locked,
                    version.VersionNumber,
                    snapshot.SchemaHash
                })
            }, ct);

            await db.SaveChangesAsync(ct);
            return MapDetail(version, form);
        }, cancellationToken);
    }

    public async Task<FormSchemaSnapshotDto> GetSnapshotAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        var version = await LoadVersionAsync(formId, versionId, cancellationToken);
        if (version.SnapshotId is null)
        {
            throw new KeyNotFoundException("لا توجد لقطة لهذا الإصدار.");
        }

        var snapshot = await db.FormSchemaSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == version.SnapshotId, cancellationToken)
            ?? throw new KeyNotFoundException("لا توجد لقطة لهذا الإصدار.");
        return new FormSchemaSnapshotDto(
            snapshot.Id, snapshot.FormVersionId, snapshot.SchemaFormatVersion, snapshot.CanonicalSchemaJson,
            snapshot.SchemaHash, snapshot.SchemaSizeBytes, snapshot.PageCount, snapshot.SectionCount,
            snapshot.FieldCount, snapshot.CalculatedFieldCount, snapshot.ConditionCount,
            snapshot.CreatedByUserId, snapshot.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<FormVersionReviewDecisionDto>> GetReviewDecisionsAsync(
        Guid formId, Guid versionId, CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await LoadVersionAsync(formId, versionId, cancellationToken);
        var decisions = await db.FormVersionReviewDecisions.AsNoTracking()
            .Where(d => d.FormVersionId == versionId)
            .OrderByDescending(d => d.ReviewedAtUtc)
            .ToListAsync(cancellationToken);
        return decisions.Select(d => new FormVersionReviewDecisionDto(
            d.Id, d.Decision, FormVersionLabels.DecisionAr(d.Decision), d.Reason, d.ReviewedByUserId,
            d.ReviewedAtUtc, d.FromStatus, d.ToStatus, d.IsAdministrativeOverride)).ToList();
    }

    private async Task<FormVersionDetailDto> TransitionAsync(
        Guid formId,
        Guid versionId,
        FormVersionTransitionRequest request,
        string permission,
        FormAccessCapability capability,
        FormVersionStatus target,
        FormVersionReviewDecisionType decisionType,
        string auditAction,
        Func<FormDefinition, FormVersion, CancellationToken, Task> beforeSave,
        CancellationToken cancellationToken)
    {
        FormAccessHelper.EnsurePermission(currentUser, permission);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, capability, cancellationToken);
        var version = await LoadVersionAsync(formId, versionId, cancellationToken);
        FormAccessHelper.EnsureRowVersion(version.RowVersion, request.RowVersion);
        FormVersionStateMachine.EnsureAllowed(version.Status, target);
        await beforeSave(form, version, cancellationToken);

        var from = version.Status;
        version.Status = target;
        version.UpdatedByUserId = userId;
        version.UpdatedAtUtc = DateTimeOffset.UtcNow;
        form.LastModifiedByUserId = userId;
        db.Add(new FormVersionReviewDecision
        {
            FormVersionId = version.Id,
            Decision = decisionType,
            Reason = request.Reason,
            ReviewedByUserId = userId,
            FromStatus = from,
            ToStatus = target
        });
        await audit.WriteAsync(new AuditEntry
        {
            Action = auditAction,
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormVersion),
            EntityId = version.Id.ToString("D"),
            Reason = request.Reason,
            OldValues = new { Status = from },
            NewValues = new { Status = target, version.VersionNumber, version.DraftSchemaHash }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return MapDetail(version, form);
    }

    private async Task<FormVersion> LoadVersionAsync(Guid formId, Guid versionId, CancellationToken cancellationToken)
    {
        var version = await db.FormVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.FormDefinitionId == formId, cancellationToken);
        return version ?? throw new KeyNotFoundException("إصدار النموذج غير موجود.");
    }

    private async Task<int> NextVersionNumberAsync(Guid formId, CancellationToken cancellationToken)
    {
        var max = await db.FormVersions.Where(v => v.FormDefinitionId == formId).Select(v => (int?)v.VersionNumber).MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    private void EnsureViewPermission()
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsView)
            && !currentUser.HasPermission(PermissionCodes.FormsViewVersionHistory))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    private static FormVersionListItemDto MapListItem(FormVersion version) => new(
        version.Id, version.FormDefinitionId, version.VersionNumber, version.Status,
        FormVersionLabels.StatusAr(version.Status), version.BasedOnVersionId, version.DraftSchemaHash,
        version.SchemaFormatVersion, version.CreatedAtUtc, version.LastSavedAtUtc, version.ApprovedAtUtc,
        version.SnapshotId, Convert.ToBase64String(version.RowVersion));

    private FormVersionDetailDto MapDetail(FormVersion version, FormDefinition form)
    {
        var actions = new List<string>();
        if (FormVersionStateMachine.IsEditable(version.Status)
            && currentUser.HasPermission(PermissionCodes.FormsUpdateDraft))
        {
            actions.Add("UpdateDraft");
            actions.Add("SaveSchema");
            actions.Add("Autosave");
            actions.Add("Validate");
        }

        if (version.Status == FormVersionStatus.Draft && currentUser.HasPermission(PermissionCodes.FormsSubmitForReview))
        {
            actions.Add("SubmitForReview");
        }

        if (version.Status == FormVersionStatus.InReview)
        {
            if (currentUser.HasPermission(PermissionCodes.FormsRequestChanges)) actions.Add("RequestChanges");
            if (currentUser.HasPermission(PermissionCodes.FormsReject)) actions.Add("Reject");
            if (currentUser.HasPermission(PermissionCodes.FormsApprove)) actions.Add("ApproveAndLock");
        }

        if (version.Status == FormVersionStatus.ChangesRequested && currentUser.HasPermission(PermissionCodes.FormsSubmitForReview))
        {
            actions.Add("SubmitForReview");
        }

        if (version.Status == FormVersionStatus.Rejected && currentUser.HasPermission(PermissionCodes.FormsUpdateDraft))
        {
            actions.Add("Reopen");
        }

        if (currentUser.HasPermission(PermissionCodes.FormsCloneVersion)) actions.Add("Clone");
        if (version.SnapshotId is not null) actions.Add("ViewSnapshot");

        return new FormVersionDetailDto(
            version.Id, version.FormDefinitionId, version.VersionNumber, version.Status,
            FormVersionLabels.StatusAr(version.Status), version.BasedOnVersionId, version.DraftSchemaJson,
            version.DraftSchemaHash, version.SchemaFormatVersion, version.CreatedByUserId, version.UpdatedByUserId,
            version.CreatedAtUtc, version.LastSavedAtUtc, version.SubmittedForReviewAtUtc, version.ApprovedAtUtc,
            version.ApprovedByUserId, version.SnapshotId, Convert.ToBase64String(version.RowVersion), actions);
    }

    private static string DefaultSchemaJson()
    {
        var pageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var doc = new FormSchemaDocument
        {
            SchemaFormatVersion = 1,
            Pages =
            [
                new FormPageSchema
                {
                    Id = pageId,
                    Key = "page1",
                    TitleAr = "الصفحة 1",
                    Order = 0,
                    Sections =
                    [
                        new FormSectionSchema
                        {
                            Id = sectionId,
                            Key = "section1",
                            TitleAr = "القسم 1",
                            Order = 0,
                            Fields =
                            [
                                new FormFieldSchema
                                {
                                    Id = fieldId,
                                    Key = "field1",
                                    Type = FormFieldType.ShortText,
                                    LabelAr = "حقل نصي",
                                    Order = 0
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        return JsonSerializer.Serialize(doc, FormSchemaCanonicalizer.SerializerOptions);
    }
}
