using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.IntegrationTests;

public sealed class FormResponseIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static int _seq;

    public FormResponseIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Draft_submit_return_resubmit_approve_close_and_idor_404()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("rsp-designer");
        var approver = _factory.CreateAuthenticatedClient("rsp-form-approver");
        var publisher = _factory.CreateAuthenticatedClient("rsp-publisher");
        var respondent = _factory.CreateAuthenticatedClient("rsp-facility");
        var reviewer = _factory.CreateAuthenticatedClient("rsp-reviewer");
        var outsider = _factory.CreateAuthenticatedClient("rsp-outsider");

        var (formId, versionId) = await CreateLockedFormAsync(designer, approver);
        var campaign = await CreateAndPublishCampaignAsync(designer, publisher, formId, versionId);
        var assignmentId = await GetFirstAssignmentIdAsync(publisher, campaign.Id);

        var getNotStarted = await respondent.GetAsync($"/api/v1/form-assignments/{assignmentId}/response");
        Assert.True(getNotStarted.IsSuccessStatusCode, await getNotStarted.Content.ReadAsStringAsync());
        using var notStartedDoc = JsonDocument.Parse(await getNotStarted.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, notStartedDoc.RootElement.GetProperty("responseId").ValueKind);

        var mutationId = Guid.NewGuid();
        var draft1 = await respondent.PutAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/draft", new
        {
            answers = new { q1 = "مسودة أولى" },
            clientMutationId = mutationId,
            expectedDraftVersion = 0,
            rowVersion = (string?)null
        });
        var draft1Body = await draft1.Content.ReadAsStringAsync();
        Assert.True(draft1.IsSuccessStatusCode, draft1Body);
        var draft = JsonSerializer.Deserialize<DraftResult>(draft1Body, JsonOptions)!;

        var draftRetry = await respondent.PutAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/draft", new
        {
            answers = new { q1 = "يجب ألا يتكرر" },
            clientMutationId = mutationId,
            expectedDraftVersion = 0,
            rowVersion = (string?)null
        });
        var draftRetryBody = await draftRetry.Content.ReadAsStringAsync();
        Assert.True(draftRetry.IsSuccessStatusCode, draftRetryBody);
        var draftRetryDto = JsonSerializer.Deserialize<DraftResult>(draftRetryBody, JsonOptions)!;
        Assert.Equal(draft.DraftVersion, draftRetryDto.DraftVersion);

        var conflict = await respondent.PutAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/draft", new
        {
            answers = new { q1 = "تعارض" },
            clientMutationId = Guid.NewGuid(),
            expectedDraftVersion = draft.DraftVersion - 1,
            rowVersion = draft.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var idor = await outsider.GetAsync($"/api/v1/form-assignments/{assignmentId}/response");
        Assert.Equal(HttpStatusCode.NotFound, idor.StatusCode);

        var submit = await respondent.PostAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/submit", new
        {
            answers = new { q1 = "إجابة نهائية" },
            clientMutationId = Guid.NewGuid(),
            expectedDraftVersion = draft.DraftVersion,
            rowVersion = draft.RowVersion,
            acknowledged = true,
            acknowledgementText = "أقر بصحة البيانات"
        });
        var submitBody = await submit.Content.ReadAsStringAsync();
        Assert.True(submit.IsSuccessStatusCode, submitBody);
        var submitted = JsonSerializer.Deserialize<SubmitResult>(submitBody, JsonOptions)!;
        Assert.Equal(FormResponseStatus.UnderReview, submitted.Status);

        var selfApprove = await respondent.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/approve", new
        {
            reason = "ذاتي",
            rowVersion = submitted.RowVersion
        });
        Assert.True(selfApprove.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);

        var returnResp = await reviewer.PostAsJsonAsync($"/api/v1/form-responses/{submitted.ResponseId}/return", new
        {
            reason = "يلزم توضيح",
            newDueAtUtc = DateTimeOffset.UtcNow.AddDays(3),
            comments = new[] { new { fieldKey = "q1", body = "وضّح أكثر", isVisibleToRespondent = true } },
            rowVersion = submitted.RowVersion
        });
        Assert.True(returnResp.IsSuccessStatusCode, await returnResp.Content.ReadAsStringAsync());

        var afterReturn = await respondent.GetFromJsonAsync<ResponseDetail>($"/api/v1/form-assignments/{assignmentId}/response", JsonOptions);
        Assert.NotNull(afterReturn);
        Assert.Equal(FormResponseStatus.Returned, afterReturn!.ResponseStatus);
        Assert.NotEmpty(afterReturn.VisibleComments);

        var resubmit = await respondent.PostAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/submit", new
        {
            answers = new { q1 = "إجابة بعد الإعادة" },
            clientMutationId = Guid.NewGuid(),
            expectedDraftVersion = afterReturn.DraftVersion,
            rowVersion = afterReturn.RowVersion,
            acknowledged = true,
            acknowledgementText = "أقر"
        });
        var resubmitBody = await resubmit.Content.ReadAsStringAsync();
        Assert.True(resubmit.IsSuccessStatusCode, resubmitBody);
        var resubmitted = JsonSerializer.Deserialize<SubmitResult>(resubmitBody, JsonOptions)!;
        Assert.Equal(2, resubmitted.SubmissionNumber);

        var submissions = await reviewer.GetFromJsonAsync<List<SubmissionDto>>(
            $"/api/v1/form-responses/{resubmitted.ResponseId}/submissions", JsonOptions);
        Assert.NotNull(submissions);
        Assert.Equal(2, submissions!.Count);

        var detail = await reviewer.GetFromJsonAsync<ReviewDetail>(
            $"/api/v1/form-responses/{resubmitted.ResponseId}/review", JsonOptions);
        Assert.NotNull(detail);

        var approve = await reviewer.PostAsJsonAsync($"/api/v1/form-responses/{resubmitted.ResponseId}/approve", new
        {
            reason = "مناسب",
            rowVersion = detail!.Workspace.RowVersion
        });
        Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());

        var afterApprove = await reviewer.GetFromJsonAsync<ReviewDetail>(
            $"/api/v1/form-responses/{resubmitted.ResponseId}/review", JsonOptions);
        Assert.Equal(FormResponseStatus.Approved, afterApprove!.Workspace.ResponseStatus);

        var close = await reviewer.PostAsJsonAsync($"/api/v1/form-responses/{resubmitted.ResponseId}/close", new
        {
            reason = "إغلاق",
            rowVersion = afterApprove.Workspace.RowVersion
        });
        Assert.True(close.IsSuccessStatusCode, await close.Content.ReadAsStringAsync());
    }

    [IntegrationConnectionFact]
    public async Task Submit_validation_failure_returns_422()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("rsp-designer");
        var approver = _factory.CreateAuthenticatedClient("rsp-form-approver");
        var publisher = _factory.CreateAuthenticatedClient("rsp-publisher");
        var respondent = _factory.CreateAuthenticatedClient("rsp-facility");

        var (formId, versionId) = await CreateLockedFormAsync(designer, approver);
        var campaign = await CreateAndPublishCampaignAsync(designer, publisher, formId, versionId);
        var assignmentId = await GetFirstAssignmentIdAsync(publisher, campaign.Id);

        var draft = await respondent.PutAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/draft", new
        {
            answers = new { q1 = "س" },
            clientMutationId = Guid.NewGuid(),
            expectedDraftVersion = 0,
            rowVersion = (string?)null
        });
        var draftDto = JsonSerializer.Deserialize<DraftResult>(await draft.Content.ReadAsStringAsync(), JsonOptions)!;

        var submit = await respondent.PostAsJsonAsync($"/api/v1/form-assignments/{assignmentId}/response/submit", new
        {
            answers = new { },
            clientMutationId = Guid.NewGuid(),
            expectedDraftVersion = draftDto.DraftVersion,
            rowVersion = draftDto.RowVersion,
            acknowledged = true,
            acknowledgementText = "أقر"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, submit.StatusCode);
    }

    private async Task SeedAsync()
    {
        await _factory.SeedUserAsync("rsp-designer", "مصمم", [RoleCodes.FormDesigner], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("rsp-form-approver", "معتمد نموذج", [RoleCodes.FormApprover], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("rsp-publisher", "ناشر", [RoleCodes.FormPublisher], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("rsp-facility", "منسق سجن", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserWithPermissionsAsync(
            "rsp-reviewer",
            "مراجع ردود",
            [RoleCodes.FormRegionalMonitor],
            [
                PermissionCodes.FormsRespond,
                PermissionCodes.FormsViewResponses,
                PermissionCodes.FormsReviewResponses,
                PermissionCodes.FormsApproveResponses,
                PermissionCodes.FormsCloseResponses
            ],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserAsync("rsp-outsider", "خارج النطاق", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1));
    }

    private async Task<(Guid FormId, Guid VersionId)> CreateLockedFormAsync(HttpClient designer, HttpClient approver)
    {
        var createForm = await designer.PostAsJsonAsync("/api/v1/forms", new
        {
            code = $"FRSP{_seq++:D4}",
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
        HttpClient designer, HttpClient publisher, Guid formId, Guid versionId)
    {
        var firstOpen = DateTimeOffset.UtcNow.AddMinutes(-10);
        var create = await designer.PostAsJsonAsync("/api/v1/form-campaigns", new
        {
            formDefinitionId = formId,
            formVersionId = versionId,
            code = $"RSP{_seq++:D4}",
            nameAr = "حملة ردود",
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
            targets = new[] { new { ruleType = 2, regionIds = (Guid[]?)null, facilityIds = new[] { SeedIds.FacilityA1 }, dynamicCriteria = (object?)null } },
            exclusions = Array.Empty<object>(),
            responsePolicy = new
            {
                completionBasis = 1,
                reviewMode = 1,
                requiredApprovalLevels = 1,
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
        var publishBody = await publish.Content.ReadAsStringAsync();
        Assert.True(publish.IsSuccessStatusCode, publishBody);
        return JsonSerializer.Deserialize<CampaignDetail>(publishBody, JsonOptions)!;
    }

    private async Task<Guid> GetFirstAssignmentIdAsync(HttpClient publisher, Guid campaignId)
    {
        var cycles = await publisher.GetFromJsonAsync<PagedCycles>($"/api/v1/form-campaigns/{campaignId}/cycles", JsonOptions);
        Assert.NotNull(cycles);
        Assert.True(cycles!.TotalCount >= 1);
        var assignments = await publisher.GetFromJsonAsync<PagedAssignments>(
            $"/api/v1/form-campaigns/{campaignId}/cycles/{cycles.Items[0].Id}/assignments", JsonOptions);
        Assert.NotNull(assignments);
        Assert.True(assignments!.TotalCount >= 1);
        return assignments.Items[0].Id;
    }

    private sealed record DraftResult(Guid ResponseId, int DraftVersion, string RowVersion);
    private sealed record SubmitResult(Guid ResponseId, Guid SubmissionId, int SubmissionNumber, FormResponseStatus Status, string RowVersion);
    private sealed record CampaignDetail(Guid Id, string RowVersion, FormCampaignStatus Status);
    private sealed record PagedCycles(int TotalCount, List<CycleItem> Items);
    private sealed record CycleItem(Guid Id);
    private sealed record PagedAssignments(int TotalCount, List<AssignmentItem> Items);
    private sealed record AssignmentItem(Guid Id);
    private sealed record CommentDto(string Body);
    private sealed record ResponseDetail(
        Guid? ResponseId,
        FormResponseStatus? ResponseStatus,
        int DraftVersion,
        string? RowVersion,
        List<CommentDto> VisibleComments);
    private sealed record SubmissionDto(int SubmissionNumber);
    private sealed record ReviewDetail(WorkspaceDto Workspace);
    private sealed record WorkspaceDto(FormResponseStatus? ResponseStatus, string? RowVersion);
}
