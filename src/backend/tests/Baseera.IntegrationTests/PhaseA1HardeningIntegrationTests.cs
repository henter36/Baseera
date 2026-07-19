using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.IntegrationTests;

public sealed class PhaseA1HardeningIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;

    public PhaseA1HardeningIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Inactive_user_is_forbidden()
    {
        await _factory.SeedUserAsync("inactive-user", "معطل", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SetUserProvisioningAsync("inactive-user", active: false, UserProvisioningStatus.Active);

        var client = _factory.CreateAuthenticatedClient("inactive-user");
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Pending_user_is_forbidden()
    {
        await _factory.SeedUserAsync("pending-user", "معلق", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SetUserProvisioningAsync("pending-user", active: true, UserProvisioningStatus.Pending);

        var client = _factory.CreateAuthenticatedClient("pending-user");
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Archived_user_is_forbidden()
    {
        await _factory.SeedUserAsync("archived-user", "مؤرشف", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.ArchiveUserAsync("archived-user");

        var client = _factory.CreateAuthenticatedClient("archived-user");
        var response = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Headquarters_scope_does_not_list_all_facilities_like_global()
    {
        await _factory.SeedUserAsync("hq-only", "رئيسي", [RoleCodes.HeadquartersExecutive],
            (ScopeType.Headquarters, null, null));

        var client = _factory.CreateAuthenticatedClient("hq-only");
        var list = await client.GetFromJsonAsync<PagedEnvelope<FacilityItem>>("/api/v1/facilities?page=1&pageSize=50");
        Assert.NotNull(list);
        Assert.Empty(list!.Items);
    }

    [IntegrationConnectionFact]
    public async Task Regional_auditor_cannot_read_national_audit_logs()
    {
        await _factory.SeedUserWithPermissionsAsync(
            "region-auditor",
            "مدقق منطقة",
            [RoleCodes.RegionalDirector],
            [PermissionCodes.AuditView],
            (ScopeType.Region, SeedIds.RegionA, null));

        var client = _factory.CreateAuthenticatedClient("region-auditor");
        var response = await client.GetAsync("/api/v1/audit-logs?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Upload_to_missing_entity_returns_not_found()
    {
        await _factory.SeedUserAsync("attach-global", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("attach-global");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(Guid.NewGuid().ToString()), "entityId");
        content.Add(new StringContent("Facility"), "entityType");
        content.Add(new StringContent("Internal"), "classification");
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("x"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "x.txt");

        var response = await client.PostAsync("/api/v1/attachments", content);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Upload_outside_scope_returns_not_found()
    {
        await _factory.SeedUserAsync("a1-uploader", "رافع أ1", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = _factory.CreateAuthenticatedClient("a1-uploader");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(SeedIds.FacilityB1.ToString()), "entityId");
        content.Add(new StringContent("Facility"), "entityType");
        content.Add(new StringContent("Internal"), "classification");
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("x"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "x.txt");

        var response = await client.PostAsync("/api/v1/attachments", content);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Facility_pagination_total_does_not_include_out_of_scope()
    {
        await _factory.SeedUserAsync("a1-pager", "صفحات", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = _factory.CreateAuthenticatedClient("a1-pager");
        var list = await client.GetFromJsonAsync<PagedEnvelope<FacilityItem>>("/api/v1/facilities?page=1&pageSize=50");
        Assert.NotNull(list);
        Assert.Equal(list!.Items.Count, list.TotalCount);
        Assert.DoesNotContain(list.Items, f => f.Id == SeedIds.FacilityB1);
    }

    [IntegrationConnectionFact]
    public async Task Facility_search_does_not_leak_out_of_scope()
    {
        await _factory.SeedUserAsync("a1-search", "بحث", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = _factory.CreateAuthenticatedClient("a1-search");
        var list = await client.GetFromJsonAsync<PagedEnvelope<FacilityItem>>(
            "/api/v1/facilities?page=1&pageSize=50&search=" + Uri.EscapeDataString("ب-1"));
        Assert.NotNull(list);
        Assert.Empty(list!.Items);
        Assert.Equal(0, list.TotalCount);
    }

    [IntegrationConnectionFact]
    public async Task Assign_role_to_missing_user_returns_not_found()
    {
        await _factory.SeedUserAsync("role-admin", "أدوار", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("role-admin");
        var response = await client.PostAsJsonAsync(
            $"/api/v1/users/{Guid.NewGuid()}/roles",
            new { roleCode = RoleCodes.ReadOnlyUser, reason = "اختبار" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Assign_scope_to_missing_user_returns_not_found()
    {
        await _factory.SeedUserAsync("scope-admin", "نطاقات", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("scope-admin");
        var response = await client.PostAsJsonAsync(
            $"/api/v1/users/{Guid.NewGuid()}/scopes",
            new { scopeType = ScopeType.Global, reason = "اختبار" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
