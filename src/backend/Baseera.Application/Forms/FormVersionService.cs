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
    private const int MaxVersionAllocationAttempts = 5;

    public async Task<IReadOnlyList<FormVersionListItemDto>> ListAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        EnsureVersionHistoryPermission();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await EnsureViewOrNotFoundAsync(form, cancellationToken);
        var versions = await db.FormVersions.AsNoTracking()
            .Where(v => v.FormDefinitionId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
        return versions.Select(MapListItem).ToList();
    }

    public async Task<FormVersionDetailDto> GetAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default)
    {
        EnsureVersionHistoryPermission();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await EnsureViewOrNotFoundAsync(form, cancellationToken);
        var version = await LoadVersionAsync(formId, versionId, cancellationToken);
        return await MapDetailAsync(version, form, cancellationToken);
    }

    public async Task<FormVersionDetailDto> CreateAsync(Guid formId, CreateFormVersionRequest request, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsUpdateDraft);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, FormAccessCapability.Design, cancellationToken);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();

        return await db.ExecuteInTransactionAsync(async ct =>
        {
            var (schemaJson, basedOn) = await ResolveSourceSchemaAsync(formId, request.BasedOnVersionId, ct);
            return await AllocateAndPersistVersionAsync(form, formId, basedOn, schemaJson, userId, ct);
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
            return await MapDetailAsync(version, form, cancellationToken);
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
            NewValues = new
            {
                formId,
                version.VersionNumber,
                canonical.SchemaHash,
                canonical.PageCount,
                canonical.SectionCount,
                canonical.FieldCount
            }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(version, form, cancellationToken);
    }

    public async Task<FormVersionValidateResultDto> ValidateAsync(
        Guid formId, Guid versionId, SaveFormSchemaRequest request, CancellationToken cancellationToken = default)
    {
        EnsureViewPermission();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await EnsureViewOrNotFoundAsync(form, cancellationToken);
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
        TransitionAsync(new VersionTransitionContext(
            formId, versionId, request, PermissionCodes.FormsSubmitForReview, FormAccessCapability.Design,
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
            }), cancellationToken);

    public Task<FormVersionDetailDto> RequestChangesAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(new VersionTransitionContext(
            formId, versionId, request, PermissionCodes.FormsRequestChanges, FormAccessCapability.Review,
            FormVersionStatus.ChangesRequested, FormVersionReviewDecisionType.RequestChanges, "FormVersionChangesRequested",
            async (form, _, ct) => await sod.EnforceReviewAsync(form, currentUser.UserId!.Value, request.Reason, ct)), cancellationToken);

    public Task<FormVersionDetailDto> RejectAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(new VersionTransitionContext(
            formId, versionId, request, PermissionCodes.FormsReject, FormAccessCapability.Review,
            FormVersionStatus.Rejected, FormVersionReviewDecisionType.Reject, "FormVersionRejected",
            async (form, _, ct) => await sod.EnforceReviewAsync(form, currentUser.UserId!.Value, request.Reason, ct)), cancellationToken);

    public Task<FormVersionDetailDto> ReopenAsync(Guid formId, Guid versionId, FormVersionTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(new VersionTransitionContext(
            formId, versionId, request, PermissionCodes.FormsUpdateDraft, FormAccessCapability.Design,
            FormVersionStatus.Draft, FormVersionReviewDecisionType.Reopen, "FormVersionReopened",
            (_, _, _) => Task.CompletedTask), cancellationToken);

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
            var snapshot = FormSchemaSnapshot.Create(new FormSchemaSnapshotData
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
            });
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
                NewValues = new
                {
                    formId,
                    versionId = version.Id,
                    version.VersionNumber,
                    snapshot.SchemaHash,
                    snapshot.PageCount,
                    snapshot.FieldCount
                }
            }, ct);
            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormVersionApprovedAndLocked",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormVersion),
                EntityId = version.Id.ToString("D"),
                Reason = request.Reason,
                OldValues = new { Status = from },
                NewValues = new
                {
                    Status = FormVersionStatus.Locked,
                    version.VersionNumber,
                    snapshot.SchemaHash
                }
            }, ct);

            await db.SaveChangesAsync(ct);
            return await MapDetailAsync(version, form, ct);
        }, cancellationToken);
    }

    public async Task<FormSchemaSnapshotDto> GetSnapshotAsync(Guid formId, Guid versionId, CancellationToken cancellationToken = default)
    {
        EnsureVersionHistoryPermission();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await EnsureViewOrNotFoundAsync(form, cancellationToken);
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
        EnsureVersionHistoryPermission();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, formId, cancellationToken: cancellationToken);
        await EnsureViewOrNotFoundAsync(form, cancellationToken);
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
        VersionTransitionContext context,
        CancellationToken cancellationToken)
    {
        FormAccessHelper.EnsurePermission(currentUser, context.Permission);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, context.FormId, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, context.Capability, cancellationToken);
        var version = await LoadVersionAsync(context.FormId, context.VersionId, cancellationToken);
        FormAccessHelper.EnsureRowVersion(version.RowVersion, context.Request.RowVersion);
        FormVersionStateMachine.EnsureAllowed(version.Status, context.Target);
        await context.BeforeSave(form, version, cancellationToken);

        var from = version.Status;
        version.Status = context.Target;
        version.UpdatedByUserId = userId;
        version.UpdatedAtUtc = DateTimeOffset.UtcNow;
        form.LastModifiedByUserId = userId;
        db.Add(new FormVersionReviewDecision
        {
            FormVersionId = version.Id,
            Decision = context.DecisionType,
            Reason = context.Request.Reason,
            ReviewedByUserId = userId,
            FromStatus = from,
            ToStatus = context.Target
        });
        await audit.WriteAsync(new AuditEntry
        {
            Action = context.AuditAction,
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormVersion),
            EntityId = version.Id.ToString("D"),
            Reason = context.Request.Reason,
            OldValues = new { Status = from },
            NewValues = new { Status = context.Target, version.VersionNumber, version.DraftSchemaHash }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(version, form, cancellationToken);
    }

    private async Task<(string SchemaJson, Guid? BasedOn)> ResolveSourceSchemaAsync(
        Guid formId,
        Guid? basedOnVersionId,
        CancellationToken cancellationToken)
    {
        if (basedOnVersionId is Guid basedOnId)
        {
            var source = await db.FormVersions.FirstOrDefaultAsync(v => v.Id == basedOnId && v.FormDefinitionId == formId, cancellationToken)
                ?? throw new KeyNotFoundException("الإصدار المصدر غير موجود.");
            var schemaJson = source.Status == FormVersionStatus.Locked && source.SnapshotId is not null
                ? (await db.FormSchemaSnapshots.AsNoTracking().FirstAsync(s => s.Id == source.SnapshotId, cancellationToken)).CanonicalSchemaJson
                : source.DraftSchemaJson;
            return (schemaJson, basedOnId);
        }

        var latestLocked = await db.FormVersions
            .Where(v => v.FormDefinitionId == formId && v.Status == FormVersionStatus.Locked)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestLocked?.SnapshotId is Guid snapId)
        {
            var schemaJson = (await db.FormSchemaSnapshots.AsNoTracking().FirstAsync(s => s.Id == snapId, cancellationToken)).CanonicalSchemaJson;
            return (schemaJson, latestLocked.Id);
        }

        return (DefaultSchemaJson(), null);
    }

    private async Task<FormVersionDetailDto> AllocateAndPersistVersionAsync(
        FormDefinition form,
        Guid formId,
        Guid? basedOn,
        string schemaJson,
        Guid userId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxVersionAllocationAttempts; attempt++)
        {
            try
            {
                var nextNumber = await db.AllocateFormVersionNumberAsync(formId, cancellationToken);
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
                }, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return await MapDetailAsync(version, form, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsFormVersionNumberConflict(ex) && attempt < MaxVersionAllocationAttempts - 1)
            {
                db.ClearChanges();
            }
        }

        throw new InvalidOperationException("تعذر تخصيص رقم إصدار جديد بسبب تعارض متزامن. أعد المحاولة.");
    }

    private async Task<FormVersion> LoadVersionAsync(Guid formId, Guid versionId, CancellationToken cancellationToken)
    {
        var version = await db.FormVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.FormDefinitionId == formId, cancellationToken);
        return version ?? throw new KeyNotFoundException("إصدار النموذج غير موجود.");
    }

    private static bool IsFormVersionNumberConflict(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("FormVersions", StringComparison.OrdinalIgnoreCase)
               && message.Contains("VersionNumber", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureViewPermission()
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsView)
            && !currentUser.HasPermission(PermissionCodes.FormsViewVersionHistory))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنفيذ هذه العملية.");
        }
    }

    private void EnsureVersionHistoryPermission() =>
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsViewVersionHistory);

    private async Task EnsureViewOrNotFoundAsync(FormDefinition form, CancellationToken cancellationToken)
    {
        if (!await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.View, cancellationToken))
        {
            throw new KeyNotFoundException("النموذج غير موجود.");
        }
    }

    private static FormVersionListItemDto MapListItem(FormVersion version) => new(
        version.Id, version.FormDefinitionId, version.VersionNumber, version.Status,
        FormVersionLabels.StatusAr(version.Status), version.BasedOnVersionId, version.DraftSchemaHash,
        version.SchemaFormatVersion, version.CreatedAtUtc, version.LastSavedAtUtc, version.ApprovedAtUtc,
        version.SnapshotId, Convert.ToBase64String(version.RowVersion));

    private async Task<FormVersionDetailDto> MapDetailAsync(
        FormVersion version,
        FormDefinition form,
        CancellationToken cancellationToken)
    {
        var actions = await BuildAllowedActionsAsync(version, form, cancellationToken);
        return new FormVersionDetailDto(
            version.Id, version.FormDefinitionId, version.VersionNumber, version.Status,
            FormVersionLabels.StatusAr(version.Status), version.BasedOnVersionId, version.DraftSchemaJson,
            version.DraftSchemaHash, version.SchemaFormatVersion, version.CreatedByUserId, version.UpdatedByUserId,
            version.CreatedAtUtc, version.LastSavedAtUtc, version.SubmittedForReviewAtUtc, version.ApprovedAtUtc,
            version.ApprovedByUserId, version.SnapshotId, Convert.ToBase64String(version.RowVersion), actions);
    }

    private async Task<List<string>> BuildAllowedActionsAsync(
        FormVersion version,
        FormDefinition form,
        CancellationToken cancellationToken)
    {
        var caps = await ResolveCapabilitiesAsync(form, cancellationToken);
        var actions = new List<string>();
        AddDesignActions(actions, version, caps.CanDesign);
        AddReviewActions(actions, version, caps.CanReview);
        AddApprovalActions(actions, version, caps.CanApprove);
        AddCloneActions(actions);
        AddSnapshotActions(actions, version);
        return actions;
    }

    private async Task<(bool CanDesign, bool CanReview, bool CanApprove)> ResolveCapabilitiesAsync(
        FormDefinition form,
        CancellationToken cancellationToken)
    {
        var canDesign = currentUser.HasPermission(PermissionCodes.FormsUpdateDraft)
            && await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Design, cancellationToken);
        var canReview = await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Review, cancellationToken);
        var canApprove = await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Approve, cancellationToken);
        return (canDesign, canReview, canApprove);
    }

    private void AddDesignActions(List<string> actions, FormVersion version, bool canDesign)
    {
        if (!canDesign)
        {
            return;
        }

        if (FormVersionStateMachine.IsEditable(version.Status))
        {
            actions.AddRange(["UpdateDraft", "SaveSchema", "Autosave", "Validate"]);
        }

        if ((version.Status is FormVersionStatus.Draft or FormVersionStatus.ChangesRequested)
            && currentUser.HasPermission(PermissionCodes.FormsSubmitForReview))
        {
            actions.Add("SubmitForReview");
        }

        if (version.Status == FormVersionStatus.Rejected)
        {
            actions.Add("Reopen");
        }
    }

    private void AddReviewActions(List<string> actions, FormVersion version, bool canReview)
    {
        if (version.Status != FormVersionStatus.InReview || !canReview)
        {
            return;
        }

        if (currentUser.HasPermission(PermissionCodes.FormsRequestChanges))
        {
            actions.Add("RequestChanges");
        }

        if (currentUser.HasPermission(PermissionCodes.FormsReject))
        {
            actions.Add("Reject");
        }
    }

    private void AddApprovalActions(List<string> actions, FormVersion version, bool canApprove)
    {
        if (version.Status == FormVersionStatus.InReview
            && canApprove
            && currentUser.HasPermission(PermissionCodes.FormsApprove))
        {
            actions.Add("ApproveAndLock");
        }
    }

    private void AddCloneActions(List<string> actions)
    {
        if (currentUser.HasPermission(PermissionCodes.FormsCloneVersion))
        {
            actions.Add("Clone");
        }
    }

    private void AddSnapshotActions(List<string> actions, FormVersion version)
    {
        if (version.SnapshotId is not null && currentUser.HasPermission(PermissionCodes.FormsViewVersionHistory))
        {
            actions.Add("ViewSnapshot");
        }
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

    private sealed record VersionTransitionContext(
        Guid FormId,
        Guid VersionId,
        FormVersionTransitionRequest Request,
        string Permission,
        FormAccessCapability Capability,
        FormVersionStatus Target,
        FormVersionReviewDecisionType DecisionType,
        string AuditAction,
        Func<FormDefinition, FormVersion, CancellationToken, Task> BeforeSave);
}
