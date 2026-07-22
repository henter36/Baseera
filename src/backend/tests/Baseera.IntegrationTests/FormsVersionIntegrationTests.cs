using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.IntegrationTests;

public sealed class FormsVersionIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static int _codeSequence;

    public FormsVersionIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Version_create_save_submit_approve_lock_and_reject_locked_update()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-v-designer");
        var reviewer = _factory.CreateAuthenticatedClient("forms-v-reviewer");
        var approver = _factory.CreateAuthenticatedClient("forms-v-approver");

        var form = await CreateFormAsync(designer);
        var createVersion = await designer.PostAsJsonAsync($"/api/v1/forms/{form.Id}/versions", new { basedOnVersionId = (Guid?)null });
        var createBody = await createVersion.Content.ReadAsStringAsync();
        Assert.True(createVersion.IsSuccessStatusCode, createBody);
        var version = JsonSerializer.Deserialize<VersionDetail>(createBody, JsonOptions)!;
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(FormVersionStatus.Draft, version.Status);

        var schema = MinimalValidSchemaJson();
        var save = await designer.PutAsJsonAsync($"/api/v1/forms/{form.Id}/versions/{version.Id}/schema", new
        {
            schemaJson = schema,
            rowVersion = version.RowVersion
        });
        var saveBody = await save.Content.ReadAsStringAsync();
        Assert.True(save.IsSuccessStatusCode, saveBody);
        version = JsonSerializer.Deserialize<VersionDetail>(saveBody, JsonOptions)!;

        var submit = await designer.PostAsJsonAsync(
            $"/api/v1/forms/{form.Id}/versions/{version.Id}/submit-review",
            new { reason = "للمراجعة", rowVersion = version.RowVersion });
        var submitBody = await submit.Content.ReadAsStringAsync();
        Assert.True(submit.IsSuccessStatusCode, submitBody);
        version = JsonSerializer.Deserialize<VersionDetail>(submitBody, JsonOptions)!;
        Assert.Equal(FormVersionStatus.InReview, version.Status);

        var approve = await approver.PostAsJsonAsync(
            $"/api/v1/forms/{form.Id}/versions/{version.Id}/approve-lock",
            new { reason = "اعتماد", rowVersion = version.RowVersion });
        var approveBody = await approve.Content.ReadAsStringAsync();
        Assert.True(approve.IsSuccessStatusCode, approveBody);
        version = JsonSerializer.Deserialize<VersionDetail>(approveBody, JsonOptions)!;
        Assert.Equal(FormVersionStatus.Locked, version.Status);
        Assert.NotNull(version.SnapshotId);

        var snapshot = await designer.GetFromJsonAsync<SnapshotDto>(
            $"/api/v1/forms/{form.Id}/versions/{version.Id}/snapshot");
        Assert.NotNull(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(snapshot!.SchemaHash));

        var lockedSave = await designer.PutAsJsonAsync($"/api/v1/forms/{form.Id}/versions/{version.Id}/schema", new
        {
            schemaJson = schema,
            rowVersion = version.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, lockedSave.StatusCode);

        // IDOR / out of scope
        await _factory.SeedUserAsync("forms-v-region", "منطقة", [RoleCodes.FormDesigner],
            (ScopeType.Region, SeedIds.RegionA, null));
        var regionClient = _factory.CreateAuthenticatedClient("forms-v-region");
        var idor = await regionClient.GetAsync($"/api/v1/forms/{form.Id}/versions/{version.Id}");
        Assert.Equal(HttpStatusCode.NotFound, idor.StatusCode);

        _ = reviewer;
    }

    [IntegrationConnectionFact]
    public async Task Version_rowversion_conflict_returns_409()
    {
        await SeedAsync();
        var designer = _factory.CreateAuthenticatedClient("forms-v-designer");
        var form = await CreateFormAsync(designer);
        var created = await designer.PostAsJsonAsync($"/api/v1/forms/{form.Id}/versions", new { });
        var version = JsonSerializer.Deserialize<VersionDetail>(await created.Content.ReadAsStringAsync(), JsonOptions)!;

        var conflict = await designer.PutAsJsonAsync($"/api/v1/forms/{form.Id}/versions/{version.Id}/schema", new
        {
            schemaJson = MinimalValidSchemaJson(),
            rowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private async Task SeedAsync()
    {
        await _factory.SeedUserAsync("forms-v-designer", "مصمم", [RoleCodes.FormDesigner],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("forms-v-reviewer", "مراجع", [RoleCodes.FormReviewer],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("forms-v-approver", "معتمد", [RoleCodes.FormApprover],
            (ScopeType.Global, null, null));
    }

    private static async Task<FormDetail> CreateFormAsync(HttpClient client)
    {
        var code = $"VF{Interlocked.Increment(ref _codeSequence):D4}";
        var response = await client.PostAsJsonAsync("/api/v1/forms", new
        {
            code,
            nameAr = "نموذج إصدارات",
            nameEn = (string?)null,
            description = "وصف نموذج لإصدارات التصميم",
            classification = 1,
            scopeType = ScopeType.Global,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<FormDetail>(body, JsonOptions)!;
    }

    private static string MinimalValidSchemaJson()
    {
        var pageId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        return JsonSerializer.Serialize(new
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
    }

    private sealed record VersionDetail(
        Guid Id,
        Guid FormDefinitionId,
        int VersionNumber,
        FormVersionStatus Status,
        string DraftSchemaJson,
        string? DraftSchemaHash,
        Guid? SnapshotId,
        string RowVersion);

    private sealed record SnapshotDto(Guid Id, string SchemaHash, string CanonicalSchemaJson);
}
