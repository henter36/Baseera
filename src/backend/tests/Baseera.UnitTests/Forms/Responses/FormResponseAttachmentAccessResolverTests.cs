using Baseera.Application.Abstractions;
using Baseera.Application.Forms;
using Baseera.Application.Forms.Responses;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests.Forms.Responses;

public sealed class FormResponseAttachmentAccessResolverTests
{
    [Fact]
    public async Task Owner_can_upload_on_draft()
    {
        var (db, response, ownerId) = await SeedResponseAsync(FormResponseStatus.Draft, ClassificationLevel.Internal);
        var resolver = CreateResolver(db, ownerId, [PermissionCodes.FormsRespond],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        var decision = await resolver.ResolveAsync(response.Id, FormResponseAttachmentOperation.Upload);
        Assert.True(decision.Allowed);
        Assert.True(decision.IsOwnerRespondent);
    }

    [Fact]
    public async Task Upload_denied_for_submitted_status()
    {
        var (db, response, ownerId) = await SeedResponseAsync(FormResponseStatus.Submitted, ClassificationLevel.Internal);
        var resolver = CreateResolver(db, ownerId, [PermissionCodes.FormsRespond],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        var decision = await resolver.ResolveAsync(response.Id, FormResponseAttachmentOperation.Upload);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task Reviewer_without_respond_cannot_upload_even_in_scope()
    {
        var (db, response, _) = await SeedResponseAsync(FormResponseStatus.Draft, ClassificationLevel.Internal);
        var reviewerId = Guid.NewGuid();
        var resolver = CreateResolver(db, reviewerId, [PermissionCodes.FormsViewResponses],
            new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));

        var decision = await resolver.ResolveAsync(response.Id, FormResponseAttachmentOperation.Upload);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task Owner_can_download_sensitive_without_view_sensitive_permission()
    {
        var (db, response, ownerId) = await SeedResponseAsync(FormResponseStatus.Submitted, ClassificationLevel.Confidential);
        var resolver = CreateResolver(db, ownerId, [PermissionCodes.FormsRespond, PermissionCodes.AttachmentsDownload],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        var decision = await resolver.ResolveAsync(response.Id, FormResponseAttachmentOperation.Download);
        Assert.True(decision.Allowed);
        Assert.True(decision.IsSensitive);
    }

    [Fact]
    public async Task Reviewer_without_view_sensitive_denied_download_on_sensitive_form()
    {
        var (db, response, _) = await SeedResponseAsync(FormResponseStatus.Submitted, ClassificationLevel.Confidential);
        var resolver = CreateResolver(db, Guid.NewGuid(), [PermissionCodes.FormsViewResponses, PermissionCodes.AttachmentsDownload],
            new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));

        var decision = await resolver.ResolveAsync(response.Id, FormResponseAttachmentOperation.Download);
        Assert.False(decision.Allowed);
        Assert.True(decision.IsSensitive);
    }

    [Fact]
    public async Task Reviewer_with_view_sensitive_allowed_on_confidential_form()
    {
        var (db, response, _) = await SeedResponseAsync(FormResponseStatus.Submitted, ClassificationLevel.Confidential);
        var resolver = CreateResolver(db, Guid.NewGuid(),
            [PermissionCodes.FormsViewResponses, PermissionCodes.FormsViewSensitiveResponses, PermissionCodes.AttachmentsDownload],
            new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));

        var decision = await resolver.ResolveAsync(response.Id, FormResponseAttachmentOperation.Download);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task Out_of_scope_facility_user_denied()
    {
        var (db, response, _) = await SeedResponseAsync(FormResponseStatus.Draft, ClassificationLevel.Internal);
        var resolver = CreateResolver(db, Guid.NewGuid(), [PermissionCodes.FormsRespond, PermissionCodes.AttachmentsDownload],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, null));

        var decision = await resolver.ResolveAsync(response.Id, FormResponseAttachmentOperation.List);
        Assert.False(decision.Allowed);
        Assert.True(decision.Exists);
    }

    [Fact]
    public async Task Missing_response_not_exists()
    {
        await using var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var resolver = CreateResolver(db, Guid.NewGuid(), [PermissionCodes.FormsRespond],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));

        var decision = await resolver.ResolveAsync(Guid.NewGuid(), FormResponseAttachmentOperation.Download);
        Assert.False(decision.Exists);
        Assert.False(decision.Allowed);
    }

    private static FormResponseAttachmentAccessResolver CreateResolver(
        BaseeraDbContext db,
        Guid userId,
        string[] permissions,
        UserScopeSnapshot scope)
    {
        var current = FormTestFixtures.CurrentUser(userId, permissions, scope);
        var orgScope = new OrganizationalScopeService(current, db);
        var effective = new FormEffectiveAccessService(db, current);
        var access = new FormResponseAccessCoordinator(current, orgScope, effective, db);
        return new FormResponseAttachmentAccessResolver(db, current, access);
    }

    private static async Task<(BaseeraDbContext Db, FormResponse Response, Guid OwnerId)> SeedResponseAsync(
        FormResponseStatus status,
        ClassificationLevel classification)
    {
        var db = FormTestFixtures.CreateDb();
        FormTestFixtures.SeedOrgGraph(db);
        var owner = FormTestFixtures.AddUser(db, "مالك");
        var form = FormTestFixtures.NewForm(owner.Id, classification: classification);
        var campaign = new FormCampaign
        {
            FormDefinitionId = form.Id,
            FormVersionId = Guid.NewGuid(),
            Code = "CAMP",
            NameAr = "حملة",
            Status = FormCampaignStatus.Active,
            CreatedByUserId = owner.Id
        };
        var response = new FormResponse
        {
            AssignmentId = Guid.NewGuid(),
            CampaignId = campaign.Id,
            CycleId = Guid.NewGuid(),
            FacilityId = SeedIds.FacilityA1,
            FormSchemaSnapshotId = Guid.NewGuid(),
            SchemaHash = "hash",
            Status = status,
            LastSavedByUserId = owner.Id,
            SubmittedByUserId = status == FormResponseStatus.Draft ? null : owner.Id,
            DraftAnswersJson = "{}"
        };
        db.FormDefinitions.Add(form);
        db.FormCampaigns.Add(campaign);
        db.FormResponses.Add(response);
        await db.SaveChangesAsync();
        return (db, response, owner.Id);
    }
}
