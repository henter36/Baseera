namespace Baseera.Application.Forms.Responses;

using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public enum FormResponseAttachmentOperation
{
    Upload = 0,
    List = 1,
    Download = 2,
    DeleteOrReplaceDraft = 3
}

public sealed record FormResponseAttachmentAccessDecision(
    bool Allowed,
    bool Exists,
    bool IsSensitive,
    ClassificationLevel Classification,
    bool IsOwnerRespondent,
    bool CanViewSensitive);

public interface IFormResponseAttachmentAccessResolver
{
    Task<FormResponseAttachmentAccessDecision> ResolveAsync(
        Guid responseId,
        FormResponseAttachmentOperation operation,
        CancellationToken cancellationToken = default);
}

public sealed class FormResponseAttachmentAccessResolver(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormResponseAccessCoordinator access) : IFormResponseAttachmentAccessResolver
{
    public async Task<FormResponseAttachmentAccessDecision> ResolveAsync(
        Guid responseId,
        FormResponseAttachmentOperation operation,
        CancellationToken cancellationToken = default)
    {
        var response = await db.FormResponses.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == responseId, cancellationToken);
        if (response is null)
        {
            return Denied(exists: false);
        }

        try
        {
            await access.EnsureFacilityInScopeAsync(response.FacilityId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return Denied(exists: true);
        }

        var campaign = await db.FormCampaigns.AsNoTracking()
            .FirstAsync(c => c.Id == response.CampaignId, cancellationToken);
        var form = await db.FormDefinitions.AsNoTracking()
            .FirstAsync(f => f.Id == campaign.FormDefinitionId, cancellationToken);
        var classification = form.Classification;
        var sensitive = FormAccessHelper.RequiresSensitive(classification);
        var isOwner = access.IsRespondentOwner(response);
        var canViewSensitive = access.CanViewSensitiveResponses();

        var allowed = operation switch
        {
            FormResponseAttachmentOperation.Upload or FormResponseAttachmentOperation.DeleteOrReplaceDraft
                => CanUploadOrReplace(response, isOwner),
            FormResponseAttachmentOperation.List or FormResponseAttachmentOperation.Download
                => CanRead(isOwner, sensitive, canViewSensitive),
            _ => false
        };

        return new FormResponseAttachmentAccessDecision(
            allowed,
            Exists: true,
            IsSensitive: sensitive,
            Classification: classification,
            IsOwnerRespondent: isOwner,
            CanViewSensitive: canViewSensitive);
    }

    private bool CanUploadOrReplace(FormResponse response, bool isOwner)
    {
        if (!currentUser.HasPermission(PermissionCodes.FormsRespond) || !isOwner)
        {
            return false;
        }

        return response.Status is FormResponseStatus.Draft or FormResponseStatus.Returned;
    }

    private bool CanRead(
        bool isOwner,
        bool sensitive,
        bool canViewSensitive)
    {
        if (isOwner)
        {
            return true;
        }

        if (!access.HasReviewerSidePermission())
        {
            return false;
        }

        if (sensitive && !canViewSensitive)
        {
            return false;
        }

        return true;
    }

    private static FormResponseAttachmentAccessDecision Denied(bool exists) =>
        new(false, exists, false, ClassificationLevel.Internal, false, false);
}
