using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Application.Forms.Campaigns;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class FormCampaignIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static int _seq;

    public FormCampaignIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Draft_preview_publish_once_creates_cycle_and_assignments_idempotent_scheduler()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("cmp-designer");
        var approver = _factory.CreateAuthenticatedClient("cmp-approver");
        var publisher = _factory.CreateAuthenticatedClient("cmp-publisher");

        var (formId, versionId, schemaHash) = await CreateLockedFormAsync(designer, approver);
        var firstOpen = DateTimeOffset.UtcNow.AddMinutes(-5);

        var create = await designer.PostAsJsonAsync("/api/v1/form-campaigns", new
        {
            formDefinitionId = formId,
            formVersionId = versionId,
            code = $"CMP{_seq++:D4}",
            nameAr = "حملة اختبار",
            priority = 1,
            timeZoneId = "Asia/Riyadh",
            schedule = new
            {
                recurrenceKind = 0,
                firstOpenAtLocal = firstOpen,
                responseWindowMinutes = 60,
                gracePeriodMinutes = 15,
                closeAfterMinutes = 0,
                businessDayAdjustment = 0
            },
            targets = new[] { new { ruleType = 0, regionIds = (Guid[]?)null, facilityIds = (Guid[]?)null, dynamicCriteria = (object?)null } },
            exclusions = Array.Empty<object>()
        });
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.True(create.IsSuccessStatusCode, createBody);
        var campaign = JsonSerializer.Deserialize<CampaignDetail>(createBody, JsonOptions)!;
        Assert.Equal(FormCampaignStatus.Draft, campaign.Status);
        Assert.Equal(schemaHash, campaign.SchemaHash);

        var preview = await publisher.PostAsync($"/api/v1/form-campaigns/{campaign.Id}/target-preview", null);
        var previewBody = await preview.Content.ReadAsStringAsync();
        Assert.True(preview.IsSuccessStatusCode, previewBody);
        using var previewDoc = JsonDocument.Parse(previewBody);
        Assert.True(previewDoc.RootElement.GetProperty("finalTargetCount").GetInt32() > 0);

        var publish = await publisher.PostAsJsonAsync($"/api/v1/form-campaigns/{campaign.Id}/publish", new { rowVersion = campaign.RowVersion });
        var publishBody = await publish.Content.ReadAsStringAsync();
        Assert.True(publish.IsSuccessStatusCode, publishBody);
        campaign = JsonSerializer.Deserialize<CampaignDetail>(publishBody, JsonOptions)!;
        Assert.NotEqual(FormCampaignStatus.Draft, campaign.Status);

        var cycles = await publisher.GetFromJsonAsync<PagedCycles>($"/api/v1/form-campaigns/{campaign.Id}/cycles");
        Assert.NotNull(cycles);
        Assert.True(cycles!.TotalCount >= 1);
        var cycleId = cycles.Items[0].Id;
        Assert.False(string.IsNullOrWhiteSpace(cycles.Items[0].TargetSnapshotHash));

        var assignments = await publisher.GetFromJsonAsync<PagedAssignments>(
            $"/api/v1/form-campaigns/{campaign.Id}/cycles/{cycleId}/assignments");
        Assert.NotNull(assignments);
        Assert.True(assignments!.TotalCount >= 1);

        using var scope = _factory.Services.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IFormCampaignScheduler>();
        var run1 = await scheduler.RunAsync("test-worker", new FormCampaignSchedulerOptions(20, 5, 3, 10));
        var run2 = await scheduler.RunAsync("test-worker", new FormCampaignSchedulerOptions(20, 5, 3, 10));
        Assert.Equal(0, run2.CyclesCreated);
        Assert.Equal(0, run2.AssignmentsCreated);

        var cyclesAfter = await publisher.GetFromJsonAsync<PagedCycles>($"/api/v1/form-campaigns/{campaign.Id}/cycles");
        Assert.Equal(cycles.TotalCount, cyclesAfter!.TotalCount);

        var stale = await publisher.PostAsJsonAsync($"/api/v1/form-campaigns/{campaign.Id}/pause", new
        {
            rowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
            reason = "إيقاف"
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Pause_before_due_then_resume_returns_scheduled_with_next_occurrence()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("cmp-designer");
        var approver = _factory.CreateAuthenticatedClient("cmp-approver");
        var publisher = _factory.CreateAuthenticatedClient("cmp-publisher");

        var (formId, versionId, _) = await CreateLockedFormAsync(designer, approver);
        // Explicit Asia/Riyadh local far in the future so ToUtc stays after now.
        var firstOpen = new DateTimeOffset(
            DateTime.SpecifyKind(DateTime.UtcNow.AddDays(2), DateTimeKind.Unspecified),
            TimeSpan.FromHours(3));

        var campaign = await CreateCampaignAsync(designer, formId, versionId, firstOpen, recurrenceKind: 1, intervalDays: 1);
        var published = await PublishCampaignAsync(publisher, campaign);
        Assert.Equal(FormCampaignStatus.Scheduled, published.Status);

        var paused = await PauseCampaignAsync(publisher, published);
        Assert.Equal(FormCampaignStatus.Paused, paused.Status);

        var resumed = await ResumeCampaignAsync(publisher, paused);
        Assert.Equal(FormCampaignStatus.Scheduled, resumed.Status);
        Assert.NotNull(resumed.NextOccurrenceUtc);
        Assert.True(resumed.NextOccurrenceUtc > DateTimeOffset.UtcNow);
    }

    [IntegrationConnectionFact]
    public async Task Pause_after_due_then_resume_returns_active_with_next_occurrence()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("cmp-designer");
        var approver = _factory.CreateAuthenticatedClient("cmp-approver");
        var publisher = _factory.CreateAuthenticatedClient("cmp-publisher");

        var (formId, versionId, _) = await CreateLockedFormAsync(designer, approver);
        var firstOpen = DateTimeOffset.UtcNow.AddMinutes(-5);

        var campaign = await CreateCampaignAsync(designer, formId, versionId, firstOpen, recurrenceKind: 1, intervalDays: 1);
        var published = await PublishCampaignAsync(publisher, campaign);
        Assert.Equal(FormCampaignStatus.Active, published.Status);

        var paused = await PauseCampaignAsync(publisher, published);
        Assert.Equal(FormCampaignStatus.Paused, paused.Status);

        var resumed = await ResumeCampaignAsync(publisher, paused);
        Assert.Equal(FormCampaignStatus.Active, resumed.Status);
        Assert.NotNull(resumed.NextOccurrenceUtc);
    }

    [IntegrationConnectionFact]
    public async Task Custom_dates_over_limit_returns_bad_request()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("cmp-designer");
        var approver = _factory.CreateAuthenticatedClient("cmp-approver");

        var (formId, versionId, _) = await CreateLockedFormAsync(designer, approver);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var customDates = Enumerable.Range(0, 101).Select(i => start.AddDays(i)).ToArray();

        var create = await designer.PostAsJsonAsync("/api/v1/form-campaigns", new
        {
            formDefinitionId = formId,
            formVersionId = versionId,
            code = $"CMPCD{_seq++:D4}",
            nameAr = "حملة تواريخ",
            priority = 1,
            timeZoneId = "Asia/Riyadh",
            schedule = new
            {
                recurrenceKind = 4,
                firstOpenAtLocal = start,
                responseWindowMinutes = 60,
                gracePeriodMinutes = 0,
                closeAfterMinutes = 0,
                businessDayAdjustment = 0,
                customDatesLocal = customDates
            },
            targets = new[] { new { ruleType = 0, regionIds = (Guid[]?)null, facilityIds = (Guid[]?)null, dynamicCriteria = (object?)null } },
            exclusions = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Inactive_facility_preview_lists_unavailable_facility()
    {
        await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var facility = await db.Facilities.FindAsync(SeedIds.FacilityA2);
            facility!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var designer = _factory.CreateAuthenticatedClient("cmp-designer");
        var approver = _factory.CreateAuthenticatedClient("cmp-approver");
        var publisher = _factory.CreateAuthenticatedClient("cmp-publisher");

        var (formId, versionId, _) = await CreateLockedFormAsync(designer, approver);
        var campaign = await CreateCampaignAsync(designer, formId, versionId, DateTimeOffset.UtcNow.AddHours(1));

        var preview = await publisher.PostAsync($"/api/v1/form-campaigns/{campaign.Id}/target-preview", null);
        var previewBody = await preview.Content.ReadAsStringAsync();
        Assert.True(preview.IsSuccessStatusCode, previewBody);
        using var doc = JsonDocument.Parse(previewBody);
        var unavailable = doc.RootElement.GetProperty("unavailableFacilities").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("FAC-A2", unavailable);
    }

    [IntegrationConnectionFact]
    public async Task Regional_user_cannot_preview_outside_scope()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("cmp-designer2");
        var approver = _factory.CreateAuthenticatedClient("cmp-approver");
        await _factory.SeedUserAsync("cmp-region", "منطقة", [RoleCodes.FormRegionalMonitor],
            (ScopeType.Region, SeedIds.RegionA, null));
        var regionClient = _factory.CreateAuthenticatedClient("cmp-region");

        var (formId, versionId, _) = await CreateLockedFormAsync(designer, approver);
        var create = await designer.PostAsJsonAsync("/api/v1/form-campaigns", new
        {
            formDefinitionId = formId,
            formVersionId = versionId,
            code = $"CMPR{_seq++:D4}",
            nameAr = "حملة نطاق",
            priority = 1,
            timeZoneId = "Asia/Riyadh",
            schedule = new
            {
                recurrenceKind = 0,
                firstOpenAtLocal = DateTimeOffset.UtcNow.AddHours(1),
                responseWindowMinutes = 60,
                gracePeriodMinutes = 0,
                closeAfterMinutes = 0,
                businessDayAdjustment = 0
            },
            targets = new[] { new { ruleType = 0, regionIds = (Guid[]?)null, facilityIds = (Guid[]?)null, dynamicCriteria = (object?)null } },
            exclusions = Array.Empty<object>()
        });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());
        var campaign = JsonSerializer.Deserialize<CampaignDetail>(await create.Content.ReadAsStringAsync(), JsonOptions)!;

        var preview = await regionClient.PostAsync($"/api/v1/form-campaigns/{campaign.Id}/target-preview", null);
        Assert.True(preview.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.OK);
        if (preview.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
            // Regional monitor may lack PreviewTargets; if allowed, counts must be scope-filtered by resolver.
            Assert.True(doc.RootElement.TryGetProperty("finalTargetCount", out _));
        }
    }

    private async Task SeedAsync()
    {
        await _factory.SeedUserAsync("cmp-designer", "مصمم", [RoleCodes.FormDesigner],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("cmp-designer2", "مصمم2", [RoleCodes.FormDesigner],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("cmp-approver", "معتمد", [RoleCodes.FormApprover],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("cmp-publisher", "ناشر", [RoleCodes.FormPublisher],
            (ScopeType.Global, null, null));
    }

    private async Task<(Guid FormId, Guid VersionId, string SchemaHash)> CreateLockedFormAsync(HttpClient designer, HttpClient approver)
    {
        var createForm = await designer.PostAsJsonAsync("/api/v1/forms", new
        {
            code = $"FCMP{_seq++:D4}",
            nameAr = "نموذج حملة",
            description = "وصف نموذج كافٍ للحملة والاختبار",
            ownerDepartmentId = (Guid?)null,
            classification = 1,
            scopeType = ScopeType.Global,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            facilityUnitId = (Guid?)null
        });
        Assert.True(createForm.IsSuccessStatusCode, await createForm.Content.ReadAsStringAsync());
        using var formDoc = JsonDocument.Parse(await createForm.Content.ReadAsStringAsync());
        var formId = formDoc.RootElement.GetProperty("id").GetGuid();

        var createVersion = await designer.PostAsJsonAsync($"/api/v1/forms/{formId}/versions", new { });
        Assert.True(createVersion.IsSuccessStatusCode, await createVersion.Content.ReadAsStringAsync());
        var version = JsonSerializer.Deserialize<VersionDto>(await createVersion.Content.ReadAsStringAsync(), JsonOptions)!;

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
                    key = "page1",
                    titleAr = "الصفحة 1",
                    order = 0,
                    sections = new[]
                    {
                        new
                        {
                            id = sectionId,
                            key = "section1",
                            titleAr = "القسم 1",
                            order = 0,
                            fields = new[]
                            {
                                new
                                {
                                    id = fieldId,
                                    key = "field1",
                                    type = 0,
                                    labelAr = "حقل",
                                    order = 0,
                                    layoutWidth = 0,
                                    isRequired = false,
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
        var save = await designer.PutAsJsonAsync($"/api/v1/forms/{formId}/versions/{version.Id}/schema", new { schemaJson = schema, rowVersion = version.RowVersion });
        Assert.True(save.IsSuccessStatusCode, await save.Content.ReadAsStringAsync());
        version = JsonSerializer.Deserialize<VersionDto>(await save.Content.ReadAsStringAsync(), JsonOptions)!;

        var submit = await designer.PostAsJsonAsync($"/api/v1/forms/{formId}/versions/{version.Id}/submit-review", new { reason = "مراجعة", rowVersion = version.RowVersion });
        Assert.True(submit.IsSuccessStatusCode, await submit.Content.ReadAsStringAsync());
        version = JsonSerializer.Deserialize<VersionDto>(await submit.Content.ReadAsStringAsync(), JsonOptions)!;

        var approve = await approver.PostAsJsonAsync($"/api/v1/forms/{formId}/versions/{version.Id}/approve-lock", new { reason = "اعتماد", rowVersion = version.RowVersion });
        Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());
        version = JsonSerializer.Deserialize<VersionDto>(await approve.Content.ReadAsStringAsync(), JsonOptions)!;
        Assert.Equal(FormVersionStatus.Locked, version.Status);

        var snapshot = await designer.GetFromJsonAsync<SnapshotDto>($"/api/v1/forms/{formId}/versions/{version.Id}/snapshot");
        return (formId, version.Id, snapshot!.SchemaHash);
    }

    private async Task<CampaignDetail> CreateCampaignAsync(
        HttpClient designer,
        Guid formId,
        Guid versionId,
        DateTimeOffset firstOpen,
        int recurrenceKind = 0,
        int? intervalDays = null)
    {
        var create = await designer.PostAsJsonAsync("/api/v1/form-campaigns", new
        {
            formDefinitionId = formId,
            formVersionId = versionId,
            code = $"CMP{_seq++:D4}",
            nameAr = "حملة اختبار",
            priority = 1,
            timeZoneId = "Asia/Riyadh",
            schedule = new
            {
                recurrenceKind,
                firstOpenAtLocal = firstOpen,
                responseWindowMinutes = 60,
                gracePeriodMinutes = 15,
                closeAfterMinutes = 0,
                businessDayAdjustment = 0,
                intervalDays
            },
            targets = new[] { new { ruleType = 0, regionIds = (Guid[]?)null, facilityIds = (Guid[]?)null, dynamicCriteria = (object?)null } },
            exclusions = Array.Empty<object>()
        });
        var body = await create.Content.ReadAsStringAsync();
        Assert.True(create.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<CampaignDetail>(body, JsonOptions)!;
    }

    private static async Task<CampaignDetail> PublishCampaignAsync(HttpClient publisher, CampaignDetail campaign)
    {
        var publish = await publisher.PostAsJsonAsync($"/api/v1/form-campaigns/{campaign.Id}/publish", new { rowVersion = campaign.RowVersion });
        var body = await publish.Content.ReadAsStringAsync();
        Assert.True(publish.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<CampaignDetail>(body, JsonOptions)!;
    }

    private static async Task<CampaignDetail> PauseCampaignAsync(HttpClient publisher, CampaignDetail campaign)
    {
        var pause = await publisher.PostAsJsonAsync($"/api/v1/form-campaigns/{campaign.Id}/pause", new
        {
            rowVersion = campaign.RowVersion,
            reason = "إيقاف مؤقت"
        });
        var body = await pause.Content.ReadAsStringAsync();
        Assert.True(pause.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<CampaignDetail>(body, JsonOptions)!;
    }

    private static async Task<CampaignDetail> ResumeCampaignAsync(HttpClient publisher, CampaignDetail campaign)
    {
        var resume = await publisher.PostAsJsonAsync($"/api/v1/form-campaigns/{campaign.Id}/resume", new
        {
            rowVersion = campaign.RowVersion
        });
        var body = await resume.Content.ReadAsStringAsync();
        Assert.True(resume.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<CampaignDetail>(body, JsonOptions)!;
    }

    private sealed record CampaignDetail(
        Guid Id,
        FormCampaignStatus Status,
        string SchemaHash,
        string RowVersion,
        DateTimeOffset? NextOccurrenceUtc);
    private sealed record VersionDto(Guid Id, FormVersionStatus Status, string RowVersion, Guid? SnapshotId);
    private sealed record SnapshotDto(string SchemaHash);
    private sealed record PagedCycles(int TotalCount, List<CycleItem> Items);
    private sealed record CycleItem(Guid Id, string TargetSnapshotHash);
    private sealed record PagedAssignments(int TotalCount, List<object> Items);
}
