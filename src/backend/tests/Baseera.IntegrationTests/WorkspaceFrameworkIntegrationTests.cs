using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;

namespace Baseera.IntegrationTests;

public sealed class WorkspaceFrameworkIntegrationTests(BaseeraApiFactory factory) : IClassFixture<BaseeraApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [IntegrationConnectionFact]
    public async Task Reference_workspace_returns_authorized_real_widgets_for_scoped_user()
    {
        await factory.SeedUserAsync(
            "workspace-region-a",
            "منطقة أ",
            [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));
        var client = factory.CreateAuthenticatedClient("workspace-region-a");

        var response = await client.GetAsync("/api/v1/workspaces/reference?level=4");

        response.EnsureSuccessStatusCode();
        var shell = await response.Content.ReadFromJsonAsync<WorkspaceShellResponse>(JsonOptions);
        Assert.NotNull(shell);
        Assert.Equal("reference", shell!.Definition.Key);
        Assert.Contains(shell.WidgetDefinitions, widget => widget.Key == "dashboard.operational-summary");
        Assert.Contains(shell.Widgets, widget => widget.WidgetKey == "dashboard.operational-summary");
        Assert.All(shell.WidgetDefinitions, widget => Assert.False(string.IsNullOrWhiteSpace(widget.RequiredPermission)));
    }

    [IntegrationConnectionFact]
    public async Task Workspace_endpoint_requires_workspace_permission()
    {
        await factory.SeedUserAsync(
            "workspace-no-permission",
            "بلا صلاحية",
            [RoleCodes.FormRespondent],
            (ScopeType.Facility, null, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workspace-no-permission");

        var response = await client.GetAsync("/api/v1/workspaces/reference?level=4");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_endpoint_returns_bad_request_for_invalid_date_range()
    {
        await factory.SeedUserAsync(
            "workspace-invalid-date-range",
            "نطاق غير صحيح",
            [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));
        var client = factory.CreateAuthenticatedClient("workspace-invalid-date-range");

        var response = await client.GetAsync("/api/v1/workspaces/reference?level=4&fromUtc=2026-07-25T00:00:00Z&toUtc=2026-07-24T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("نطاق التاريخ غير صحيح.", json.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", body, StringComparison.OrdinalIgnoreCase);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_endpoint_returns_not_found_for_out_of_scope_facility()
    {
        await factory.SeedUserAsync(
            "workspace-out-of-scope-facility",
            "خارج النطاق",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, null, SeedIds.FacilityA1));
        var client = factory.CreateAuthenticatedClient("workspace-out-of-scope-facility");

        var response = await client.GetAsync($"/api/v1/workspaces/reference?level=1&facilityId={SeedIds.FacilityB1}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(string.IsNullOrWhiteSpace(body));
        Assert.DoesNotContain("FacilityB1", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("سجن", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record WorkspaceShellResponse(
        WorkspaceDefinitionResponse Definition,
        IReadOnlyList<WidgetDefinitionResponse> WidgetDefinitions,
        IReadOnlyList<WidgetEnvelopeResponse> Widgets);

    private sealed record WorkspaceDefinitionResponse(string Key);

    private sealed record WidgetDefinitionResponse(string Key, string? RequiredPermission);

    private sealed record WidgetEnvelopeResponse(string WidgetKey);
}
