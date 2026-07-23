namespace Baseera.Application.Forms.Campaigns;

using Baseera.Domain.Forms;

public static class FormCampaignAllowedActions
{
    public static IReadOnlyList<string> Build(
        FormCampaignStatus status,
        bool canManageCampaigns,
        bool hasPublishPermission,
        bool hasPublishCapabilityOnForm,
        bool canPauseCampaign,
        bool canCancelCampaign,
        bool canViewAssignments)
    {
        var actions = new List<string> { "view" };
        if (status == FormCampaignStatus.Draft && canManageCampaigns)
        {
            actions.Add("edit");
            actions.Add("preview");
        }

        if (status == FormCampaignStatus.Draft
            && hasPublishPermission
            && hasPublishCapabilityOnForm)
        {
            actions.Add("publish");
        }

        if (FormCampaignStateMachine.CanTransition(status, FormCampaignStatus.Paused)
            && canPauseCampaign)
        {
            actions.Add("pause");
        }

        if (status == FormCampaignStatus.Paused && canPauseCampaign)
        {
            actions.Add("resume");
        }

        if (FormCampaignStateMachine.CanTransition(status, FormCampaignStatus.Cancelled)
            && canCancelCampaign)
        {
            actions.Add("cancel");
        }

        if (FormCampaignStateMachine.CanTransition(status, FormCampaignStatus.Completed)
            && hasPublishPermission)
        {
            actions.Add("complete");
        }

        if (canManageCampaigns)
        {
            actions.Add("clone");
        }

        if (canViewAssignments)
        {
            actions.Add("viewAssignments");
        }

        return actions;
    }
}
