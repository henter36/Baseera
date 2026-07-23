namespace Baseera.Application.Forms.Responses;

using Baseera.Domain.Forms;

public static class FormResponsePolicyRules
{
    public static void Validate(FormCampaignResponsePolicyRequest policy)
    {
        var levels = policy.RequiredApprovalLevels;
        switch (policy.ReviewMode)
        {
            case FormReviewMode.None when levels != 0:
                throw new ArgumentException("وضع بدون مراجعة يتطلب صفر مستويات اعتماد.");
            case FormReviewMode.SingleLevel when levels != 1:
                throw new ArgumentException("المراجعة بمستوى واحد تتطلب مستوى اعتماد واحدًا.");
            case FormReviewMode.MultiLevel when levels is < 2 or > 5:
                throw new ArgumentException("المراجعة متعددة المستويات تسمح من مستويين إلى خمسة.");
        }

        if (policy.CompletionBasis == FormCompletionBasis.Approved && policy.ReviewMode == FormReviewMode.None)
        {
            throw new ArgumentException("الإكمال بالاعتماد يتطلب وضع مراجعة.");
        }
    }

    public static FormCampaignResponsePolicy CreateDefault(Guid campaignId) => new()
    {
        CampaignId = campaignId,
        CompletionBasis = FormCompletionBasis.Submitted,
        ReviewMode = FormReviewMode.None,
        RequiredApprovalLevels = 0,
        AllowLateSubmission = true,
        AllowResubmissionAfterReturn = true,
        RequireSubmissionAcknowledgement = false,
        RequireSeparationOfDuties = true
    };

    public static void Apply(FormCampaignResponsePolicy entity, FormCampaignResponsePolicyRequest request)
    {
        Validate(request);
        entity.CompletionBasis = request.CompletionBasis;
        entity.ReviewMode = request.ReviewMode;
        entity.RequiredApprovalLevels = request.RequiredApprovalLevels;
        entity.AllowLateSubmission = request.AllowLateSubmission;
        entity.AllowResubmissionAfterReturn = request.AllowResubmissionAfterReturn;
        entity.RequireSubmissionAcknowledgement = request.RequireSubmissionAcknowledgement;
        entity.RequireSeparationOfDuties = request.RequireSeparationOfDuties;
    }

    public static FormCampaignResponsePolicyDto ToDto(FormCampaignResponsePolicy policy) =>
        new(
            policy.CompletionBasis,
            policy.ReviewMode,
            policy.RequiredApprovalLevels,
            policy.AllowLateSubmission,
            policy.AllowResubmissionAfterReturn,
            policy.RequireSubmissionAcknowledgement,
            policy.RequireSeparationOfDuties);
}
