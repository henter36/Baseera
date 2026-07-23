using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class FormResponseHardeningIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static int _seq;

    public FormResponseHardeningIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Respond_only_cannot_get_review_detail()
    {
        await SeedWorkflowUsersAsync();
        await SeedRespondOnlyUserAsync("rsp-h-respond-only");
        var submitted = await SubmitSingleResponseAsync(respondentSubject: "rsp-h-respond-only");
        var respondent = _factory.CreateAuthenticatedClient("rsp-h-respond-only");

        var review = await respondent.GetAsync($"/api/v1/form-responses/{submitted.ResponseId}/review");
        Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Respondent_cannot_see_internal_review_comments()
    {
        await SeedWorkflowUsersAsync();
        var submitted = await SubmitSingleResponseAsync();
        var reviewer = _factory.CreateAuthenticatedClient("rsp-h-reviewer");
        var respondent = _factory.CreateAuthenticatedClient("rsp-h-facility");

        var returnResp = await reviewer.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/return", new
        {
            reason = "يلزم توضيح",
            newDueAtUtc = DateTimeOffset.UtcNow.AddDays(2),
            comments = new[]
            {
                new { fieldKey = "q1", body = "ظاهر للمستجيب", isVisibleToRespondent = true },
                new { fieldKey = "q1", body = "داخلي فقط", isVisibleToRespondent = false }
            },
            rowVersion = submitted.RowVersion
        });
        Assert.True(returnResp.IsSuccessStatusCode, await returnResp.Content.ReadAsStringAsync());

        var detail = await respondent.GetFromJsonAsync<ResponseDetail>(
            $"/api/v1/form-assignments/{submitted.AssignmentId}/response", JsonOptions);
        Assert.NotNull(detail);
        Assert.Contains(detail!.VisibleComments, c => c.Body.Contains("ظاهر"));
        Assert.DoesNotContain(detail.VisibleComments, c => c.Body.Contains("داخلي"));
    }

    [IntegrationConnectionFact]
    public async Task Return_rejects_null_empty_and_whitespace_comment_bodies()
    {
        await SeedWorkflowUsersAsync();
        var reviewer = _factory.CreateAuthenticatedClient("rsp-h-reviewer");

        foreach (var body in new object?[] { null, "", "   " })
        {
            var submitted = await SubmitSingleResponseAsync();
            var response = await reviewer.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/return", new
            {
                reason = "يلزم توضيح",
                newDueAtUtc = DateTimeOffset.UtcNow.AddDays(2),
                comments = new[]
                {
                    new { fieldKey = "q1", body, isVisibleToRespondent = true }
                },
                rowVersion = submitted.RowVersion
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            Assert.Equal(0, await db.FormResponseReviewComments.CountAsync(c => c.ResponseId == submitted.ResponseId));
        }
    }

    [IntegrationConnectionFact]
    public async Task Return_trims_comment_body_and_rejects_partial_invalid_batches()
    {
        await SeedWorkflowUsersAsync();
        var reviewer = _factory.CreateAuthenticatedClient("rsp-h-reviewer");
        var submitted = await SubmitSingleResponseAsync();

        var invalidBatch = await reviewer.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/return", new
        {
            reason = "يلزم توضيح",
            newDueAtUtc = DateTimeOffset.UtcNow.AddDays(2),
            comments = new[]
            {
                new { fieldKey = "q1", body = "تعليق صالح", isVisibleToRespondent = true },
                new { fieldKey = (string?)null, body = "   ", isVisibleToRespondent = false }
            },
            rowVersion = submitted.RowVersion
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidBatch.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            Assert.Equal(0, await db.FormResponseReviewComments.CountAsync(c => c.ResponseId == submitted.ResponseId));
        }

        var valid = await reviewer.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/return", new
        {
            reason = "يلزم توضيح",
            newDueAtUtc = DateTimeOffset.UtcNow.AddDays(2),
            comments = new[]
            {
                new { fieldKey = "q1", body = "  تعليق حقلي  ", isVisibleToRespondent = true },
                new { fieldKey = (string?)null, body = "  تعليق عام  ", isVisibleToRespondent = false }
            },
            rowVersion = submitted.RowVersion
        });
        Assert.True(valid.IsSuccessStatusCode, await valid.Content.ReadAsStringAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var comments = await db.FormResponseReviewComments.AsNoTracking()
                .Where(c => c.ResponseId == submitted.ResponseId)
                .OrderBy(c => c.CreatedAtUtc)
                .ToListAsync();
            Assert.Equal(2, comments.Count);
            Assert.Contains(comments, c => c.Body == "تعليق حقلي" && c.FieldKey == "q1" && c.IsVisibleToRespondent);
            Assert.Contains(comments, c => c.Body == "تعليق عام" && c.FieldKey == null && !c.IsVisibleToRespondent);
        }
    }

    [IntegrationConnectionFact]
    public async Task ViewResponses_in_scope_can_open_review_detail()
    {
        await SeedWorkflowUsersAsync();
        var submitted = await SubmitSingleResponseAsync();
        var viewer = _factory.CreateAuthenticatedClient("rsp-h-viewer");

        var review = await viewer.GetAsync($"/api/v1/form-responses/{submitted.ResponseId}/review");
        Assert.True(review.IsSuccessStatusCode, await review.Content.ReadAsStringAsync());
    }

    [IntegrationConnectionFact]
    public async Task ReviewResponses_in_scope_can_open_review_detail()
    {
        await SeedWorkflowUsersAsync();
        var submitted = await SubmitSingleResponseAsync();
        var reviewerOnly = _factory.CreateAuthenticatedClient("rsp-h-review-only");

        var review = await reviewerOnly.GetAsync($"/api/v1/form-responses/{submitted.ResponseId}/review");
        Assert.True(review.IsSuccessStatusCode, await review.Content.ReadAsStringAsync());
    }

    [IntegrationConnectionFact]
    public async Task Out_of_scope_review_detail_returns_404()
    {
        await SeedWorkflowUsersAsync();
        var submitted = await SubmitSingleResponseAsync();
        var outsider = _factory.CreateAuthenticatedClient("rsp-h-outsider");

        var review = await outsider.GetAsync($"/api/v1/form-responses/{submitted.ResponseId}/review");
        Assert.Equal(HttpStatusCode.NotFound, review.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Deny_grant_blocks_review_detail_despite_view_permission()
    {
        await SeedWorkflowUsersAsync();
        await _factory.SeedUserAsync("rsp-h-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var submitted = await SubmitSingleResponseAsync();
        var admin = _factory.CreateAuthenticatedClient("rsp-h-admin");
        var viewer = _factory.CreateAuthenticatedClient("rsp-h-viewer");

        Guid viewerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            viewerId = await db.Users.Where(u => u.ExternalSubject == "rsp-h-viewer").Select(u => u.Id).FirstAsync();
        }

        var grant = await admin.PostAsJsonAsync($"/api/v1/forms/{submitted.FormId}/access-grants", new
        {
            principalType = FormAccessGrantPrincipalType.User,
            principalId = viewerId,
            capability = FormAccessCapability.ViewResponses,
            effect = FormAccessGrantEffect.Deny,
            scopeType = (ScopeType?)null,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            validFromUtc = (DateTimeOffset?)null,
            validToUtc = (DateTimeOffset?)null,
            reason = "منع عرض الردود"
        });
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);

        var review = await viewer.GetAsync($"/api/v1/form-responses/{submitted.ResponseId}/review");
        Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Concurrent_dual_approve_one_conflict_one_success()
    {
        await SeedWorkflowUsersAsync();
        var submitted = await SubmitSingleResponseAsync(requiredApprovalLevels: 2);
        var approver = _factory.CreateAuthenticatedClient("rsp-h-approver");

        var detail = await approver.GetFromJsonAsync<ReviewDetail>(
            $"/api/v1/form-responses/{submitted.ResponseId}/review", JsonOptions);
        Assert.NotNull(detail);

        var payload = new { reason = "اعتماد", rowVersion = detail!.Workspace.RowVersion };
        var first = approver.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/approve", payload);
        var second = approver.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/approve", payload);
        await Task.WhenAll(first, second);

        var statuses = new[] { first.Result.StatusCode, second.Result.StatusCode };
        Assert.Contains(HttpStatusCode.NoContent, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);

        var conflict = first.Result.StatusCode == HttpStatusCode.Conflict ? first.Result : second.Result;
        var body = await conflict.Content.ReadAsStringAsync();
        Assert.Contains("APPROVAL_LEVEL_ALREADY_DECIDED", body, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var response = await db.FormResponses.AsNoTracking().SingleAsync(r => r.Id == submitted.ResponseId);
        var approveCount = await db.FormResponseReviewDecisions.CountAsync(d =>
            d.ResponseId == submitted.ResponseId && d.Decision == FormResponseReviewDecisionType.Approve);
        Assert.Equal(1, approveCount);
        Assert.Equal(2, response.CurrentReviewLevel);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_pagination_is_stable_and_scoped()
    {
        await SeedWorkflowUsersAsync();
        var respondent = _factory.CreateAuthenticatedClient("rsp-h-facility");
        var publisher = _factory.CreateAuthenticatedClient("rsp-h-publisher");
        var designer = _factory.CreateAuthenticatedClient("rsp-h-designer");
        var approver = _factory.CreateAuthenticatedClient("rsp-h-form-approver");
        var marker = $"PG{Guid.NewGuid():N}"[..12];

        for (var i = 0; i < 7; i++)
        {
            var (formId, versionId) = await CreateLockedFormAsync(designer, approver, $"{marker}{i:D2}");
            await CreateAndPublishCampaignAsync(designer, publisher, formId, versionId, SeedIds.FacilityA1, campaignName: marker);
        }

        var (outFormId, outVersionId) = await CreateLockedFormAsync(designer, approver, "OUT");
        await CreateAndPublishCampaignAsync(designer, publisher, outFormId, outVersionId, SeedIds.FacilityB1, campaignName: marker);

        var qs = $"search={Uri.EscapeDataString(marker)}&pageSize=2";
        var page1 = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?page=1&{qs}", JsonOptions);
        var page2 = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?page=2&{qs}", JsonOptions);
        var page3 = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?page=3&{qs}", JsonOptions);
        var page4 = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?page=4&{qs}", JsonOptions);

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.NotNull(page3);
        Assert.NotNull(page4);
        Assert.Equal(7, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2!.Items.Count);
        Assert.Equal(2, page3!.Items.Count);
        Assert.Equal(1, page4!.Items.Count);
        Assert.NotEqual(page1.Items[0].AssignmentId, page2.Items[0].AssignmentId);
        Assert.All(page1.Items, i => Assert.Equal(SeedIds.FacilityA1, i.FacilityId));

        var overduePage = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?page=1&pageSize=50&workStatus=overdue&search={Uri.EscapeDataString(marker)}", JsonOptions);
        Assert.NotNull(overduePage);
        Assert.True(overduePage!.TotalCount <= page1.TotalCount);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_work_status_filters_translate_to_sql()
    {
        await SeedWorkflowUsersAsync();
        var respondent = _factory.CreateAuthenticatedClient("rsp-h-facility");
        var publisher = _factory.CreateAuthenticatedClient("rsp-h-publisher");
        var designer = _factory.CreateAuthenticatedClient("rsp-h-designer");
        var formApprover = _factory.CreateAuthenticatedClient("rsp-h-form-approver");
        var reviewer = _factory.CreateAuthenticatedClient("rsp-h-reviewer");
        var marker = $"WS{Guid.NewGuid():N}"[..10];
        var search = Uri.EscapeDataString(marker);

        // Completed (Submitted basis): submit alone is complete.
        var (submittedFormId, submittedVersionId) = await CreateLockedFormAsync(designer, formApprover, $"{marker}S");
        var submittedCampaign = await CreateAndPublishCampaignAsync(
            designer, publisher, submittedFormId, submittedVersionId, SeedIds.FacilityA1,
            requiredApprovalLevels: 0, campaignName: marker,
            completionBasis: FormCompletionBasis.Submitted, reviewMode: FormReviewMode.None);
        var submittedAssignmentId = await GetFirstAssignmentIdAsync(publisher, submittedCampaign.Id);
        var submitSubmitted = await respondent.PostAsJsonAsync(
            $"/api/v1/form-assignments/{submittedAssignmentId}/response/submit",
            new
            {
                answers = new { q1 = "مكتمل-إرسال" },
                clientMutationId = Guid.NewGuid(),
                expectedDraftVersion = 0,
                rowVersion = (string?)null,
                acknowledged = true,
                acknowledgementText = "أقر"
            });
        Assert.True(submitSubmitted.IsSuccessStatusCode, await submitSubmitted.Content.ReadAsStringAsync());

        // Completed (Approved basis): submit then approve.
        var (approvedFormId, approvedVersionId) = await CreateLockedFormAsync(designer, formApprover, $"{marker}A");
        var approvedCampaign = await CreateAndPublishCampaignAsync(
            designer, publisher, approvedFormId, approvedVersionId, SeedIds.FacilityA1,
            requiredApprovalLevels: 1, campaignName: marker,
            completionBasis: FormCompletionBasis.Approved, reviewMode: FormReviewMode.SingleLevel);
        var approvedAssignmentId = await GetFirstAssignmentIdAsync(publisher, approvedCampaign.Id);
        var submitApproved = await respondent.PostAsJsonAsync(
            $"/api/v1/form-assignments/{approvedAssignmentId}/response/submit",
            new
            {
                answers = new { q1 = "مكتمل-اعتماد" },
                clientMutationId = Guid.NewGuid(),
                expectedDraftVersion = 0,
                rowVersion = (string?)null,
                acknowledged = true,
                acknowledgementText = "أقر"
            });
        Assert.True(submitApproved.IsSuccessStatusCode, await submitApproved.Content.ReadAsStringAsync());
        var submittedForApprove = JsonSerializer.Deserialize<SubmitResult>(
            await submitApproved.Content.ReadAsStringAsync(), JsonOptions)!;
        var start = await reviewer.PostAsJsonAsync(
            $"/api/v1/form-responses/{submittedForApprove.ResponseId}/review/start",
            new { rowVersion = submittedForApprove.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, start.StatusCode);
        var reviewDetail = await reviewer.GetFromJsonAsync<ReviewDetail>(
            $"/api/v1/form-responses/{submittedForApprove.ResponseId}/review", JsonOptions);
        Assert.NotNull(reviewDetail);
        var approve = await reviewer.PostAsJsonAsync(
            $"/api/v1/form-responses/{submittedForApprove.ResponseId}/approve",
            new { reason = "اعتماد", rowVersion = reviewDetail!.Workspace.RowVersion });
        Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());

        // Current + response null: open assignment without draft.
        var (currentFormId, currentVersionId) = await CreateLockedFormAsync(designer, formApprover, $"{marker}C");
        await CreateAndPublishCampaignAsync(
            designer, publisher, currentFormId, currentVersionId, SeedIds.FacilityA1,
            requiredApprovalLevels: 1, campaignName: marker,
            completionBasis: FormCompletionBasis.Approved, reviewMode: FormReviewMode.SingleLevel);

        // Overdue + DueAtUtcOverride: open response with past override (API rejects past return due).
        var (overdueFormId, overdueVersionId) = await CreateLockedFormAsync(designer, formApprover, $"{marker}O");
        var overdueCampaign = await CreateAndPublishCampaignAsync(
            designer, publisher, overdueFormId, overdueVersionId, SeedIds.FacilityA1,
            requiredApprovalLevels: 1, campaignName: marker,
            completionBasis: FormCompletionBasis.Approved, reviewMode: FormReviewMode.SingleLevel);
        var overdueAssignmentId = await GetFirstAssignmentIdAsync(publisher, overdueCampaign.Id);
        var draftOverdue = await respondent.PutAsJsonAsync(
            $"/api/v1/form-assignments/{overdueAssignmentId}/response/draft",
            new
            {
                answers = new { q1 = "متأخر" },
                clientMutationId = Guid.NewGuid(),
                expectedDraftVersion = 0,
                rowVersion = (string?)null
            });
        Assert.True(draftOverdue.IsSuccessStatusCode, await draftOverdue.Content.ReadAsStringAsync());
        var overdueDraft = JsonSerializer.Deserialize<DraftResult>(
            await draftOverdue.Content.ReadAsStringAsync(), JsonOptions)!;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var response = await db.FormResponses.SingleAsync(r => r.Id == overdueDraft.ResponseId);
            response.DueAtUtcOverride = DateTimeOffset.UtcNow.AddMinutes(-30);
            await db.SaveChangesAsync();
        }

        var completed = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?workStatus=completed&search={search}&page=1&pageSize=10", JsonOptions);
        var current = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?workStatus=current&search={search}&page=1&pageSize=10", JsonOptions);
        var overdue = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?workStatus=overdue&search={search}&page=1&pageSize=10", JsonOptions);
        var page2 = await respondent.GetFromJsonAsync<PagedWorkspace>(
            $"/api/v1/form-response-workspace?workStatus=current&search={search}&page=2&pageSize=1", JsonOptions);

        Assert.NotNull(completed);
        Assert.NotNull(current);
        Assert.NotNull(overdue);
        Assert.NotNull(page2);
        Assert.Equal(2, completed!.TotalCount);
        Assert.Equal(2, completed.Items.Count);
        Assert.True(current!.TotalCount >= 1);
        Assert.True(overdue!.TotalCount >= 1);
        Assert.Equal(current.TotalCount, page2!.TotalCount);
        Assert.True(completed.TotalCount + current.TotalCount + overdue.TotalCount >= 4);
    }

    [IntegrationConnectionFact]
    public async Task StartReview_writes_form_response_history()
    {
        await SeedWorkflowUsersAsync();
        var submitted = await SubmitSingleResponseAsync();
        var reviewer = _factory.CreateAuthenticatedClient("rsp-h-reviewer");

        var start = await reviewer.PostAsJsonAsync(
            $"/api/v1/form-responses/{submitted.ResponseId}/review/start",
            new { rowVersion = submitted.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, start.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var history = await db.FormResponseHistories.AsNoTracking()
            .Where(h => h.ResponseId == submitted.ResponseId && h.EventType == "FormResponseReviewStarted")
            .ToListAsync();
        Assert.Single(history);
    }

    [IntegrationConnectionFact]
    public async Task Form_response_attachment_cross_facility_upload_denied()
    {
        await SeedWorkflowUsersAsync();
        await _factory.SeedUserWithPermissionsAsync(
            "rsp-h-facility-att",
            "منسق مرفقات",
            [RoleCodes.FacilityCoordinator],
            [PermissionCodes.FormsRespond, PermissionCodes.AttachmentsUpload],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserWithPermissionsAsync(
            "rsp-h-facility-b",
            "منسق ب",
            [RoleCodes.FacilityCoordinator],
            [PermissionCodes.FormsRespond, PermissionCodes.AttachmentsUpload],
            (ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1));

        var designer = _factory.CreateAuthenticatedClient("rsp-h-designer");
        var approver = _factory.CreateAuthenticatedClient("rsp-h-form-approver");
        var publisher = _factory.CreateAuthenticatedClient("rsp-h-publisher");
        var respondentA = _factory.CreateAuthenticatedClient("rsp-h-facility-att");
        var respondentB = _factory.CreateAuthenticatedClient("rsp-h-facility-b");

        var draftA = await CreateDraftResponseAsync(designer, approver, publisher, respondentA, SeedIds.FacilityA1);
        var draftB = await CreateDraftResponseAsync(designer, approver, publisher, respondentB, SeedIds.FacilityB1);

        using var wrongTarget = BuildAttachmentContent(draftB.ResponseId, "wrong.txt");
        var wrongUpload = await respondentA.PostAsync("/api/v1/attachments", wrongTarget);
        Assert.Equal(HttpStatusCode.NotFound, wrongUpload.StatusCode);

        using var okTarget = BuildAttachmentContent(draftA.ResponseId, "ok.txt");
        var okUpload = await respondentA.PostAsync("/api/v1/attachments", okTarget);
        Assert.Equal(HttpStatusCode.Created, okUpload.StatusCode);
    }

    private async Task SeedWorkflowUsersAsync()
    {
        await _factory.SeedUserAsync("rsp-h-designer", "مصمم", [RoleCodes.FormDesigner], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("rsp-h-form-approver", "معتمد", [RoleCodes.FormApprover], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("rsp-h-publisher", "ناشر", [RoleCodes.FormPublisher], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("rsp-h-facility", "منسق", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserWithPermissionsAsync(
            "rsp-h-reviewer",
            "مراجع",
            [RoleCodes.FormRegionalMonitor],
            [
                PermissionCodes.FormsReviewResponses,
                PermissionCodes.FormsApproveResponses,
                PermissionCodes.FormsViewResponses
            ],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserWithPermissionsAsync(
            "rsp-h-viewer",
            "عارض",
            [RoleCodes.FormRegionalMonitor],
            [PermissionCodes.FormsViewResponses],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserWithPermissionsAsync(
            "rsp-h-review-only",
            "مراجع فقط",
            [RoleCodes.FormRegionalMonitor],
            [PermissionCodes.FormsReviewResponses],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserWithPermissionsAsync(
            "rsp-h-approver",
            "معتمد رد",
            [RoleCodes.FormRegionalMonitor],
            [PermissionCodes.FormsApproveResponses, PermissionCodes.FormsViewResponses],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserAsync("rsp-h-outsider", "خارج", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1));
    }

    private async Task SeedRespondOnlyUserAsync(string subject)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var roleCode = $"respond-only-{subject}";
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode);
        if (role is null)
        {
            role = new Role
            {
                Code = roleCode,
                NameAr = "مستجيب فقط",
                IsSystem = false
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var permission = await db.Permissions.FirstAsync(p => p.Code == PermissionCodes.FormsRespond);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            await db.SaveChangesAsync();
        }

        await _factory.SeedUserAsync(subject, "مستجيب فقط", [roleCode],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
    }

    private async Task<SubmittedContext> SubmitSingleResponseAsync(
        int requiredApprovalLevels = 1,
        string respondentSubject = "rsp-h-facility")
    {
        var designer = _factory.CreateAuthenticatedClient("rsp-h-designer");
        var approver = _factory.CreateAuthenticatedClient("rsp-h-form-approver");
        var publisher = _factory.CreateAuthenticatedClient("rsp-h-publisher");
        var respondent = _factory.CreateAuthenticatedClient(respondentSubject);

        var (formId, versionId) = await CreateLockedFormAsync(designer, approver);
        var campaign = await CreateAndPublishCampaignAsync(designer, publisher, formId, versionId, SeedIds.FacilityA1, requiredApprovalLevels);
        var assignmentId = await GetFirstAssignmentIdAsync(publisher, campaign.Id);

        var submit = await respondent.PostAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/submit", new
        {
            answers = new { q1 = "إجابة نهائية" },
            clientMutationId = Guid.NewGuid(),
            expectedDraftVersion = 0,
            rowVersion = (string?)null,
            acknowledged = true,
            acknowledgementText = "أقر"
        });
        var submitBody = await submit.Content.ReadAsStringAsync();
        Assert.True(submit.IsSuccessStatusCode, submitBody);
        var submitted = JsonSerializer.Deserialize<SubmitResult>(submitBody, JsonOptions)!;
        return new SubmittedContext(submitted.ResponseId, assignmentId, formId, submitted.RowVersion);
    }

    private async Task<DraftContext> CreateDraftResponseAsync(
        HttpClient designer,
        HttpClient approver,
        HttpClient publisher,
        HttpClient respondent,
        Guid facilityId)
    {
        var (formId, versionId) = await CreateLockedFormAsync(designer, approver);
        var campaign = await CreateAndPublishCampaignAsync(designer, publisher, formId, versionId, facilityId);
        var assignmentId = await GetFirstAssignmentIdAsync(publisher, campaign.Id);
        var draft = await respondent.PutAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/draft", new
        {
            answers = new { q1 = "مسودة" },
            clientMutationId = Guid.NewGuid(),
            expectedDraftVersion = 0,
            rowVersion = (string?)null
        });
        Assert.True(draft.IsSuccessStatusCode, await draft.Content.ReadAsStringAsync());
        var draftDto = JsonSerializer.Deserialize<DraftResult>(await draft.Content.ReadAsStringAsync(), JsonOptions)!;
        return new DraftContext(draftDto.ResponseId, assignmentId);
    }

    private async Task<(Guid FormId, Guid VersionId)> CreateLockedFormAsync(
        HttpClient designer, HttpClient approver, string? codeSuffix = null)
    {
        var createForm = await designer.PostAsJsonAsync("/api/v1/forms", new
        {
            code = $"FRH{_seq++:D4}{codeSuffix ?? string.Empty}",
            nameAr = "نموذج ردود",
            description = "وصف كافٍ لاختبار مساحة الردود والتحقق",
            classification = 0,
            scopeType = ScopeType.Global
        });
        Assert.True(createForm.IsSuccessStatusCode, await createForm.Content.ReadAsStringAsync());
        using var formDoc = JsonDocument.Parse(await createForm.Content.ReadAsStringAsync());
        var formId = formDoc.RootElement.GetProperty("id").GetGuid();

        var createVersion = await designer.PostAsJsonAsync($"/api/v1/forms/{formId}/versions", new { });
        Assert.True(createVersion.IsSuccessStatusCode, await createVersion.Content.ReadAsStringAsync());
        using var versionDoc = JsonDocument.Parse(await createVersion.Content.ReadAsStringAsync());
        var versionId = versionDoc.RootElement.GetProperty("id").GetGuid();
        var rowVersion = versionDoc.RootElement.GetProperty("rowVersion").GetString();

        var pageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var schema = JsonSerializer.Serialize(new
        {
            schemaFormatVersion = 1,
            pages = new[]
            {
                new
                {
                    id = pageId,
                    key = "p1",
                    titleAr = "صفحة",
                    order = 0,
                    sections = new[]
                    {
                        new
                        {
                            id = sectionId,
                            key = "s1",
                            titleAr = "قسم",
                            order = 0,
                            fields = new[]
                            {
                                new
                                {
                                    id = fieldId,
                                    key = "q1",
                                    type = 0,
                                    labelAr = "سؤال",
                                    order = 0,
                                    layoutWidth = 0,
                                    isRequired = true,
                                    text = new { minLength = 2, maxLength = 200 },
                                    validationRules = Array.Empty<object>(),
                                    isReadOnly = false,
                                    isCalculated = false
                                }
                            }
                        }
                    }
                }
            }
        });

        var save = await designer.PutAsJsonAsync($"/api/v1/forms/{formId}/versions/{versionId}/schema", new { schemaJson = schema, rowVersion });
        Assert.True(save.IsSuccessStatusCode, await save.Content.ReadAsStringAsync());
        using var saved = JsonDocument.Parse(await save.Content.ReadAsStringAsync());
        rowVersion = saved.RootElement.GetProperty("rowVersion").GetString();

        var submit = await designer.PostAsJsonAsync($"/api/v1/forms/{formId}/versions/{versionId}/submit-review", new { reason = "مراجعة", rowVersion });
        Assert.True(submit.IsSuccessStatusCode, await submit.Content.ReadAsStringAsync());
        using var submitted = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
        rowVersion = submitted.RootElement.GetProperty("rowVersion").GetString();

        var approve = await approver.PostAsJsonAsync($"/api/v1/forms/{formId}/versions/{versionId}/approve-lock", new { reason = "اعتماد", rowVersion });
        Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());
        return (formId, versionId);
    }

    private async Task<CampaignDetail> CreateAndPublishCampaignAsync(
        HttpClient designer,
        HttpClient publisher,
        Guid formId,
        Guid versionId,
        Guid facilityId,
        int requiredApprovalLevels = 1,
        string? campaignName = null,
        FormCompletionBasis? completionBasis = null,
        FormReviewMode? reviewMode = null)
    {
        var firstOpen = DateTimeOffset.UtcNow.AddMinutes(-10);
        var resolvedReviewMode = reviewMode
            ?? (requiredApprovalLevels > 1
                ? FormReviewMode.MultiLevel
                : requiredApprovalLevels == 0
                    ? FormReviewMode.None
                    : FormReviewMode.SingleLevel);
        var resolvedCompletionBasis = completionBasis ?? FormCompletionBasis.Approved;
        var create = await designer.PostAsJsonAsync("/api/v1/form-campaigns", new
        {
            formDefinitionId = formId,
            formVersionId = versionId,
            code = $"RH{_seq++:D4}",
            nameAr = campaignName ?? "حملة ردود",
            priority = 1,
            timeZoneId = "Asia/Riyadh",
            schedule = new
            {
                recurrenceKind = 0,
                firstOpenAtLocal = firstOpen,
                responseWindowMinutes = 240,
                gracePeriodMinutes = 30,
                closeAfterMinutes = 60,
                businessDayAdjustment = 0
            },
            targets = new[] { new { ruleType = 2, regionIds = (Guid[]?)null, facilityIds = new[] { facilityId }, dynamicCriteria = (object?)null } },
            exclusions = Array.Empty<object>(),
            responsePolicy = new
            {
                completionBasis = resolvedCompletionBasis,
                reviewMode = resolvedReviewMode,
                requiredApprovalLevels,
                allowLateSubmission = true,
                allowResubmissionAfterReturn = true,
                requireSubmissionAcknowledgement = true,
                requireSeparationOfDuties = true
            }
        });
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.IsSuccessStatusCode, createBody);
        var campaign = JsonSerializer.Deserialize<CampaignDetail>(createBody, JsonOptions)!;
        var publish = await publisher.PostAsJsonAsync($"/api/v1/form-campaigns/{campaign.Id}/publish", new { rowVersion = campaign.RowVersion });
        Assert.True(publish.IsSuccessStatusCode, await publish.Content.ReadAsStringAsync());
        return JsonSerializer.Deserialize<CampaignDetail>(await publish.Content.ReadAsStringAsync(), JsonOptions)!;
    }

    private async Task<Guid> GetFirstAssignmentIdAsync(HttpClient publisher, Guid campaignId)
    {
        var cycles = await publisher.GetFromJsonAsync<PagedCycles>($"/api/v1/form-campaigns/{campaignId}/cycles", JsonOptions);
        Assert.NotNull(cycles);
        var assignments = await publisher.GetFromJsonAsync<PagedAssignments>(
            $"/api/v1/form-campaigns/{campaignId}/cycles/{cycles!.Items[0].Id}/assignments", JsonOptions);
        Assert.NotNull(assignments);
        return assignments!.Items[0].Id;
    }

    private static MultipartFormDataContent BuildAttachmentContent(Guid responseId, string fileName)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("FormResponse"), "entityType");
        content.Add(new StringContent(responseId.ToString()), "entityId");
        content.Add(new StringContent("مرفق"), "reason");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("payload"))
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") }
        }, "file", fileName);
        return content;
    }

    private sealed record SubmittedContext(Guid ResponseId, Guid AssignmentId, Guid FormId, string RowVersion);
    private sealed record DraftContext(Guid ResponseId, Guid AssignmentId);
    private sealed record DraftResult(Guid ResponseId, int DraftVersion, string RowVersion);
    private sealed record SubmitResult(Guid ResponseId, Guid SubmissionId, int SubmissionNumber, FormResponseStatus Status, string RowVersion);
    private sealed record CampaignDetail(Guid Id, string RowVersion, FormCampaignStatus Status);
    private sealed record PagedCycles(int TotalCount, List<CycleItem> Items);
    private sealed record CycleItem(Guid Id);
    private sealed record PagedAssignments(int TotalCount, List<AssignmentItem> Items);
    private sealed record AssignmentItem(Guid Id);
    private sealed record CommentDto(string Body);
    private sealed record ResponseDetail(List<CommentDto> VisibleComments);
    private sealed record ReviewDetail(WorkspaceDto Workspace);
    private sealed record WorkspaceDto(string? RowVersion);
    private sealed record PagedWorkspace(int TotalCount, List<WorkspaceItem> Items);
    private sealed record WorkspaceItem(Guid AssignmentId, Guid FacilityId);
}
