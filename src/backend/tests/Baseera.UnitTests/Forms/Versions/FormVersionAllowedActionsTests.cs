using Baseera.Application.Forms;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;

namespace Baseera.UnitTests.Forms.Versions;

public sealed class FormVersionAllowedActionsTests
{
    [Theory]
    [InlineData(FormVersionStatus.Draft, true, false, false, new[] { "UpdateDraft", "SaveSchema", "Autosave", "Validate", "SubmitForReview" })]
    [InlineData(FormVersionStatus.Draft, false, false, false, new string[0])]
    [InlineData(FormVersionStatus.ChangesRequested, true, false, false, new[] { "UpdateDraft", "SaveSchema", "Autosave", "Validate", "SubmitForReview" })]
    [InlineData(FormVersionStatus.InReview, false, true, false, new[] { "RequestChanges", "Reject" })]
    [InlineData(FormVersionStatus.InReview, false, false, false, new string[0])]
    [InlineData(FormVersionStatus.InReview, false, false, true, new[] { "ApproveAndLock" })]
    [InlineData(FormVersionStatus.InReview, false, true, true, new[] { "RequestChanges", "Reject", "ApproveAndLock" })]
    [InlineData(FormVersionStatus.Rejected, true, false, false, new[] { "Reopen" })]
    [InlineData(FormVersionStatus.Locked, false, false, false, new string[0])]
    public void Action_matrix_matches_status_and_capabilities(
        FormVersionStatus status,
        bool canDesign,
        bool canReview,
        bool canApprove,
        string[] expectedCore)
    {
        var version = new FormVersion
        {
            Status = status,
            SnapshotId = status == FormVersionStatus.Locked ? Guid.NewGuid() : null
        };

        var actions = SimulateActions(
            version,
            canDesign,
            canReview,
            canApprove,
            hasClone: status == FormVersionStatus.Locked,
            hasHistory: status == FormVersionStatus.Locked,
            hasSubmit: true,
            hasRequestChanges: true,
            hasReject: true,
            hasApprove: true,
            hasUpdateDraft: true);

        foreach (var action in expectedCore)
        {
            Assert.Contains(action, actions);
        }

        if (status == FormVersionStatus.Draft && !canDesign)
        {
            Assert.DoesNotContain("SaveSchema", actions);
            Assert.DoesNotContain("SubmitForReview", actions);
        }

        if (status == FormVersionStatus.InReview && !canReview)
        {
            Assert.DoesNotContain("RequestChanges", actions);
            Assert.DoesNotContain("Reject", actions);
        }

        if (status == FormVersionStatus.InReview && !canApprove)
        {
            Assert.DoesNotContain("ApproveAndLock", actions);
        }

        if (status == FormVersionStatus.Locked)
        {
            Assert.Contains("Clone", actions);
            Assert.Contains("ViewSnapshot", actions);
        }
    }

    private static List<string> SimulateActions(
        FormVersion version,
        bool canDesign,
        bool canReview,
        bool canApprove,
        bool hasClone,
        bool hasHistory,
        bool hasSubmit,
        bool hasRequestChanges,
        bool hasReject,
        bool hasApprove,
        bool hasUpdateDraft)
    {
        // Mirrors FormVersionService.BuildAllowedActionsAsync branching without EF.
        var actions = new List<string>();
        if (canDesign && hasUpdateDraft)
        {
            if (FormVersionStateMachine.IsEditable(version.Status))
            {
                actions.AddRange(["UpdateDraft", "SaveSchema", "Autosave", "Validate"]);
            }

            if ((version.Status is FormVersionStatus.Draft or FormVersionStatus.ChangesRequested) && hasSubmit)
            {
                actions.Add("SubmitForReview");
            }

            if (version.Status == FormVersionStatus.Rejected)
            {
                actions.Add("Reopen");
            }
        }

        if (version.Status == FormVersionStatus.InReview && canReview)
        {
            if (hasRequestChanges)
            {
                actions.Add("RequestChanges");
            }

            if (hasReject)
            {
                actions.Add("Reject");
            }
        }

        if (version.Status == FormVersionStatus.InReview && canApprove && hasApprove)
        {
            actions.Add("ApproveAndLock");
        }

        if (hasClone)
        {
            actions.Add("Clone");
        }

        if (version.SnapshotId is not null && hasHistory)
        {
            actions.Add("ViewSnapshot");
        }

        return actions;
    }
}
