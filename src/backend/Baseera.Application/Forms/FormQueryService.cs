namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormQueryService
{
    Task<PagedResult<FormListItemDto>> ListAsync(FormListQuery query, CancellationToken cancellationToken = default);
    Task<FormDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormReviewDecisionDto>> GetReviewDecisionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FormRetentionStatusDto> GetRetentionStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class FormQueryService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IFormRetentionPolicyService retention,
    IFormEffectiveAccessService effectiveAccess,
    IAuditService audit) : IFormQueryService
{
    private static readonly HashSet<string> SortAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAtUtc", "code", "nameAr", "status", "classification"
    };

    public async Task<PagedResult<FormListItemDto>> ListAsync(FormListQuery query, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsView);
        var canSensitive = FormAccessHelper.CanViewSensitive(currentUser);
        var q = await formScope.FilterQueryableAsync(db.FormDefinitions, cancellationToken);
        q = ApplyFilters(q, query);
        var total = await q.CountAsync(cancellationToken);
        q = ApplySort(q, query.SortBy, query.SortDesc);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var items = rows.Select(f =>
        {
            var redact = FormAccessHelper.RequiresSensitive(f.Classification) && !canSensitive;
            return new FormListItemDto(
                f.Id,
                f.Code,
                redact ? FormAccessHelper.RedactedTitle : f.NameAr,
                redact ? null : f.NameEn,
                redact ? null : Truncate(f.Description, 160),
                f.Status,
                FormDisplay.StatusAr(f.Status),
                f.Classification,
                f.ScopeType,
                f.RegionId,
                f.FacilityId,
                f.FacilityUnitId,
                f.OwnerDepartmentId,
                f.CreatedAtUtc,
                Convert.ToBase64String(f.RowVersion),
                redact);
        }).ToList();

        return new PagedResult<FormListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<FormDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsView);
        var form = await db.FormDefinitions.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (form is null || !formScope.CanAccess(form))
        {
            return null;
        }

        if (!await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.View, cancellationToken))
        {
            return null;
        }

        var canSensitive = FormAccessHelper.CanViewSensitive(currentUser);
        var redact = FormAccessHelper.RequiresSensitive(form.Classification) && !canSensitive;
        if (FormAccessHelper.RequiresSensitive(form.Classification) && canSensitive)
        {
            var policy = await db.FormGovernancePolicies.AsNoTracking().OrderBy(p => p.CreatedAtUtc).FirstAsync(cancellationToken);
            if (policy.AuditSensitiveViews)
            {
                await audit.WriteAsync(new AuditEntry
                {
                    Action = "FormSensitiveViewed",
                    Module = FormAccessHelper.ModuleName,
                    EntityType = nameof(FormDefinition),
                    EntityId = form.Id.ToString(),
                    IsSensitiveView = true,
                    NewValues = new { form.Code, form.Classification }
                }, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var userIds = new HashSet<Guid> { form.CreatedByUserId };
        if (form.UpdatedByUserId.HasValue)
        {
            userIds.Add(form.UpdatedByUserId.Value);
        }

        if (form.LastModifiedByUserId.HasValue)
        {
            userIds.Add(form.LastModifiedByUserId.Value);
        }

        if (form.ArchivedByUserId.HasValue)
        {
            userIds.Add(form.ArchivedByUserId.Value);
        }

        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameAr, cancellationToken);
        var allowedActions = await ResolveAllowedActionsAsync(form, cancellationToken);

        return new FormDetailDto(
            form.Id,
            form.Code,
            redact ? FormAccessHelper.RedactedTitle : form.NameAr,
            redact ? null : form.NameEn,
            redact ? FormAccessHelper.RedactedDescription : form.Description,
            form.Status,
            FormDisplay.StatusAr(form.Status),
            form.Classification,
            form.ScopeType,
            form.RegionId,
            form.FacilityId,
            form.FacilityUnitId,
            form.OwnerDepartmentId,
            form.CreatedByUserId,
            users.GetValueOrDefault(form.CreatedByUserId),
            form.UpdatedByUserId,
            form.UpdatedByUserId is Guid updatedId ? users.GetValueOrDefault(updatedId) : null,
            form.LastModifiedByUserId,
            form.LastModifiedByUserId is Guid modifiedId ? users.GetValueOrDefault(modifiedId) : null,
            form.SubmittedForReviewAtUtc,
            form.ApprovedAtUtc,
            form.ArchivedAtUtc,
            form.ArchivedByUserId,
            form.ArchivedByUserId is Guid archivedId ? users.GetValueOrDefault(archivedId) : null,
            form.CreatedAtUtc,
            form.UpdatedAtUtc,
            Convert.ToBase64String(form.RowVersion),
            redact,
            allowedActions);
    }

    public async Task<IReadOnlyList<FormReviewDecisionDto>> GetReviewDecisionsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsView);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, id, cancellationToken: cancellationToken);
        await EnsureViewCapabilityAsync(form, cancellationToken);

        var rows = await db.FormReviewDecisions
            .Where(d => d.FormDefinitionId == id)
            .OrderBy(d => d.ReviewedAtUtc)
            .ToListAsync(cancellationToken);
        var userIds = rows.Select(r => r.ReviewedByUserId).ToHashSet();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameAr, cancellationToken);

        return rows.Select(d => new FormReviewDecisionDto(
            d.Id,
            d.Decision,
            DecisionAr(d.Decision),
            d.Reason,
            d.ReviewedByUserId,
            users.GetValueOrDefault(d.ReviewedByUserId),
            d.ReviewedAtUtc,
            d.FromStatus,
            FormDisplay.StatusAr(d.FromStatus),
            d.ToStatus,
            FormDisplay.StatusAr(d.ToStatus),
            d.IsAdministrativeOverride)).ToList();
    }

    public Task<FormRetentionStatusDto> GetRetentionStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsView);
        return retention.GetRetentionStatusAsync(id, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ResolveAllowedActionsAsync(FormDefinition form, CancellationToken cancellationToken)
    {
        var actions = new List<string>();
        if (FormDefinitionStateMachine.IsEditable(form.Status) &&
            currentUser.HasPermission(PermissionCodes.FormsUpdateDraft) &&
            await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Design, cancellationToken))
        {
            actions.Add("UpdateDraft");
        }

        if (FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.InReview) &&
            currentUser.HasPermission(PermissionCodes.FormsSubmitForReview) &&
            await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Design, cancellationToken))
        {
            actions.Add("SubmitForReview");
        }

        if (form.Status == FormDefinitionStatus.InReview)
        {
            if (currentUser.HasPermission(PermissionCodes.FormsRequestChanges) &&
                FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.ChangesRequested) &&
                await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Review, cancellationToken))
            {
                actions.Add("RequestChanges");
            }

            if (currentUser.HasPermission(PermissionCodes.FormsApprove) &&
                FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.Approved) &&
                await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Approve, cancellationToken))
            {
                actions.Add("Approve");
            }

            if (currentUser.HasPermission(PermissionCodes.FormsReject) &&
                FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.Rejected) &&
                await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Review, cancellationToken))
            {
                actions.Add("Reject");
            }
        }

        if (FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.Archived) &&
            currentUser.HasPermission(PermissionCodes.FormsArchive) &&
            await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Archive, cancellationToken))
        {
            actions.Add("Archive");
        }

        if (form.Status == FormDefinitionStatus.Archived &&
            currentUser.HasPermission(PermissionCodes.FormsRestore) &&
            await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.Restore, cancellationToken))
        {
            actions.Add("Restore");
        }

        return actions;
    }

    private async Task EnsureViewCapabilityAsync(FormDefinition form, CancellationToken cancellationToken)
    {
        if (!await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.View, cancellationToken))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض هذا النموذج.");
        }
    }

    private static IQueryable<FormDefinition> ApplyFilters(IQueryable<FormDefinition> q, FormListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(f => f.Code.Contains(term) || f.NameAr.Contains(term) || (f.NameEn != null && f.NameEn.Contains(term)));
        }

        if (query.Status.HasValue)
        {
            q = q.Where(f => f.Status == query.Status.Value);
        }

        if (query.Classification.HasValue)
        {
            q = q.Where(f => f.Classification == query.Classification.Value);
        }

        if (query.RegionId.HasValue)
        {
            q = q.Where(f => f.RegionId == query.RegionId.Value);
        }

        if (query.FacilityId.HasValue)
        {
            q = q.Where(f => f.FacilityId == query.FacilityId.Value);
        }

        return q;
    }

    private static IQueryable<FormDefinition> ApplySort(IQueryable<FormDefinition> q, string? sortBy, bool sortDesc)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) || !SortAllowlist.Contains(sortBy)
            ? "createdAtUtc"
            : sortBy;

        return (key.ToLowerInvariant(), sortDesc) switch
        {
            ("code", true) => q.OrderByDescending(f => f.Code),
            ("code", false) => q.OrderBy(f => f.Code),
            ("namear", true) => q.OrderByDescending(f => f.NameAr),
            ("namear", false) => q.OrderBy(f => f.NameAr),
            ("status", true) => q.OrderByDescending(f => f.Status).ThenByDescending(f => f.CreatedAtUtc),
            ("status", false) => q.OrderBy(f => f.Status).ThenBy(f => f.CreatedAtUtc),
            ("classification", true) => q.OrderByDescending(f => f.Classification).ThenByDescending(f => f.CreatedAtUtc),
            ("classification", false) => q.OrderBy(f => f.Classification).ThenBy(f => f.CreatedAtUtc),
            (_, true) => q.OrderByDescending(f => f.CreatedAtUtc),
            _ => q.OrderBy(f => f.CreatedAtUtc)
        };
    }

    private static string DecisionAr(FormReviewDecisionType decision) => decision switch
    {
        FormReviewDecisionType.SubmitForReview => "إرسال للمراجعة",
        FormReviewDecisionType.RequestChanges => "طلب تعديلات",
        FormReviewDecisionType.Approve => "اعتماد",
        FormReviewDecisionType.Reject => "رفض",
        FormReviewDecisionType.Archive => "أرشفة",
        FormReviewDecisionType.Restore => "استعادة",
        _ => decision.ToString()
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
