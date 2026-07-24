using Baseera.Application.Abstractions;
using Baseera.Application.Common;
using Baseera.Application.Dashboard;
using Baseera.Application.Forms.Compliance;
using Baseera.Application.Notes;
using Baseera.Domain.Attachments;
using Baseera.Application.Workspaces;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, PermissionCodes.DashboardViewOperational);
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
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, PermissionCodes.DashboardViewOperational);
        var service = BuildService(user, new TestWidgetProvider("visible", PermissionCodes.DashboardViewOperational), new FailingWidgetProvider("fails"));

        var shell = await service.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
        Assert.True(shell!.IsPartial);
        Assert.Single(shell.Widgets);
        Assert.Single(shell.WidgetFailures);
        Assert.Equal("fails", shell.WidgetFailures[0].WidgetKey);
    }

    [Fact]
    public async Task Workspace_requires_all_definition_permissions()
    {
        var partialUser = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain);
        var partialService = BuildServiceWithDefinition(
            partialUser,
            new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain },
            new HashSet<string> { PermissionCodes.WorkspacesView, PermissionCodes.DashboardViewOperational },
            null,
            new TestWidgetProvider("visible", PermissionCodes.WorkspacesView));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => partialService.GetWorkspaceAsync(new WorkspaceRequest(
            "test",
            WorkspaceLevel.Domain,
            null,
            null,
            null,
            null,
            null,
            null,
            null)));

        var fullUser = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, PermissionCodes.DashboardViewOperational);
        var fullService = BuildServiceWithDefinition(
            fullUser,
            new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain },
            new HashSet<string> { PermissionCodes.WorkspacesView, PermissionCodes.DashboardViewOperational },
            null,
            new TestWidgetProvider("visible", PermissionCodes.WorkspacesView));

        var shell = await fullService.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
    }

    [Fact]
    public async Task Domain_level_requires_domain_permission()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView);
        var service = BuildService(user, new TestWidgetProvider("visible", PermissionCodes.WorkspacesView));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetWorkspaceAsync(new WorkspaceRequest(
            "test",
            WorkspaceLevel.Domain,
            null,
            null,
            null,
            null,
            null,
            null,
            null)));
    }

    [Fact]
    public async Task Sensitive_flag_uses_only_authorized_widget_definitions()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, "Sensitive.View", "Normal.View");
        var service = BuildService(
            user,
            new TestWidgetProvider("normal", "Normal.View"),
            new TestWidgetProvider("sensitive", "Sensitive.View", containsSensitiveData: true));

        var shell = await service.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
        Assert.True(shell!.Context.IncludesSensitiveData);
        Assert.All(shell.Widgets, widget => Assert.True(widget.ScopeSummary.IsSensitive));
    }

    [Fact]
    public async Task Unauthorized_sensitive_widgets_do_not_mark_workspace_sensitive()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, "Normal.View");
        var service = BuildService(
            user,
            new TestWidgetProvider("normal", "Normal.View"),
            new TestWidgetProvider("hidden-sensitive", "Sensitive.View", containsSensitiveData: true));

        var shell = await service.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
        Assert.False(shell!.Context.IncludesSensitiveData);
        Assert.Single(shell.Widgets);
        Assert.False(shell.Widgets[0].ScopeSummary.IsSensitive);
    }

    [Fact]
    public async Task Widget_endpoint_uses_selected_widget_sensitivity()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, "Sensitive.View");
        var service = BuildService(user, new TestWidgetProvider("sensitive", "Sensitive.View", containsSensitiveData: true));

        var widget = await service.GetWidgetAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null), "sensitive");

        Assert.NotNull(widget);
        Assert.True(widget!.Data.ScopeSummary.IsSensitive);
    }

    [Fact]
    public async Task Widget_cancellation_is_not_recorded_as_partial_failure()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, PermissionCodes.DashboardViewOperational);
        var cancelled = new CancelledWidgetProvider("cancelled");
        var next = new CountingWidgetProvider("next");
        var service = BuildService(user, cancelled, next);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetWorkspaceAsync(new WorkspaceRequest(
            "test",
            WorkspaceLevel.Domain,
            null,
            null,
            null,
            null,
            null,
            null,
            null)));

        Assert.Equal(1, cancelled.LoadCount);
        Assert.Equal(0, next.LoadCount);
    }

    [Fact]
    public async Task Query_respects_configured_widget_budget()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, PermissionCodes.DashboardViewOperational);
        var service = BuildService(
            user,
            new WorkspaceFrameworkOptions { WidgetQueryBudget = 2 },
            new TestWidgetProvider("first", PermissionCodes.DashboardViewOperational),
            new TestWidgetProvider("second", PermissionCodes.DashboardViewOperational),
            new TestWidgetProvider("third", PermissionCodes.DashboardViewOperational));

        var shell = await service.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
        Assert.Equal(["first", "second"], shell!.Widgets.Select(widget => widget.WidgetKey).ToArray());
    }

    [Fact]
    public async Task Query_corrects_invalid_low_widget_budget()
    {
        var user = CurrentUser(PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewDomain, PermissionCodes.DashboardViewOperational);
        var service = BuildService(
            user,
            new WorkspaceFrameworkOptions { WidgetQueryBudget = 0 },
            new TestWidgetProvider("first", PermissionCodes.DashboardViewOperational),
            new TestWidgetProvider("second", PermissionCodes.DashboardViewOperational));

        var shell = await service.GetWorkspaceAsync(new WorkspaceRequest("test", WorkspaceLevel.Domain, null, null, null, null, null, null, null));

        Assert.NotNull(shell);
        Assert.Equal(["first"], shell!.Widgets.Select(widget => widget.WidgetKey).ToArray());
    }

    [Fact]
    public async Task Corrective_actions_drill_down_preserves_workspace_filters()
    {
        var provider = new CorrectiveActionsSummaryWorkspaceWidgetProvider(new FakeOperationalDashboardQueryService(), time);
        var context = new WorkspaceContext(
            "reference",
            WorkspaceLevel.Region,
            SeedIds.Organization,
            SeedIds.RegionA,
            SeedIds.FacilityA1,
            null,
            "region",
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero),
            "ar-SA",
            "Asia/Riyadh",
            new HashSet<string>(),
            false);

        var envelope = await provider.LoadAsync(context, CancellationToken.None);
        var target = Assert.Single(envelope.DrillDownTargets);

        Assert.Equal("2026-07-01T00:00:00.0000000+00:00", target.PreservedFilters["fromUtc"]);
        Assert.Equal("2026-07-24T00:00:00.0000000+00:00", target.PreservedFilters["toUtc"]);
        Assert.Equal(SeedIds.RegionA.ToString(), target.PreservedFilters["regionId"]);
        Assert.Equal(SeedIds.FacilityA1.ToString(), target.PreservedFilters["facilityId"]);
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
    public void Envelope_build_request_preserves_contract_values()
    {
        var context = new WorkspaceContext(
            "test",
            WorkspaceLevel.Domain,
            SeedIds.Organization,
            null,
            null,
            null,
            "global",
            time.GetUtcNow().AddDays(-1),
            time.GetUtcNow(),
            "ar-SA",
            "Asia/Riyadh",
            new HashSet<string> { PermissionCodes.WorkspacesView },
            false);
        var generatedAt = time.GetUtcNow();
        var payload = new { Count = 3 };

        var envelope = WorkspaceContractFactory.Envelope(WorkspaceContractFactory.BuildRequest(
            context,
            "widget.test",
            generatedAt,
            generatedAt,
            payload));

        Assert.Equal("widget.test", envelope.WidgetKey);
        Assert.Equal(generatedAt, envelope.GeneratedAtUtc);
        Assert.Equal(DataFreshnessStatus.Current, envelope.Freshness.Status);
        Assert.Equal(ConfidenceLevel.High, envelope.Confidence.Level);
        Assert.Same(payload, envelope.Payload);
        Assert.Empty(envelope.WarningMessages);
        Assert.Empty(envelope.AllowedActions);
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

    [Fact]
    public void Facility_workspace_definition_registers_facility_only_widgets()
    {
        var definition = new FacilityWorkspaceDefinitionProvider().Definition;

        Assert.Equal(FacilityWorkspaceDefinitionProvider.WorkspaceKey, definition.Key);
        Assert.Equal([WorkspaceLevel.Facility], definition.SupportedLevels.Order().ToArray());
        Assert.Contains(PermissionCodes.WorkspacesViewFacility, definition.RequiredPermissions);
        Assert.Contains(FacilityWorkspaceDefinitionProvider.PriorityQueueWidgetKey, definition.RegisteredWidgets);
        Assert.DoesNotContain(WorkspaceLevel.Region, definition.SupportedLevels);
        Assert.DoesNotContain(WorkspaceLevel.Headquarters, definition.SupportedLevels);
    }

    [Fact]
    public void Facility_widget_definition_spec_preserves_contract_values()
    {
        var definition = FacilityWorkspaceWidgetDefinitions.Create(new FacilityWorkspaceWidgetDefinitionSpec
        {
            Key = "facility.test",
            TitleAr = "اختبار",
            TitleEn = "Test",
            DescriptionAr = "وصف",
            Category = WidgetCategory.Risk,
            RequiredPermission = PermissionCodes.DashboardViewRisk,
            DataCapability = "Facility.Test",
            Size = WidgetSize.Large,
            Sensitive = true
        });

        Assert.Equal("facility.test", definition.Key);
        Assert.Equal("اختبار", definition.TitleAr);
        Assert.Equal("Test", definition.TitleEn);
        Assert.Equal("وصف", definition.DescriptionAr);
        Assert.Equal(WidgetCategory.Risk, definition.Category);
        Assert.Equal([WorkspaceLevel.Facility], definition.SupportedLevels.Order().ToArray());
        Assert.Equal(PermissionCodes.DashboardViewRisk, definition.RequiredPermission);
        Assert.Equal("Facility.Test", definition.RequiredDataCapability);
        Assert.Equal(WidgetSize.Large, definition.DefaultSize);
        Assert.Equal(WidgetSize.Small, definition.MinSize);
        Assert.Equal(WidgetSize.Wide, definition.MaxSize);
        Assert.Equal(60, definition.RefreshPolicy.MinimumRefreshSeconds);
        Assert.True(definition.RefreshPolicy.SupportsManualRefresh);
        Assert.Equal(300, definition.DataFreshnessPolicy.CurrentForSeconds);
        Assert.Equal(1800, definition.DataFreshnessPolicy.DelayedAfterSeconds);
        Assert.Equal(3600, definition.DataFreshnessPolicy.StaleAfterSeconds);
        Assert.Equal("لا توجد بيانات ضمن هذا السجن.", definition.EmptyErrorBehavior.EmptyMessageAr);
        Assert.Equal("تعذر تحميل اختبار.", definition.EmptyErrorBehavior.ErrorMessageAr);
        Assert.True(definition.EmptyErrorBehavior.AllowPartialFailure);
        Assert.True(definition.SupportsDrillDown);
        Assert.False(definition.IsConfigurable);
        Assert.True(definition.ContainsSensitiveData);
        Assert.True(definition.IsEnabled);
    }

    [Fact]
    public async Task Facility_metric_widgets_call_only_their_focused_read_operation()
    {
        var context = FacilityWorkspaceContext();
        var notesRead = new StrictFacilityReadService(FacilityReadMethod.NotesOverview);
        await new FacilityNotesOverviewWorkspaceWidgetProvider(notesRead, time).LoadAsync(context, CancellationToken.None);
        Assert.Equal(1, notesRead.NotesOverviewCalls);
        Assert.Equal(0, notesRead.MetricsCalls);

        var actionsRead = new StrictFacilityReadService(FacilityReadMethod.CorrectiveActions);
        await new FacilityCorrectiveActionsWorkspaceWidgetProvider(actionsRead, time).LoadAsync(context, CancellationToken.None);
        Assert.Equal(1, actionsRead.CorrectiveActionsCalls);
        Assert.Equal(0, actionsRead.MetricsCalls);

        var alertsRead = new StrictFacilityReadService(FacilityReadMethod.AlertsEscalations);
        await new FacilityAlertsEscalationsWorkspaceWidgetProvider(alertsRead, time).LoadAsync(context, CancellationToken.None);
        Assert.Equal(1, alertsRead.AlertsEscalationsCalls);
        Assert.Equal(0, alertsRead.MetricsCalls);

        var formsRead = new StrictFacilityReadService(FacilityReadMethod.FormCompliance);
        await new FacilityFormComplianceWorkspaceWidgetProvider(formsRead, time).LoadAsync(context, CancellationToken.None);
        Assert.Equal(1, formsRead.FormComplianceCalls);
        Assert.Equal(0, formsRead.MetricsCalls);
    }

    [Fact]
    public async Task Facility_executive_summary_is_the_only_metric_widget_calling_full_metrics()
    {
        var read = new StrictFacilityReadService(FacilityReadMethod.Metrics);

        await new FacilityExecutiveSummaryWorkspaceWidgetProvider(read, time).LoadAsync(FacilityWorkspaceContext(), CancellationToken.None);

        Assert.Equal(1, read.MetricsCalls);
        Assert.Equal(0, read.NotesOverviewCalls);
        Assert.Equal(0, read.CorrectiveActionsCalls);
        Assert.Equal(0, read.AlertsEscalationsCalls);
        Assert.Equal(0, read.FormComplianceCalls);
    }

    [Fact]
    public async Task Facility_form_compliance_drill_down_requires_facility_id()
    {
        var read = new StrictFacilityReadService(FacilityReadMethod.FormCompliance);
        var context = FacilityWorkspaceContext(omitFacilityId: true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new FacilityFormComplianceWorkspaceWidgetProvider(read, time).LoadAsync(context, CancellationToken.None));

        Assert.Equal(0, read.FormComplianceCalls);
    }

    [Fact]
    public async Task Facility_corrective_actions_average_closure_uses_only_valid_completed_dates()
    {
        var user = NoteTestFixtures.AddUser(db, "facility-average");
        NoteTestFixtures.GrantPermissions(
            db,
            user.Id,
            "FacilityAverage",
            PermissionCodes.NotesView,
            PermissionCodes.CorrectiveActionsView);
        var note = NoteTestFixtures.NewNote(
            ScopeType.Facility,
            user.Id,
            SeedIds.RegionA,
            SeedIds.FacilityA1,
            status: Baseera.Domain.Notes.NoteStatus.Open);
        db.OperationalNotes.Add(note);
        var submitted = new DateTimeOffset(2026, 7, 24, 4, 0, 0, TimeSpan.Zero);
        db.CorrectiveActions.AddRange(
            CorrectiveAction(note.Id, user.Id, "CA-AVG-1", submitted, submitted.AddHours(4)),
            CorrectiveAction(note.Id, user.Id, "CA-AVG-2", submitted, null),
            CorrectiveAction(note.Id, user.Id, "CA-AVG-3", submitted, submitted.AddHours(-1)));
        db.SaveChanges();
        var currentUser = new FakeCurrentUser(
            true,
            user.Id,
            "facility-average",
            "facility-average",
            [PermissionCodes.NotesView, PermissionCodes.CorrectiveActionsView],
            [new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);
        var readService = new FacilityWorkspaceReadService(
            db,
            currentUser,
            new OperationalDashboardFilterBuilder(
                db,
                currentUser,
                new NoteScopeService(new Baseera.Application.Security.OrganizationalScopeService(currentUser, db), currentUser, db),
                new NoteTypeAccessService(db, currentUser)),
            new FakeOperationalDashboardQueryService(),
            new FakeFormComplianceQueryService(),
            time);

        var payload = await readService.GetCorrectiveActionsAsync(FacilityWorkspaceContext(), CancellationToken.None);

        Assert.Equal(4, payload.AverageClosureHours);
    }

    [Fact]
    public async Task Facility_workspace_rejects_unsupported_region_level()
    {
        var user = new FakeCurrentUser(
            true,
            Guid.NewGuid(),
            "facility-workspace-user",
            "facility-workspace-user",
            [PermissionCodes.WorkspacesView, PermissionCodes.WorkspacesViewFacility],
            [new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);
        var workspace = new FacilityWorkspaceDefinitionProvider();
        var registry = new WorkspaceRegistry([workspace], [], NullLogger<WorkspaceRegistry>.Instance);
        var service = new WorkspaceQueryService(
            registry,
            new WorkspaceContextResolver(db, user, new Baseera.Application.Security.OrganizationalScopeService(user, db), time),
            user,
            time,
            NullLogger<WorkspaceQueryService>.Instance,
            Options.Create(new WorkspaceFrameworkOptions()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetWorkspaceAsync(new WorkspaceRequest(
            FacilityWorkspaceDefinitionProvider.WorkspaceKey,
            WorkspaceLevel.Region,
            SeedIds.RegionA,
            null,
            null,
            null,
            null,
            null,
            null)));
    }

    [Fact]
    public void Facility_summary_rules_classify_critical_from_escalations()
    {
        var metrics = FacilityMetrics(
            notes: new FacilityNotesOverviewPayload(2, 0, 0, 0, 0, 1, []),
            actions: new FacilityCorrectiveActionsPayload(1, 0, 0, 0, 0, 0, null),
            alerts: new FacilityAlertsEscalationsPayload(0, 1, 1, 1, null),
            forms: new FacilityFormCompliancePayload
            {
                TargetedForms = 1,
                CompletedForms = 1,
                RemainingForms = 0,
                OverdueForms = 0,
                CompletionRate = 1,
                NotStartedForms = 0,
                PendingReviewForms = 0
            });

        var status = FacilityWorkspaceRules.ClassifyStatus(metrics);

        Assert.Equal("critical", status.Code);
        Assert.Equal("التصعيدات الحرجة: 1", FacilityWorkspaceRules.TopDriver(metrics));
    }

    [Fact]
    public void Facility_summary_rules_report_medium_confidence_without_form_targets()
    {
        var metrics = FacilityMetrics(
            notes: new FacilityNotesOverviewPayload(0, 0, 0, 0, 0, 0, []),
            actions: new FacilityCorrectiveActionsPayload(0, 0, 0, 0, 0, 0, null),
            alerts: new FacilityAlertsEscalationsPayload(0, 0, 0, 0, null),
            forms: new FacilityFormCompliancePayload
            {
                TargetedForms = 0,
                CompletedForms = 0,
                RemainingForms = 0,
                OverdueForms = 0,
                NotStartedForms = 0,
                PendingReviewForms = 0
            });

        Assert.Equal(ConfidenceLevel.Medium, FacilityWorkspaceRules.Confidence(metrics));
        Assert.NotEmpty(FacilityWorkspaceRules.ConfidenceReasons(metrics));
    }

    private WorkspaceQueryService BuildService(ICurrentUser user, params IWorkspaceWidgetProvider[] widgets)
    {
        return BuildServiceWithDefinition(user, new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain }, new HashSet<string> { PermissionCodes.WorkspacesView }, null, widgets);
    }

    private WorkspaceQueryService BuildService(ICurrentUser user, WorkspaceFrameworkOptions options, params IWorkspaceWidgetProvider[] widgets)
    {
        return BuildServiceWithDefinition(user, new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain }, new HashSet<string> { PermissionCodes.WorkspacesView }, options, widgets);
    }

    private WorkspaceQueryService BuildService(ICurrentUser user, IReadOnlySet<WorkspaceLevel> levels, params IWorkspaceWidgetProvider[] widgets)
    {
        return BuildServiceWithDefinition(user, levels, new HashSet<string> { PermissionCodes.WorkspacesView }, null, widgets);
    }

    private WorkspaceQueryService BuildServiceWithDefinition(
        ICurrentUser user,
        IReadOnlySet<WorkspaceLevel> levels,
        IReadOnlySet<string> requiredPermissions,
        WorkspaceFrameworkOptions? options,
        params IWorkspaceWidgetProvider[] widgets)
    {
        var workspace = new TestWorkspaceProvider(widgets.Select(widget => widget.Definition.Key).ToArray(), levels, requiredPermissions);
        var registry = new WorkspaceRegistry([workspace], widgets, NullLogger<WorkspaceRegistry>.Instance);
        var scope = new Baseera.Application.Security.OrganizationalScopeService(user, db);
        return new WorkspaceQueryService(
            registry,
            new WorkspaceContextResolver(db, user, scope, time),
            user,
            time,
            NullLogger<WorkspaceQueryService>.Instance,
            Options.Create(options ?? new WorkspaceFrameworkOptions()));
    }

    private static ICurrentUser CurrentUser(params string[] permissions) => new FakeCurrentUser(
        true,
        Guid.NewGuid(),
        "workspace-user",
        "workspace-user",
        permissions,
        [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);

    private static FacilityWorkspaceMetrics FacilityMetrics(
        FacilityNotesOverviewPayload notes,
        FacilityCorrectiveActionsPayload actions,
        FacilityAlertsEscalationsPayload alerts,
        FacilityFormCompliancePayload forms) =>
        new(
            new FacilityWorkspaceFacilityInfo(SeedIds.FacilityA1, "سجن أ1", SeedIds.RegionA, "منطقة أ", "سجن"),
            notes,
            actions,
            alerts,
            forms);

    private static WorkspaceContext FacilityWorkspaceContext(Guid? facilityId = null, bool omitFacilityId = false) =>
        new(
            FacilityWorkspaceDefinitionProvider.WorkspaceKey,
            WorkspaceLevel.Facility,
            SeedIds.Organization,
            SeedIds.RegionA,
            omitFacilityId ? null : facilityId ?? SeedIds.FacilityA1,
            null,
            "سجن أ1",
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero),
            "ar-SA",
            "Asia/Riyadh",
            new HashSet<string>(),
            false);

    private static CorrectiveAction CorrectiveAction(
        Guid noteId,
        Guid userId,
        string reference,
        DateTimeOffset submittedAtUtc,
        DateTimeOffset? completedAtUtc) =>
        new()
        {
            OperationalNoteId = noteId,
            ReferenceNumber = reference,
            Title = reference,
            Description = "اختبار",
            Status = CorrectiveActionStatus.Completed,
            Priority = CorrectiveActionPriority.Medium,
            Classification = ClassificationLevel.Internal,
            CreatedByUserId = userId,
            SubmittedAtUtc = submittedAtUtc,
            CompletedAtUtc = completedAtUtc
        };

    private enum FacilityReadMethod
    {
        Facility,
        Metrics,
        NotesOverview,
        CorrectiveActions,
        AlertsEscalations,
        FormCompliance,
        PriorityQueue,
        RecentActivity
    }

    private sealed class StrictFacilityReadService(FacilityReadMethod allowed) : IFacilityWorkspaceReadService
    {
        public int MetricsCalls { get; private set; }
        public int NotesOverviewCalls { get; private set; }
        public int CorrectiveActionsCalls { get; private set; }
        public int AlertsEscalationsCalls { get; private set; }
        public int FormComplianceCalls { get; private set; }

        public Task<FacilityWorkspaceFacilityInfo> GetFacilityAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            EnsureAllowed(FacilityReadMethod.Facility);
            return Task.FromResult(new FacilityWorkspaceFacilityInfo(SeedIds.FacilityA1, "سجن أ1", SeedIds.RegionA, "منطقة أ", "سجن"));
        }

        public Task<FacilityWorkspaceMetrics> GetMetricsAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            MetricsCalls += 1;
            EnsureAllowed(FacilityReadMethod.Metrics);
            return Task.FromResult(FacilityMetrics(
                new FacilityNotesOverviewPayload(0, 0, 0, 0, 0, 0, []),
                new FacilityCorrectiveActionsPayload(0, 0, 0, 0, 0, 0, null),
                new FacilityAlertsEscalationsPayload(0, 0, 0, 0, null),
                EmptyFormCompliance()));
        }

        public Task<FacilityNotesOverviewPayload> GetNotesOverviewAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            NotesOverviewCalls += 1;
            EnsureAllowed(FacilityReadMethod.NotesOverview);
            return Task.FromResult(new FacilityNotesOverviewPayload(1, 0, 0, 0, 0, 1, []));
        }

        public Task<FacilityCorrectiveActionsPayload> GetCorrectiveActionsAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            CorrectiveActionsCalls += 1;
            EnsureAllowed(FacilityReadMethod.CorrectiveActions);
            return Task.FromResult(new FacilityCorrectiveActionsPayload(1, 0, 0, 0, 0, 0, null));
        }

        public Task<FacilityAlertsEscalationsPayload> GetAlertsEscalationsAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            AlertsEscalationsCalls += 1;
            EnsureAllowed(FacilityReadMethod.AlertsEscalations);
            return Task.FromResult(new FacilityAlertsEscalationsPayload(0, 1, 0, 0, null));
        }

        public Task<FacilityFormCompliancePayload> GetFormComplianceAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            FormComplianceCalls += 1;
            EnsureAllowed(FacilityReadMethod.FormCompliance);
            return Task.FromResult(EmptyFormCompliance());
        }

        public Task<FacilityPriorityQueuePayload> GetPriorityQueueAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            EnsureAllowed(FacilityReadMethod.PriorityQueue);
            return Task.FromResult(new FacilityPriorityQueuePayload(10, []));
        }

        public Task<FacilityRecentActivityPayload> GetRecentActivityAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            EnsureAllowed(FacilityReadMethod.RecentActivity);
            return Task.FromResult(new FacilityRecentActivityPayload(10, []));
        }

        private void EnsureAllowed(FacilityReadMethod actual)
        {
            if (allowed != actual)
            {
                throw new InvalidOperationException($"Unexpected read method {actual}; expected {allowed}.");
            }
        }

        private static FacilityFormCompliancePayload EmptyFormCompliance() =>
            new()
            {
                TargetedForms = 0,
                CompletedForms = 0,
                RemainingForms = 0,
                OverdueForms = 0,
                NotStartedForms = 0,
                PendingReviewForms = 0
            };
    }

    private sealed class TestWorkspaceProvider(
        IReadOnlyList<string> widgetKeys,
        IReadOnlySet<WorkspaceLevel>? levels = null,
        IReadOnlySet<string>? requiredPermissions = null) : IWorkspaceDefinitionProvider
    {
        public WorkspaceDefinition Definition { get; } = new(
            "test",
            "اختبار",
            "Test",
            levels ?? new HashSet<WorkspaceLevel> { WorkspaceLevel.Domain },
            requiredPermissions ?? new HashSet<string> { PermissionCodes.WorkspacesView },
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
        IReadOnlySet<WorkspaceLevel>? levels = null,
        bool containsSensitiveData = false) : IWorkspaceWidgetProvider
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
            containsSensitiveData,
            true);

        public virtual Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            var generatedAt = new DateTimeOffset(2026, 7, 24, 9, 0, 0, TimeSpan.Zero);
            return Task.FromResult(WorkspaceContractFactory.Envelope(WorkspaceContractFactory.BuildRequest(
                context,
                Definition.Key,
                generatedAt,
                generatedAt,
                new { Count = 1 })));
        }
    }

    private sealed class FailingWidgetProvider(string key) : TestWidgetProvider(key, PermissionCodes.DashboardViewOperational)
    {
        public override Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("safe failure");
        }
    }

    private sealed class CancelledWidgetProvider(string key) : TestWidgetProvider(key, PermissionCodes.DashboardViewOperational)
    {
        public int LoadCount { get; private set; }

        public override Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            LoadCount += 1;
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class CountingWidgetProvider(string key) : TestWidgetProvider(key, PermissionCodes.DashboardViewOperational)
    {
        public int LoadCount { get; private set; }

        public override Task<WidgetDataEnvelopeDto> LoadAsync(WorkspaceContext context, CancellationToken cancellationToken)
        {
            LoadCount += 1;
            return base.LoadAsync(context, cancellationToken);
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeFormComplianceQueryService : IFormComplianceQueryService
    {
        public Task<FormComplianceSummaryDto> GetSummaryAsync(FormComplianceQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FormComplianceSummaryDto
            {
                TargetedAssignmentCount = 0,
                DistinctFacilityCount = 0,
                UnavailableAssignmentCount = 0,
                EligibleAssignmentCount = 0,
                CompletedCount = 0,
                RemainingCount = 0,
                CompletionRate = null,
                NotStartedCount = 0,
                DraftCount = 0,
                SubmittedCount = 0,
                UnderReviewCount = 0,
                ReturnedCount = 0,
                ApprovedCount = 0,
                RejectedCount = 0,
                ClosedCount = 0,
                OverdueCount = 0,
                CompletedOnTimeCount = 0,
                CompletedLateCount = 0,
                AverageCompletionMinutes = null,
                UnknownCompletionTimestampCount = 0,
                InvalidCompletionDurationCount = 0,
                StatusBucketTotal = 0,
                StatusReconciliationValid = true,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            });
        }

        public Task<PagedResult<FormComplianceRegionRowDto>> GetRegionsAsync(FormComplianceQuery query, CancellationToken cancellationToken = default) =>
            EmptyPage<FormComplianceRegionRowDto>(query);

        public Task<PagedResult<FormComplianceFacilityRowDto>> GetFacilitiesAsync(FormComplianceQuery query, CancellationToken cancellationToken = default) =>
            EmptyPage<FormComplianceFacilityRowDto>(query);

        public Task<PagedResult<FormComplianceCycleRowDto>> GetCyclesAsync(FormComplianceQuery query, CancellationToken cancellationToken = default) =>
            EmptyPage<FormComplianceCycleRowDto>(query);

        public Task<PagedResult<FormCompliancePendingItemDto>> GetPendingAsync(FormComplianceQuery query, CancellationToken cancellationToken = default) =>
            EmptyPage<FormCompliancePendingItemDto>(query);

        public Task<IReadOnlyList<FormComplianceTrendPointDto>> GetTrendAsync(FormComplianceQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FormComplianceTrendPointDto>>([]);

        public Task<FormComplianceExportResult> ExportCsvAsync(FormComplianceQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FormComplianceExportResult("empty.csv", "text/csv", [], 0));

        private static Task<PagedResult<T>> EmptyPage<T>(FormComplianceQuery query) =>
            Task.FromResult(new PagedResult<T>
            {
                Items = [],
                Page = query.Page ?? 1,
                PageSize = query.PageSize ?? 20,
                TotalCount = 0
            });
    }

    private sealed class FakeOperationalDashboardQueryService : IOperationalDashboardQueryService
    {
        public Task<OperationalDashboardSummaryDto> GetSummaryAsync(OperationalDashboardQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OperationalDashboardSummaryDto(
                null,
                null,
                new OperationalDashboardCorrectiveActionsSummaryDto(1, 2, 3, 4, 5),
                null,
                query.FromUtc ?? DateTimeOffset.MinValue,
                query.ToUtc ?? DateTimeOffset.MinValue,
                7));
        }

        public Task<OperationalDashboardTrendsDto> GetTrendsAsync(OperationalDashboardQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OperationalDashboardBreakdownsDto> GetBreakdownsAsync(OperationalDashboardQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<OperationalDashboardPriorityQueuesDto> GetPriorityQueuesAsync(OperationalDashboardQuery query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
