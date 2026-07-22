namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormGovernanceService
{
    Task<FormGovernancePolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<FormGovernancePolicyDto> UpdatePolicyAsync(UpdateFormGovernancePolicyRequest request, CancellationToken cancellationToken = default);
}

public sealed class FormGovernanceService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IFormGovernanceService
{
    public async Task<FormGovernancePolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageGovernance);
        var policy = await LoadPolicyAsync(cancellationToken);
        return ToDto(policy);
    }

    public async Task<FormGovernancePolicyDto> UpdatePolicyAsync(
        UpdateFormGovernancePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        FormAccessHelper.EnsurePermission(currentUser, PermissionCodes.FormsManageGovernance);
        var policy = await LoadPolicyAsync(cancellationToken);
        FormAccessHelper.EnsureRowVersion(policy.RowVersion, request.RowVersion);

        var old = new
        {
            policy.RequireReviewBeforeApproval,
            policy.RequireSeparationOfDuties,
            policy.AllowDesignerToReviewOwnForm,
            policy.AllowReviewerToApproveOwnReview,
            policy.AllowApproverToPublish,
            policy.DefaultRetentionDays,
            policy.SensitiveRetentionDays,
            policy.MinimumRetentionDays,
            policy.AuditSensitiveViews,
            policy.AuditExports,
            policy.RequireReasonForArchive
        };

        policy.RequireReviewBeforeApproval = request.RequireReviewBeforeApproval;
        policy.RequireSeparationOfDuties = request.RequireSeparationOfDuties;
        policy.AllowDesignerToReviewOwnForm = request.AllowDesignerToReviewOwnForm;
        policy.AllowReviewerToApproveOwnReview = request.AllowReviewerToApproveOwnReview;
        policy.AllowApproverToPublish = request.AllowApproverToPublish;
        policy.DefaultRetentionDays = request.DefaultRetentionDays;
        policy.SensitiveRetentionDays = request.SensitiveRetentionDays;
        policy.MinimumRetentionDays = request.MinimumRetentionDays;
        policy.AuditSensitiveViews = request.AuditSensitiveViews;
        policy.AuditExports = request.AuditExports;
        policy.RequireReasonForArchive = request.RequireReasonForArchive;
        policy.UpdatedAtUtc = DateTimeOffset.UtcNow;
        policy.UpdatedBy = currentUser.ExternalSubject;
        policy.UpdatedByUserId = currentUser.UserId;
        db.Update(policy);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormGovernancePolicyUpdated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormGovernancePolicy),
            EntityId = policy.Id.ToString(),
            OldValues = old,
            NewValues = new
            {
                policy.RequireReviewBeforeApproval,
                policy.RequireSeparationOfDuties,
                policy.AllowDesignerToReviewOwnForm,
                policy.AllowReviewerToApproveOwnReview,
                policy.AllowApproverToPublish,
                policy.DefaultRetentionDays,
                policy.SensitiveRetentionDays,
                policy.MinimumRetentionDays,
                policy.AuditSensitiveViews,
                policy.AuditExports,
                policy.RequireReasonForArchive
            }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(policy);
    }

    private async Task<FormGovernancePolicy> LoadPolicyAsync(CancellationToken cancellationToken) =>
        await db.FormGovernancePolicies.OrderBy(p => p.CreatedAtUtc).FirstAsync(cancellationToken);

    private static FormGovernancePolicyDto ToDto(FormGovernancePolicy policy) =>
        new(
            policy.Id,
            policy.RequireReviewBeforeApproval,
            policy.RequireSeparationOfDuties,
            policy.AllowDesignerToReviewOwnForm,
            policy.AllowReviewerToApproveOwnReview,
            policy.AllowApproverToPublish,
            policy.DefaultRetentionDays,
            policy.SensitiveRetentionDays,
            policy.MinimumRetentionDays,
            policy.AuditSensitiveViews,
            policy.AuditExports,
            policy.RequireReasonForArchive,
            Convert.ToBase64String(policy.RowVersion));
}
