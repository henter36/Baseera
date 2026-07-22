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
    private sealed record AllowedActionRule(
        string Action,
        string Permission,
        FormAccessCapability Capability,
        Func<FormDefinition, bool> IsStateAllowed);

    private static readonly AllowedActionRule[] AllowedActionRules =
    [
        new(
            "UpdateDraft",
            PermissionCodes.FormsUpdateDraft,
            FormAccessCapability.Design,
            form => FormDefinitionStateMachine.IsEditable(form.Status)),
        new(
            "SubmitForReview",
            PermissionCodes.FormsSubmitForReview,
            FormAccessCapability.Design,
            form => FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.InReview)),
        new(
            "RequestChanges",
            PermissionCodes.FormsRequestChanges,
            FormAccessCapability.Review,
            form => form.Status == FormDefinitionStatus.InReview &&
                    FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.ChangesRequested)),
        new(
            "Approve",
            PermissionCodes.FormsApprove,
            FormAccessCapability.Approve,
            form => form.Status == FormDefinitionStatus.InReview &&
                    FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.Approved)),
        new(
            "Reject",
            PermissionCodes.FormsReject,
            FormAccessCapability.Review,
            form => form.Status == FormDefinitionStatus.InReview &&
                    FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.Rejected)),
        new(
            "Archive",
            PermissionCodes.FormsArchive,
            FormAccessCapability.Archive,
            form => FormDefinitionStateMachine.CanTransition(form.Status, FormDefinitionStatus.Archived)),
        new(
            "Restore",
            PermissionCodes.FormsRestore,
            FormAccessCapability.Restore,
            form => form.Status == FormDefinitionStatus.Archived)
    ];

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

        return new PagedResult<FormListItemDto>
        {
            Items = rows.Select(form => MapListItem(form, canSensitive)).ToList(),
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
        await MaybeAuditSensitiveViewAsync(form, canSensitive, cancellationToken);

        var userIds = CollectRelatedUserIds(form);
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayNameAr, cancellationToken);
        var allowedActions = await ResolveAllowedActionsAsync(form, cancellationToken);
        return MapDetail(form, users, redact, allowedActions);
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
        foreach (var rule in AllowedActionRules)
        {
            await TryAddAllowedActionAsync(actions, form, rule, cancellationToken);
        }

        return actions;
    }

    private async Task TryAddAllowedActionAsync(
        List<string> actions,
        FormDefinition form,
        AllowedActionRule rule,
        CancellationToken cancellationToken)
    {
        if (!rule.IsStateAllowed(form) || !currentUser.HasPermission(rule.Permission))
        {
            return;
        }

        if (await effectiveAccess.HasCapabilityAsync(form, rule.Capability, cancellationToken))
        {
            actions.Add(rule.Action);
        }
    }

    private async Task MaybeAuditSensitiveViewAsync(
        FormDefinition form,
        bool canSensitive,
        CancellationToken cancellationToken)
    {
        if (!FormAccessHelper.RequiresSensitive(form.Classification) || !canSensitive)
        {
            return;
        }

        var policy = await db.FormGovernancePolicies.AsNoTracking().OrderBy(p => p.CreatedAtUtc).FirstAsync(cancellationToken);
        if (!policy.AuditSensitiveViews)
        {
            return;
        }

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

    private async Task EnsureViewCapabilityAsync(FormDefinition form, CancellationToken cancellationToken)
    {
        if (!await effectiveAccess.HasCapabilityAsync(form, FormAccessCapability.View, cancellationToken))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية عرض هذا النموذج.");
        }
    }

    private static FormListItemDto MapListItem(FormDefinition form, bool canViewSensitive)
    {
        var redact = FormAccessHelper.RequiresSensitive(form.Classification) && !canViewSensitive;
        return new FormListItemDto(
            form.Id,
            form.Code,
            redact ? FormAccessHelper.RedactedTitle : form.NameAr,
            redact ? null : form.NameEn,
            redact ? null : Truncate(form.Description, 160),
            form.Status,
            FormDisplay.StatusAr(form.Status),
            form.Classification,
            form.ScopeType,
            form.RegionId,
            form.FacilityId,
            form.FacilityUnitId,
            form.OwnerDepartmentId,
            form.CreatedAtUtc,
            Convert.ToBase64String(form.RowVersion),
            redact);
    }

    private static FormDetailDto MapDetail(
        FormDefinition form,
        IReadOnlyDictionary<Guid, string> users,
        bool redact,
        IReadOnlyList<string> allowedActions) =>
        new(
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

    private static HashSet<Guid> CollectRelatedUserIds(FormDefinition form)
    {
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

        return userIds;
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
