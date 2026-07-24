using Baseera.Application.Abstractions;
using Baseera.Application.Workspaces;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baseera.UnitTests.Workspaces;

public sealed class WorkspaceFrameworkTests : IDisposable
{
    private readonly BaseeraDbContext db = NoteTestFixtures.CreateDb();
    private readonly MutableTimeProvider time = new(new DateTimeOffset(2026, 7, 24, 9, 0, 0, TimeSpan.Zero));

    public void Dispose() => db.Dispose();

    [Fact]
    public void Registry_rejects_duplicate_widget_keys()
    {
        var workspace = new TestWorkspaceProvider(["duplicate"]);
        var first = new TestWidgetProvider("duplicate", PermissionCodes.WorkspacesView);
        var second = new TestWidgetProvider("duplicate", PermissionCodes.WorkspacesView);

        Assert.Throws<InvalidOperationException>(() => new WorkspaceRegistry(
            [workspace],
            [first, second],
            NullLogger<WorkspaceRegistry>.Instance));
    }

    [Fact]
    public void Registry_rejects_widget_without_supported_workspace_level()
    {
        var workspace = new TestWorkspaceProvider(["facility-only"]);
        var widget = new TestWidgetProvider("facility-only", PermissionCodes.WorkspacesView, new HashSet<WorkspaceLevel> { WorkspaceLevel.Facility });

        Assert.Throws<InvalidOperationException>(() => new WorkspaceRegistry(
            [workspace],
            [widget],
            NullLogger<WorkspaceRegistry>.Instance));
    }

    [Fact]
    public async Task Query_filters_widgets_by_server_permissions()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.DashboardViewOperational);
        var service = BuildService(
            user,
            new TestWidgetProvider("visible", PermissionCodes.DashboardViewOperational),
            new TestWidgetProvider("hidden", PermissionCodes.DashboardViewRisk));

        var shell = await service.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
        Assert.Single(shell!.Widgets);
        Assert.Equal("visible", shell.Widgets[0].WidgetKey);
        Assert.DoesNotContain(shell.WidgetDefinitions, widget => widget.Key == "hidden");
    }

    [Fact]
    public async Task Query_returns_partial_shell_when_widget_fails_safely()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.DashboardViewOperational);
        var service = BuildService(user, new TestWidgetProvider("visible", PermissionCodes.DashboardViewOperational), new FailingWidgetProvider("fails"));

        var shell = await service.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
        Assert.True(shell!.IsPartial);
        Assert.Single(shell.Widgets);
        Assert.Single(shell.WidgetFailures);
        Assert.Equal("fails", shell.WidgetFailures[0].WidgetKey);
    }

    [Fact]
    public void Freshness_classification_is_server_authored()
    {
        var generated = new DateTimeOffset(2026, 7, 24, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal(DataFreshnessStatus.Current, WorkspaceContractFactory.Freshness(generated, generated.AddMinutes(-4)).Status);
        Assert.Equal(DataFreshnessStatus.Delayed, WorkspaceContractFactory.Freshness(generated, generated.AddMinutes(-20)).Status);
        Assert.Equal(DataFreshnessStatus.Stale, WorkspaceContractFactory.Freshness(generated, generated.AddMinutes(-40)).Status);
        Assert.Equal(DataFreshnessStatus.Unknown, WorkspaceContractFactory.Freshness(generated, null).Status);
    }

    [Fact]
    public async Task Context_rejects_out_of_scope_facility()
    {
        db.Facilities.Add(new Baseera.Domain.Organization.Facility
        {
            Id = SeedIds.FacilityA1,
            RegionId = SeedIds.RegionA,
            Code = "FAC-A",
            NameAr = "سجن أ"
        });
        db.SaveChanges();
        var user = new FakeCurrentUser(
            true,
            Guid.NewGuid(),
            "user",
            "user",
            [PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewFacility],
            [new UserScopeSnapshot(ScopeType.Facility, null, SeedIds.FacilityB1, null)]);
        var service = BuildService(
            user,
            new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain, WorkspaceLevel.Facility },
            new TestWidgetProvider("visible", PermissionCodes.WorkspacesView, new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain, WorkspaceLevel.Facility }));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetWorkspaceAsync(new WorkspaceRequest(
            "test",
            WorkspaceLevel.Facility,
            null,
            SeedIds.FacilityA1,
            null,
            null,
            null,
            null,
            null)));
    }

    private WorkspaceQueryService BuildService(ICurrentUser user, params IWorkspaceWidgetProvider[] widgets)
    {
        return BuildService(user, new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain }, widgets);
    }

    private WorkspaceQueryService BuildService(ICurrentUser user, IReadOnlySet<WorkspaceLevel> levels, params IWorkspaceWidgetProvider[] widgets)
    {
        var workspace = new TestWorkspaceProvider(widgets.Select(widget => widget.Definition.Key).ToArray(), levels);
        var registry = new WorkspaceRegistry([workspace], widgets, NullLogger<WorkspaceRegistry>.Instance);
        var scope = new Baseera.Application.Security.OrganizationalScopeService(user, db);
        return new WorkspaceQueryService(
            registry,
            new WorkspaceContextResolver(db, user, scope, time),
            user,
            time,
            NullLogger<WorkspaceQueryService>.Instance);
    }

    private static ICurrentUser CurrentUser(params string[] permissions) => new FakeCurrentUser(
        true,
        Guid.NewGuid(),
        "workspace-user",
        "workspace-user",
        permissions,
        [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);

    private sealed class TestWorkspaceProvider(IReadOnlyList<string> widgetKeys, IReadOnlySet<WorkspaceLevel>? levels = null) : IWorkspaceDefinitionProvider
    {
        public WorkspaceDefinition Definition { get; } = new(
            "test",
            "اختبار",
            "Test",
            levels ?? new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain },
            new HashSet<string> { PermissionCodes.WorkspacesView },
            widgetKeys,
            new WorkspaceLayoutDefinition([], 1),
            [],
            [],
            new WorkspaceFeatureAvailability(false, false, false, true),
            1);
    }

    private class TestWidgetProvider(
        string key,
        string requiredPermission,
        IReadOnlySet<WorkspaceLevel>? levels = null) : IWorkspaceWidgetProvider
    {
        public WidgetDefinition Definition { get; } = new(
            key,
            key,
            key,
            null,
            WidgetCategory.Summary,
            levels ?? new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain },
            requiredPermission,
            null,
            WidgetSize.Medium,
            WidgetSize.Small,
            WidgetSize.Wide,
            new WidgetRefreshPolicy(60, true),
            new WidgetDataFreshnessPolicy(300, 1800, 3600),
            new WidgetEmptyErrorBehavior("empty", "error", true),
            false,
            false,
            false,
            true);

        public virtual Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            var generatedAt = new DateTimeOffset(2026, 7, 24, 9, 0, 0, TimeSpan.Zero);
            return Task.FromResult(WorkspaceContractFactory.Envelope(context, Definition.Key, generatedAt, generatedAt, new { Count = 1 }));
        }
    }

    private sealed class FailingWidgetProvider(string key) : TestWidgetProvider(key, PermissionCodes.DashboardViewOperational)
    {
        public override Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("safe failure");
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
