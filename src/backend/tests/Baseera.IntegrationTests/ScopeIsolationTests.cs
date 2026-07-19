using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.IntegrationTests;

public sealed class ScopeIsolationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;

    public ScopeIsolationTests(BaseeraApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Facility_user_cannot_see_other_facility_by_id()
    {
        await _factory.SeedUserAsync(
            "facility-a1-user",
            "مستخدم سجن أ1",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var client = _factory.CreateAuthenticatedClient("facility-a1-user");

        var allowed = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        var denied = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityB1}");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        var listResponse = await client.GetAsync("/api/v1/facilities?page=1&pageSize=50");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.True(listResponse.IsSuccessStatusCode, listBody);
        var list = System.Text.Json.JsonSerializer.Deserialize<PagedEnvelope<FacilityItem>>(listBody, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(list);
        Assert.All(list!.Items, f => Assert.Equal(SeedIds.FacilityA1, f.Id));
        Assert.DoesNotContain(list.Items, f => f.Id == SeedIds.FacilityB1);
    }

    [Fact]
    public async Task Regional_director_sees_only_own_region_facilities()
    {
        await _factory.SeedUserAsync(
            "region-a-director",
            "مدير منطقة أ",
            [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));

        var client = _factory.CreateAuthenticatedClient("region-a-director");
        var list = await client.GetFromJsonAsync<PagedEnvelope<FacilityItem>>("/api/v1/facilities?page=1&pageSize=50");
        Assert.NotNull(list);
        Assert.Contains(list!.Items, f => f.Id == SeedIds.FacilityA1);
        Assert.Contains(list.Items, f => f.Id == SeedIds.FacilityA2);
        Assert.DoesNotContain(list.Items, f => f.Id == SeedIds.FacilityB1);
    }

    [Fact]
    public async Task Headquarters_global_scope_sees_all_regions()
    {
        await _factory.SeedUserAsync(
            "hq-exec",
            "تنفيذي رئيسي",
            [RoleCodes.HeadquartersExecutive],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient("hq-exec");
        var regions = await client.GetFromJsonAsync<PagedEnvelope<RegionItem>>("/api/v1/regions?page=1&pageSize=50");
        Assert.NotNull(regions);
        Assert.True(regions!.TotalCount >= 2);
        Assert.Contains(regions.Items, r => r.Id == SeedIds.RegionA);
        Assert.Contains(regions.Items, r => r.Id == SeedIds.RegionB);
    }

    [Fact]
    public async Task Facility_user_cannot_create_facility_in_other_region()
    {
        await _factory.SeedUserAsync(
            "facility-a1-manager",
            "مدير سجن",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        // SystemAdministrator has Manage permission but scope is facility-only — still blocked by scope.
        var client = _factory.CreateAuthenticatedClient("facility-a1-manager");
        var response = await client.PostAsJsonAsync("/api/v1/facilities", new
        {
            regionId = SeedIds.RegionB,
            code = "FAC-HACK",
            nameAr = "محاولة اختراق نطاق",
            facilityType = "Prison"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public sealed class AuditAndAttachmentTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;

    public AuditAndAttachmentTests(BaseeraApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Updating_region_writes_audit_log()
    {
        await _factory.SeedUserAsync(
            "admin-audit",
            "مسؤول",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient("admin-audit");
        var region = await client.GetFromJsonAsync<RegionItem>($"/api/v1/regions/{SeedIds.RegionA}");
        Assert.NotNull(region);

        var update = await client.PutAsJsonAsync($"/api/v1/regions/{SeedIds.RegionA}", new
        {
            nameAr = "منطقة أ محدّثة",
            isActive = true,
            rowVersion = region!.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var audits = await client.GetFromJsonAsync<PagedEnvelope<AuditItem>>("/api/v1/audit-logs?module=Organization&page=1&pageSize=20");
        Assert.NotNull(audits);
        Assert.Contains(audits!.Items, a => a.Action == "Update" && a.EntityId == SeedIds.RegionA.ToString());
    }

    [Fact]
    public async Task Optimistic_concurrency_rejects_stale_rowversion()
    {
        await _factory.SeedUserAsync(
            "admin-concurrency",
            "مسؤول",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient("admin-concurrency");
        var region = await client.GetFromJsonAsync<RegionItem>($"/api/v1/regions/{SeedIds.RegionA}");
        Assert.NotNull(region);

        var first = await client.PutAsJsonAsync($"/api/v1/regions/{SeedIds.RegionA}", new
        {
            nameAr = "تحديث أول",
            isActive = true,
            rowVersion = region!.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var stale = await client.PutAsJsonAsync($"/api/v1/regions/{SeedIds.RegionA}", new
        {
            nameAr = "تحديث قديم",
            isActive = true,
            rowVersion = region.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Attachment_rejects_disallowed_content_type_and_logs_download()
    {
        await _factory.SeedUserAsync(
            "admin-attach",
            "مسؤول",
            [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient("admin-attach");

        using var badContent = new MultipartFormDataContent();
        badContent.Add(new StringContent(SeedIds.FacilityA1.ToString()), "entityId");
        badContent.Add(new StringContent("Facility"), "entityType");
        badContent.Add(new ByteArrayContent("MZ executable"u8.ToArray())
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-msdownload") }
        }, "file", "malware.exe");

        var bad = await client.PostAsync("/api/v1/attachments", badContent);
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);

        using var goodContent = new MultipartFormDataContent();
        goodContent.Add(new StringContent(SeedIds.FacilityA1.ToString()), "entityId");
        goodContent.Add(new StringContent("Facility"), "entityType");
        goodContent.Add(new StringContent("Internal"), "classification");
        var bytes = Encoding.UTF8.GetBytes("محتوى تجريبي");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        goodContent.Add(fileContent, "file", "note.txt");

        var uploaded = await client.PostAsync("/api/v1/attachments", goodContent);
        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        var body = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();

        var download = await client.GetAsync($"/api/v1/attachments/{id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);

        var audits = await client.GetFromJsonAsync<PagedEnvelope<AuditItem>>("/api/v1/audit-logs?module=Attachments&page=1&pageSize=50");
        Assert.NotNull(audits);
        Assert.Contains(audits!.Items, a => a.Action == "Download" && a.EntityId == id.ToString());
    }

    [Fact]
    public async Task Audit_logs_have_no_update_endpoint()
    {
        await _factory.SeedUserAsync(
            "auditor-user",
            "مدقق",
            [RoleCodes.Auditor],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient("auditor-user");
        var response = await client.PutAsJsonAsync("/api/v1/audit-logs/00000000-0000-0000-0000-000000000001", new { action = "hack" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed record PagedEnvelope<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record FacilityItem(Guid Id, Guid RegionId, string Code, string NameAr);
public sealed record RegionItem(Guid Id, string Code, string NameAr, bool IsActive, string RowVersion);
public sealed record AuditItem(Guid Id, string Action, string Module, string? EntityId);
