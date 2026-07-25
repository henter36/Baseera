using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class ResourceReadinessIntegrationTests(BaseeraApiFactory factory) : IClassFixture<BaseeraApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [IntegrationConnectionFact]
    public async Task Facility_resource_summary_uses_seeded_assets_and_requirements()
    {
        await factory.SeedUserAsync(
            "resources-facility-director",
            "مدير موارد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-facility-director");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/summary");

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<ResourceSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(6, summary!.TotalRegistered);
        Assert.Equal(17, summary.Required);
        Assert.True(summary.Gap > 0);
        Assert.True(summary.MissionCriticalUnavailable > 0);
        Assert.Contains(summary.Warnings, warning => warning.Contains("موارد حرجة", StringComparison.Ordinal));
    }

    [IntegrationConnectionFact]
    public async Task Resource_summary_requires_resource_permission()
    {
        await factory.SeedUserAsync(
            "resources-no-permission",
            "بلا موارد",
            [RoleCodes.FormRespondent],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-no-permission");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Resource_assets_return_not_found_outside_facility_scope()
    {
        await factory.SeedUserAsync(
            "resources-out-scope",
            "خارج الموارد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-out-scope");

        var response = await client.GetAsync($"/api/v1/facilities/{SeedIds.FacilityB1}/resources/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workspace_contains_resource_widget_when_domain_permission_exists()
    {
        await factory.SeedUserAsync(
            "resources-workspace",
            "مساحة موارد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-workspace");

        var response = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");

        response.EnsureSuccessStatusCode();
        var shell = await response.Content.ReadFromJsonAsync<WorkspaceShellResponse>(JsonOptions);
        Assert.NotNull(shell);
        Assert.Contains(shell!.WidgetDefinitions, widget => widget.Key == "facility.resources");
        Assert.Contains(shell.Widgets, widget => widget.WidgetKey == "facility.resources");
    }

    [IntegrationConnectionFact]
    public async Task Created_resource_is_facility_scoped_and_audited()
    {
        await factory.SeedUserAsync(
            "resources-create",
            "إنشاء مورد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("resources-create");
        var body = new
        {
            resourceType = 1,
            assetCode = $"COM-TST-{Guid.NewGuid():N}"[..16],
            displayName = "جهاز اتصال اختبار",
            ownershipOrganizationId = SeedIds.Organization,
            currentStatus = 0,
            condition = 1,
            criticality = 2,
            sourceType = 0,
            sourceReference = "integration-test"
        };

        var response = await client.PostAsJsonAsync($"/api/v1/facilities/{SeedIds.FacilityA1}/resources/assets", body);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateResponse>(JsonOptions);
        Assert.NotNull(result);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var asset = await db.ResourceAssets.AsNoTracking().FirstAsync(a => a.Id == result!.Id);
        Assert.Equal(SeedIds.FacilityA1, asset.OperationalFacilityId);
        Assert.Equal(SeedIds.Organization, asset.OwnershipOrganizationId);
        Assert.True(await db.ResourceStatusEvents.AnyAsync(e => e.ResourceAssetId == asset.Id));
        Assert.True(await db.ResourcePlacements.AnyAsync(p => p.ResourceAssetId == asset.Id && p.EffectiveToUtc == null));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "ResourceCreated" && a.EntityId == asset.Id.ToString()));
    }

    private sealed record ResourceSummaryResponse(
        int TotalRegistered,
        int Required,
        int Gap,
        int MissionCriticalUnavailable,
        IReadOnlyList<string> Warnings);

    private sealed record WorkspaceShellResponse(
        IReadOnlyList<WidgetDefinitionResponse> WidgetDefinitions,
        IReadOnlyList<WidgetEnvelopeResponse> Widgets);

    private sealed record WidgetDefinitionResponse(string Key);

    private sealed record WidgetEnvelopeResponse(string WidgetKey);

    private sealed record CreateResponse(Guid Id);
}
