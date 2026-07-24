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

    private sealed record WorkspaceShellResponse(
        WorkspaceDefinitionResponse Definition,
        IReadOnlyList<WidgetDefinitionResponse> WidgetDefinitions,
        IReadOnlyList<WidgetEnvelopeResponse> Widgets);

    private sealed record WorkspaceDefinitionResponse(string Key);

    private sealed record WidgetDefinitionResponse(string Key, string? RequiredPermission);

    private sealed record WidgetEnvelopeResponse(string WidgetKey);
}
