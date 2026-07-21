namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public interface IFormRetentionPolicyService
{
    Task<FormRetentionStatusDto> GetRetentionStatusAsync(Guid formDefinitionId, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleForArchiveAsync(FormDefinition form, CancellationToken cancellationToken = default);
    void RejectHardDelete();
}

public sealed class FormRetentionPolicyService(
    IBaseeraDbContext db,
    IFormScopeService formScope) : IFormRetentionPolicyService
{
    public async Task<FormRetentionStatusDto> GetRetentionStatusAsync(
        Guid formDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var form = await db.FormDefinitions.FirstOrDefaultAsync(f => f.Id == formDefinitionId, cancellationToken)
            ?? throw new KeyNotFoundException("النموذج غير موجود.");
        if (!formScope.CanAccess(form))
        {
            throw new KeyNotFoundException("النموذج غير موجود.");
        }

        return await BuildStatusAsync(form, cancellationToken);
    }

    public async Task<bool> IsEligibleForArchiveAsync(FormDefinition form, CancellationToken cancellationToken = default)
    {
        var status = await BuildStatusAsync(form, cancellationToken);
        return status.IsEligibleForArchive;
    }

    public void RejectHardDelete() =>
        throw new InvalidOperationException("الحذف النهائي للنماذج غير مسموح. استخدم الأرشفة وفق سياسة الاحتفاظ.");

    private async Task<FormRetentionStatusDto> BuildStatusAsync(FormDefinition form, CancellationToken cancellationToken)
    {
        var policy = await db.FormGovernancePolicies.AsNoTracking().OrderBy(p => p.CreatedAtUtc).FirstAsync(cancellationToken);
        var anchor = ResolveAnchorUtc(form);
        var retentionDays = ResolveRetentionDays(form.Classification, policy);
        DateTimeOffset? expiresAt = anchor?.AddDays(retentionDays);
        var now = DateTimeOffset.UtcNow;
        var isExpired = expiresAt.HasValue && now >= expiresAt.Value;
        var isApplicable = anchor.HasValue &&
                           form.Status is FormDefinitionStatus.Approved or FormDefinitionStatus.Archived;

        return new FormRetentionStatusDto(
            form.Id,
            isApplicable,
            anchor,
            retentionDays,
            expiresAt,
            isExpired,
            isApplicable && isExpired && form.Status == FormDefinitionStatus.Approved);
    }

    private static DateTimeOffset? ResolveAnchorUtc(FormDefinition form) =>
        form.Status switch
        {
            FormDefinitionStatus.Archived => form.ArchivedAtUtc ?? form.ApprovedAtUtc,
            FormDefinitionStatus.Approved => form.ApprovedAtUtc,
            _ => null
        };

    private static int ResolveRetentionDays(ClassificationLevel classification, FormGovernancePolicy policy)
    {
        var days = FormAccessHelper.RequiresSensitive(classification)
            ? policy.SensitiveRetentionDays
            : policy.DefaultRetentionDays;
        return Math.Max(days, policy.MinimumRetentionDays);
    }
}
