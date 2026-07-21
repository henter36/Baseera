using System.Reflection;
using Baseera.Application.Common;
using Baseera.Application.Abstractions;
using Baseera.Application.Security;
using Baseera.Application.Dashboard;
using Baseera.Application.Notes;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests.Dashboard;

public sealed class OperationalDashboardQueryServiceTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();
    private readonly MutableTimeProvider _time = new(FixedNow);

    public void Dispose() => _db.Dispose();

    private (IOperationalDashboardQueryService Service, Guid UserId) BuildService(
        params string[] permissions)
    {
        var user = NoteTestFixtures.AddUser(_db, "dashboard-viewer");
        return (BuildServiceForUser(user.Id, permissions), user.Id);
    }

    private IOperationalDashboardQueryService BuildServiceForUser(
        Guid userId,
        params string[] permissions)
    {
        NoteTestFixtures.GrantPermissions(_db, userId, $"Role-{userId}", permissions);
        var current = DashboardUser(userId, permissions);
        return BuildServiceWithCurrent(current);
    }

    private IOperationalDashboardQueryService BuildServiceWithMocks(
        ICurrentUser current,
        INoteScopeService noteScope,
        INoteTypeAccessService typeAccess) =>
        new OperationalDashboardQueryService(
            _db,
            current,
            new OperationalDashboardFilterBuilder(_db, current, noteScope, typeAccess),
            _time);

    private IOperationalDashboardQueryService BuildServiceWithCurrent(ICurrentUser current)
    {
        var orgScope = new OrganizationalScopeService(current, _db);
        var noteScope = new NoteScopeService(orgScope, current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        return new OperationalDashboardQueryService(
            _db,
            current,
            new OperationalDashboardFilterBuilder(_db, current, noteScope, typeAccess),
            _time);
    }

    private static ICurrentUser DashboardUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(
            true,
            userId,
            userId.ToString(),
            "dashboard-viewer",
            permissions,
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);

    private static string[] AllDashboardPermissions =>
    [
        PermissionCodes.DashboardViewOperational,
        PermissionCodes.DashboardViewRisk,
        PermissionCodes.DashboardViewRouting,
        PermissionCodes.DashboardViewCorrectiveActions,
        PermissionCodes.NotesViewSensitive
    ];

    private OperationalNote SeedNote(
        Guid reporterId,
        NoteStatus status = NoteStatus.Open,
        DateTimeOffset? dueAtUtc = null,
        ClassificationLevel classification = ClassificationLevel.Internal,
        Guid? regionId = null,
        Guid? noteTypeId = null,
        string reference = "OBS-00000001",
        bool isDeleted = false)
    {
        var note = NoteTestFixtures.NewNote(
            regionId.HasValue ? ScopeType.Region : ScopeType.Global,
            reporterId,
            regionId: regionId,
            status: status,
            classification: classification,
            reference: reference);
        note.NoteTypeId = noteTypeId ?? NoteTestFixtures.DefaultNoteTypeId;
        note.DueAtUtc = dueAtUtc;
        note.IsDeleted = isDeleted;
        if (isDeleted)
        {
            note.DeletedAtUtc = FixedNow.AddDays(-1);
            note.DeletedBy = reporterId.ToString();
        }

        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private void SeedCurrentAssignment(OperationalNote note, Guid assignedByUserId)
    {
        _db.NoteAssignments.Add(new NoteAssignment
        {
            OperationalNoteId = note.Id,
            AssignedByUserId = assignedByUserId,
            AssignedAtUtc = FixedNow.AddDays(-1),
            IsCurrent = true,
            Reason = "test"
        });
        _db.SaveChanges();
    }

    private void SeedRoutingDecision(
        OperationalNote note,
        NoteRoutingResultStatus resultStatus,
        DateTimeOffset? decidedAtUtc = null)
    {
        _db.NoteRoutingDecisions.Add(new NoteRoutingDecision
        {
            OperationalNoteId = note.Id,
            Trigger = NoteRoutingTrigger.Submit,
            AttemptNumber = 1,
            DecisionKey = Guid.NewGuid().ToString("N"),
            ResultStatus = resultStatus,
            DecidedAtUtc = decidedAtUtc ?? FixedNow.AddDays(-1)
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task GetSummary_requires_any_dashboard_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id);
        var service = BuildServiceForUser(reporter.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetSummaryAsync(new OperationalDashboardQuery()));
    }

    [Fact]
    public async Task GetSummary_open_count_includes_all_non_terminal_statuses()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000001");
        SeedNote(reporter.Id, NoteStatus.Assigned, reference: "OBS-00000002");
        SeedNote(reporter.Id, NoteStatus.InProgress, reference: "OBS-00000003");
        SeedNote(reporter.Id, NoteStatus.PendingVerification, reference: "OBS-00000004");
        SeedNote(reporter.Id, NoteStatus.Reopened, reference: "OBS-00000005");
        SeedNote(reporter.Id, NoteStatus.Closed, reference: "OBS-00000006");
        SeedNote(reporter.Id, NoteStatus.Cancelled, reference: "OBS-00000007");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(5, summary.Workload!.OpenTotal);
    }

    [Fact]
    public async Task GetSummary_overdue_count_includes_past_due_open_notes()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, NoteStatus.Open, dueAtUtc: FixedNow.AddDays(-2), reference: "OBS-00000001");
        SeedNote(reporter.Id, NoteStatus.InProgress, dueAtUtc: FixedNow.AddDays(-1), reference: "OBS-00000002");

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRisk);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Risk);
        Assert.Equal(2, summary.Risk!.Overdue);
    }

    [Fact]
    public async Task GetSummary_overdue_count_excludes_closed_and_future_due_notes()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, NoteStatus.Closed, dueAtUtc: FixedNow.AddDays(-3), reference: "OBS-00000001");
        SeedNote(reporter.Id, NoteStatus.Open, dueAtUtc: FixedNow.AddDays(3), reference: "OBS-00000002");
        SeedNote(reporter.Id, NoteStatus.Open, dueAtUtc: null, reference: "OBS-00000003");

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRisk);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Risk);
        Assert.Equal(0, summary.Risk!.Overdue);
    }

    [Fact]
    public async Task GetSummary_unassigned_count_includes_open_notes_without_current_assignment()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var unassigned = SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000001");
        var assigned = SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000002");
        SeedCurrentAssignment(assigned, reporter.Id);

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.Unassigned);
        Assert.Equal(unassigned.Id, (await _db.OperationalNotes
            .Where(note => note.Status != NoteStatus.Closed &&
                           note.Status != NoteStatus.Cancelled &&
                           !note.Assignments.Any(assignment => assignment.IsCurrent))
            .Select(note => note.Id)
            .ToListAsync()).Single());
    }

    [Fact]
    public async Task GetSummary_unassigned_count_excludes_closed_notes_even_without_assignment()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, NoteStatus.Closed, reference: "OBS-00000001");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(0, summary.Workload!.Unassigned);
    }

    [Fact]
    public async Task GetSummary_unassigned_count_treats_only_non_current_assignments_as_unassigned()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var note = SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000001");
        _db.NoteAssignments.Add(new NoteAssignment
        {
            OperationalNoteId = note.Id,
            AssignedByUserId = reporter.Id,
            AssignedAtUtc = FixedNow.AddDays(-2),
            EndedAtUtc = FixedNow.AddDays(-1),
            IsCurrent = false,
            Reason = "ended"
        });
        _db.SaveChanges();

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.Unassigned);
    }

    [Fact]
    public async Task GetSummary_requires_routing_count_includes_open_unassigned_without_decisions()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000001");
        var assigned = SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000002");
        SeedCurrentAssignment(assigned, reporter.Id);

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRouting);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.RequiresRouting);
        Assert.NotNull(summary.Routing);
        Assert.Equal(1, summary.Routing!.RequiresRouting);
    }

    [Fact]
    public async Task GetSummary_requires_routing_count_includes_reopened_with_failed_routing()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var note = SeedNote(reporter.Id, NoteStatus.Reopened, reference: "OBS-00000001");
        SeedRoutingDecision(note, NoteRoutingResultStatus.NoMatchingRule);

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRouting);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.RequiresRouting);
    }

    [Fact]
    public async Task GetSummary_requires_routing_count_excludes_open_with_successful_routing()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var note = SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000001");
        SeedRoutingDecision(note, NoteRoutingResultStatus.AssignedToDepartment);

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRouting);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(0, summary.Workload!.RequiresRouting);
    }

    [Fact]
    public async Task GetSummary_due_soon_count_uses_default_window_and_null_due_excluded()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, NoteStatus.Open, dueAtUtc: FixedNow.AddDays(3), reference: "OBS-00000001");
        SeedNote(reporter.Id, NoteStatus.Open, dueAtUtc: FixedNow.AddDays(10), reference: "OBS-00000002");
        SeedNote(reporter.Id, NoteStatus.Open, dueAtUtc: null, reference: "OBS-00000003");

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRisk);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Risk);
        Assert.Equal(1, summary.Risk!.DueSoon);
        Assert.Equal(7, summary.DueSoonDays);
    }

    [Fact]
    public async Task GetSummary_throws_when_from_is_after_to()
    {
        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var query = new OperationalDashboardQuery
        {
            FromUtc = FixedNow,
            ToUtc = FixedNow.AddDays(-1)
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetSummaryAsync(query));
        Assert.Contains("from", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTrends_throws_when_from_is_after_to()
    {
        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var query = new OperationalDashboardQuery
        {
            FromUtc = FixedNow,
            ToUtc = FixedNow.AddDays(-2)
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTrendsAsync(query));
    }

    [Fact]
    public async Task GetSummary_throws_when_period_exceeds_90_days()
    {
        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var query = new OperationalDashboardQuery
        {
            FromUtc = FixedNow.AddDays(-91),
            ToUtc = FixedNow
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetSummaryAsync(query));
        Assert.Contains("90", exception.Message);
    }

    [Fact]
    public async Task GetTrends_throws_when_period_exceeds_90_days()
    {
        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var query = new OperationalDashboardQuery
        {
            FromUtc = FixedNow.AddDays(-100),
            ToUtc = FixedNow
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTrendsAsync(query));
    }

    [Fact]
    public async Task GetSummary_accepts_exactly_90_day_period()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, reference: "OBS-00000001");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var query = new OperationalDashboardQuery
        {
            FromUtc = FixedNow.AddDays(-90),
            ToUtc = FixedNow
        };

        var summary = await service.GetSummaryAsync(query);

        Assert.Equal(FixedNow.AddDays(-90), summary.FromUtc);
        Assert.Equal(FixedNow, summary.ToUtc);
    }

    [Fact]
    public async Task GetSummary_defaults_to_30_day_period_when_unspecified()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, reference: "OBS-00000001");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.Equal(FixedNow.AddDays(-30), summary.FromUtc);
        Assert.Equal(FixedNow, summary.ToUtc);
    }

    [Fact]
    public void StartOfSaudiDayUtc_midnight_riyadh_converts_to_expected_utc()
    {
        var noonUtc = new DateTimeOffset(2026, 7, 21, 9, 30, 0, TimeSpan.Zero);
        var start = InvokeStartOfSaudiDayUtc(noonUtc);
        var local = TimeZoneInfo.ConvertTime(start, TimeZones.SaudiArabia);

        Assert.Equal(0, local.Hour);
        Assert.Equal(0, local.Minute);
        Assert.Equal(2026, local.Year);
        Assert.Equal(7, local.Month);
        Assert.Equal(21, local.Day);
    }

    [Fact]
    public void StartOfSaudiDayUtc_same_saudi_calendar_day_returns_identical_start()
    {
        var early = new DateTimeOffset(2026, 7, 20, 21, 0, 0, TimeSpan.Zero);
        var late = new DateTimeOffset(2026, 7, 21, 20, 59, 59, TimeSpan.Zero);

        Assert.Equal(InvokeStartOfSaudiDayUtc(early), InvokeStartOfSaudiDayUtc(late));
    }

    [Fact]
    public void StartOfSaudiDayUtc_just_after_midnight_starts_new_saudi_day()
    {
        var beforeMidnight = new DateTimeOffset(2026, 7, 20, 20, 59, 0, TimeSpan.Zero);
        var afterMidnight = new DateTimeOffset(2026, 7, 20, 21, 1, 0, TimeSpan.Zero);

        Assert.NotEqual(
            InvokeStartOfSaudiDayUtc(beforeMidnight),
            InvokeStartOfSaudiDayUtc(afterMidnight));
    }

    [Fact]
    public async Task GetTrends_daily_buckets_start_on_saudi_day_boundaries()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, reference: "OBS-00000001");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var from = FixedNow.AddDays(-2);
        var trends = await service.GetTrendsAsync(new OperationalDashboardQuery
        {
            FromUtc = from,
            ToUtc = FixedNow
        });

        Assert.Equal("daily", trends.Granularity);
        Assert.NotEmpty(trends.Points);
        Assert.All(trends.Points, point =>
        {
            var local = TimeZoneInfo.ConvertTime(point.BucketStartUtc, TimeZones.SaudiArabia);
            Assert.Equal(0, local.Hour);
            Assert.Equal(0, local.Minute);
        });
    }

    [Fact]
    public async Task Scope_filter_excludes_notes_outside_mocked_scope()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var inScope = SeedNote(reporter.Id, regionId: SeedIds.RegionA, reference: "OBS-00000001");
        SeedNote(reporter.Id, regionId: SeedIds.RegionB, reference: "OBS-00000002");

        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        NoteTestFixtures.GrantPermissions(
            _db,
            viewer.Id,
            $"Role-{viewer.Id}",
            PermissionCodes.DashboardViewOperational);
        var current = new FakeCurrentUser(
            true,
            viewer.Id,
            viewer.Id.ToString(),
            "viewer",
            [PermissionCodes.DashboardViewOperational],
            [new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null)]);
        _db.Regions.Add(new Domain.Organization.Region
        {
            Id = SeedIds.RegionA,
            OrganizationId = SeedIds.Organization,
            Code = "A",
            NameAr = "أ"
        });
        _db.SaveChanges();

        var service = BuildServiceWithCurrent(current);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.OpenTotal);
        Assert.Equal(inScope.Id, (await _db.OperationalNotes
            .Where(note => note.RegionId == SeedIds.RegionA)
            .Select(note => note.Id)
            .ToListAsync()).Single());
    }

    [Fact]
    public async Task Mocked_scope_service_limits_kpi_notes_to_allowed_ids()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var allowed = SeedNote(reporter.Id, reference: "OBS-00000001");
        var blocked = SeedNote(reporter.Id, reference: "OBS-00000002");

        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        NoteTestFixtures.GrantPermissions(
            _db,
            viewer.Id,
            $"Role-{viewer.Id}",
            PermissionCodes.DashboardViewOperational);
        var current = DashboardUser(viewer.Id, PermissionCodes.DashboardViewOperational);
        var scope = new FilteringNoteScopeService(_db.OperationalNotes.Where(note => note.Id == allowed.Id));
        var typeAccess = new PassThroughNoteTypeAccessService();
        var service = BuildServiceWithMocks(current, scope, typeAccess);

        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.OpenTotal);
        Assert.NotEqual(blocked.Id, allowed.Id);
    }

    [Fact]
    public async Task Mocked_type_access_service_limits_kpi_notes_to_allowed_types()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var allowedTypeId = Guid.Parse("44444444-4444-4444-4444-444444444401");
        var blockedTypeId = Guid.Parse("44444444-4444-4444-4444-444444444402");
        _db.NoteTypes.AddRange(
            new NoteType
            {
                Id = allowedTypeId,
                Code = "SECURITY",
                NameAr = "أمن",
                IsActive = true,
                SortOrder = 1,
                DefaultSeverity = NoteSeverity.Medium
            },
            new NoteType
            {
                Id = blockedTypeId,
                Code = "TECH",
                NameAr = "تقني",
                IsActive = true,
                SortOrder = 2,
                DefaultSeverity = NoteSeverity.Medium
            });
        _db.SaveChanges();

        SeedNote(reporter.Id, noteTypeId: allowedTypeId, reference: "OBS-00000001");
        SeedNote(reporter.Id, noteTypeId: blockedTypeId, reference: "OBS-00000002");

        var viewer = NoteTestFixtures.AddUser(_db, "viewer");
        NoteTestFixtures.GrantPermissions(
            _db,
            viewer.Id,
            $"Role-{viewer.Id}",
            PermissionCodes.DashboardViewOperational);
        var current = DashboardUser(viewer.Id, PermissionCodes.DashboardViewOperational);
        var scope = new PassThroughNoteScopeService();
        var typeAccess = new FilteringNoteTypeAccessService(
            _db.OperationalNotes.Where(note => note.NoteTypeId == allowedTypeId));
        var service = BuildServiceWithMocks(current, scope, typeAccess);

        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.OpenTotal);
    }

    [Fact]
    public async Task Sensitive_confidential_notes_excluded_from_kpis_without_view_sensitive()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(
            reporter.Id,
            classification: ClassificationLevel.Internal,
            reference: "OBS-00000001");
        SeedNote(
            reporter.Id,
            classification: ClassificationLevel.Confidential,
            reference: "OBS-00000002");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.OpenTotal);
    }

    [Fact]
    public async Task Sensitive_restricted_notes_remain_in_kpis_without_view_sensitive()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(
            reporter.Id,
            classification: ClassificationLevel.Restricted,
            reference: "OBS-00000001");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.OpenTotal);
    }

    [Fact]
    public async Task Sensitive_confidential_notes_included_with_view_sensitive_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(
            reporter.Id,
            classification: ClassificationLevel.Secret,
            reference: "OBS-00000001");

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.NotesViewSensitive);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.OpenTotal);
    }

    [Fact]
    public async Task Soft_deleted_notes_are_excluded_from_open_kpis()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, reference: "OBS-00000001");
        SeedNote(reporter.Id, reference: "OBS-00000002", isDeleted: true);

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Equal(1, summary.Workload!.OpenTotal);
    }

    [Fact]
    public async Task Soft_deleted_overdue_note_is_not_counted_in_risk_kpis()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(
            reporter.Id,
            dueAtUtc: FixedNow.AddDays(-5),
            reference: "OBS-00000001",
            isDeleted: true);

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRisk);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Risk);
        Assert.Equal(0, summary.Risk!.Overdue);
    }

    [Fact]
    public async Task GetSummary_risk_section_null_without_dashboard_view_risk_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, dueAtUtc: FixedNow.AddDays(-1), reference: "OBS-00000001");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Null(summary.Risk);
    }

    [Fact]
    public async Task GetSummary_routing_section_null_without_dashboard_view_routing_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, reference: "OBS-00000001");

        var (service, _) = BuildService(PermissionCodes.DashboardViewOperational);
        var summary = await service.GetSummaryAsync(new OperationalDashboardQuery());

        Assert.NotNull(summary.Workload);
        Assert.Null(summary.Routing);
    }

    [Fact]
    public async Task ApplyRequiresRoutingFilter_counts_only_latest_failed_decision()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var note = SeedNote(reporter.Id, NoteStatus.Open, reference: "OBS-00000001");
        SeedRoutingDecision(note, NoteRoutingResultStatus.NoMatchingRule, FixedNow.AddDays(-2));
        SeedRoutingDecision(note, NoteRoutingResultStatus.AssignedToDepartment, FixedNow.AddDays(-1));

        var queryable = OperationalDashboardFilterBuilder.ApplyRequiresRoutingFilter(_db.OperationalNotes);
        var count = await queryable.CountAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ApplyUnassignedOpenFilter_excludes_cancelled_notes()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, NoteStatus.Cancelled, reference: "OBS-00000001");

        var count = await OperationalDashboardFilterBuilder
            .ApplyUnassignedOpenFilter(_db.OperationalNotes)
            .CountAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetPriorityQueues_most_overdue_skips_notes_without_due_date()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        SeedNote(reporter.Id, dueAtUtc: null, reference: "OBS-00000001");
        var overdue = SeedNote(
            reporter.Id,
            dueAtUtc: FixedNow.AddDays(-4),
            reference: "OBS-00000002");

        var (service, _) = BuildService(
            PermissionCodes.DashboardViewOperational,
            PermissionCodes.DashboardViewRisk);
        var queues = await service.GetPriorityQueuesAsync(new OperationalDashboardQuery
        {
            Queue = OperationalDashboardPriorityQueue.MostOverdueNotes
        });

        var item = Assert.Single(queues.MostOverdueNotes!);
        Assert.Equal(overdue.Id, item.Id);
        Assert.Equal(FixedNow.AddDays(-4), item.DueAtUtc);
    }

    private static DateTimeOffset InvokeStartOfSaudiDayUtc(DateTimeOffset utcInstant)
    {
        var resolverType = typeof(OperationalDashboardFilterBuilder).Assembly
            .GetType("Baseera.Application.Dashboard.OperationalDashboardPeriodResolver")
            ?? throw new InvalidOperationException("OperationalDashboardPeriodResolver not found.");
        var method = resolverType.GetMethod(
            "StartOfSaudiDayUtc",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("StartOfSaudiDayUtc not found.");
        return (DateTimeOffset)method.Invoke(null, [utcInstant])!;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class PassThroughNoteScopeService : INoteScopeService
    {
        public bool CanAccess(OperationalNote note) => true;
        public bool CanAccessRoutingRule(NoteRoutingRule rule) => true;
        public IQueryable<OperationalNote> FilterQueryable(IQueryable<OperationalNote> query) => query;
        public Task<IQueryable<OperationalNote>> FilterQueryableAsync(
            IQueryable<OperationalNote> query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(query);
        public IQueryable<NoteRoutingRule> FilterRoutingRulesQueryable(IQueryable<NoteRoutingRule> query) => query;
        public Task<IQueryable<NoteRoutingRule>> FilterRoutingRulesQueryableAsync(
            IQueryable<NoteRoutingRule> query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(query);
        public void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId) { }
        public Task EnsureOrgEntitiesActiveAsync(
            ScopeType scopeType,
            Guid? regionId,
            Guid? facilityId,
            Guid? facilityUnitId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<(Guid RegionId, Guid FacilityId)> ResolveIntakeAsync(
            Guid userId,
            Guid? requestedRegionId,
            Guid? requestedFacilityId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((Guid.Empty, Guid.Empty));
    }

    private sealed class FilteringNoteScopeService(IQueryable<OperationalNote> allowed) : INoteScopeService
    {
        private IQueryable<OperationalNote> Filter(IQueryable<OperationalNote> query) =>
            query.Where(note => allowed.Select(n => n.Id).Contains(note.Id));

        public bool CanAccess(OperationalNote note) => true;
        public bool CanAccessRoutingRule(NoteRoutingRule rule) => true;
        public IQueryable<OperationalNote> FilterQueryable(IQueryable<OperationalNote> query) => Filter(query);
        public Task<IQueryable<OperationalNote>> FilterQueryableAsync(
            IQueryable<OperationalNote> query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Filter(query));
        public IQueryable<NoteRoutingRule> FilterRoutingRulesQueryable(IQueryable<NoteRoutingRule> query) => query;
        public Task<IQueryable<NoteRoutingRule>> FilterRoutingRulesQueryableAsync(
            IQueryable<NoteRoutingRule> query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(query);
        public void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId) { }
        public Task EnsureOrgEntitiesActiveAsync(
            ScopeType scopeType,
            Guid? regionId,
            Guid? facilityId,
            Guid? facilityUnitId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<(Guid RegionId, Guid FacilityId)> ResolveIntakeAsync(
            Guid userId,
            Guid? requestedRegionId,
            Guid? requestedFacilityId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((Guid.Empty, Guid.Empty));
    }

    private sealed class PassThroughNoteTypeAccessService : INoteTypeAccessService
    {
        public Task<bool> CanViewAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanCreateAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanAssignAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanProcessAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanSubmitForVerificationAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanReviewAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanCancelAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanReopenAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanArchiveAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanRestoreAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<EffectiveNoteTypeAccessDto?> GetEffectiveAccessAsync(
            Guid userId,
            Guid noteTypeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EffectiveNoteTypeAccessDto?>(null);
        public Task<IReadOnlyList<EffectiveNoteTypeAccessDto>> GetEffectiveAccessAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EffectiveNoteTypeAccessDto>>([]);
        public Task<IReadOnlyDictionary<Guid, EffectiveNoteTypeAccessDto?>> GetEffectiveAccessForUsersAsync(
            IEnumerable<Guid> userIds,
            Guid noteTypeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, EffectiveNoteTypeAccessDto?>>(
                new Dictionary<Guid, EffectiveNoteTypeAccessDto?>());
        public Task<IReadOnlyList<NoteTypeDto>> GetAccessibleNoteTypesAsync(
            NoteTypeCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NoteTypeDto>>([]);
        public Task<IQueryable<OperationalNote>> FilterViewableNotesAsync(
            IQueryable<OperationalNote> query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(query);
        public Task EnsureCanAsync(
            Guid noteTypeId,
            NoteTypeCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FilteringNoteTypeAccessService(IQueryable<OperationalNote> allowed) : INoteTypeAccessService
    {
        public Task<IQueryable<OperationalNote>> FilterViewableNotesAsync(
            IQueryable<OperationalNote> query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(query.Where(note => allowed.Select(n => n.Id).Contains(note.Id)));

        public Task<bool> CanViewAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanCreateAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanAssignAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanProcessAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanSubmitForVerificationAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanReviewAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanCancelAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanReopenAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanArchiveAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> CanRestoreAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<EffectiveNoteTypeAccessDto?> GetEffectiveAccessAsync(
            Guid userId,
            Guid noteTypeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EffectiveNoteTypeAccessDto?>(null);
        public Task<IReadOnlyList<EffectiveNoteTypeAccessDto>> GetEffectiveAccessAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EffectiveNoteTypeAccessDto>>([]);
        public Task<IReadOnlyDictionary<Guid, EffectiveNoteTypeAccessDto?>> GetEffectiveAccessForUsersAsync(
            IEnumerable<Guid> userIds,
            Guid noteTypeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, EffectiveNoteTypeAccessDto?>>(
                new Dictionary<Guid, EffectiveNoteTypeAccessDto?>());
        public Task<IReadOnlyList<NoteTypeDto>> GetAccessibleNoteTypesAsync(
            NoteTypeCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NoteTypeDto>>([]);
        public Task EnsureCanAsync(
            Guid noteTypeId,
            NoteTypeCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
