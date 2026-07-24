using System.Data.Common;
using System.Net.Http.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class OperationalDashboardQueryCountIntegrationTests
{
    private const int SummaryQueryMax = 18;
    private const int TrendsQueryMax = 14;
    private const int FacilityWorkspaceQueryMax = 80;

    [IntegrationConnectionFact]
    public async Task Summary_query_count_is_bounded_and_independent_of_note_volume()
    {
        var counter = new SqlCommandCounter();
        await using var factory = BaseeraApiFactory.WithInterceptor(counter);
        await SeedDashboardAdminAsync(factory, "dash-count-admin");

        var client = factory.CreateAuthenticatedClient("dash-count-admin");
        await SeedOverdueNotesAsync(factory, "dash-count-admin", count: 5);
        counter.Reset();
        var small = await client.GetAsync("/api/v1/dashboard/operations/summary");
        small.EnsureSuccessStatusCode();
        var smallCount = counter.SelectCount;

        await SeedOverdueNotesAsync(factory, "dash-count-admin", count: 40, startIndex: 100);
        counter.Reset();
        var large = await client.GetAsync("/api/v1/dashboard/operations/summary");
        large.EnsureSuccessStatusCode();

        Assert.Equal(smallCount, counter.SelectCount);
        Assert.InRange(counter.SelectCount, 1, SummaryQueryMax);
    }

    [IntegrationConnectionFact]
    public async Task Summary_with_risk_and_routing_does_not_double_routing_failure_aggregate()
    {
        var counter = new SqlCommandCounter();
        await using var factory = BaseeraApiFactory.WithInterceptor(counter);
        await SeedDashboardAdminAsync(factory, "dash-routing-count-admin");

        var client = factory.CreateAuthenticatedClient("dash-routing-count-admin");
        counter.Reset();
        var response = await client.GetAsync("/api/v1/dashboard/operations/summary");
        response.EnsureSuccessStatusCode();

        Assert.InRange(counter.SelectCount, 1, SummaryQueryMax);
        Assert.Equal(1, CountRoutingFailureAggregateQueries(counter.CommandTexts));
    }

    [IntegrationConnectionFact]
    public async Task Trends_query_count_is_constant_for_7_30_and_90_day_periods()
    {
        var counter = new SqlCommandCounter();
        await using var factory = BaseeraApiFactory.WithInterceptor(counter);
        await SeedDashboardAdminAsync(factory, "dash-trends-count-admin");

        var client = factory.CreateAuthenticatedClient("dash-trends-count-admin");
        await SeedTrendSampleDataAsync(factory);

        var counts = new List<int>();
        foreach (var days in new[] { 7, 30, 90 })
        {
            counter.Reset();
            var response = await client.GetAsync($"/api/v1/dashboard/operations/trends?periodDays={days}");
            response.EnsureSuccessStatusCode();
            counts.Add(counter.SelectCount);
            Assert.InRange(counter.SelectCount, 1, TrendsQueryMax);
        }

        Assert.Equal(counts[0], counts[1]);
        Assert.Equal(counts[1], counts[2]);
    }

    [IntegrationConnectionFact]
    public async Task Facility_workspace_query_count_is_bounded_and_independent_of_note_volume()
    {
        var counter = new SqlCommandCounter();
        await using var factory = BaseeraApiFactory.WithInterceptor(counter);
        await SeedFacilityDirectorAsync(factory, "facility-workspace-count-admin");

        var client = factory.CreateAuthenticatedClient("facility-workspace-count-admin");
        await SeedOverdueNotesAsync(factory, "facility-workspace-count-admin", count: 5, startIndex: 200);
        counter.Reset();
        var small = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");
        small.EnsureSuccessStatusCode();
        var smallCount = counter.SelectCount;

        await SeedOverdueNotesAsync(factory, "facility-workspace-count-admin", count: 40, startIndex: 300);
        counter.Reset();
        var large = await client.GetAsync($"/api/v1/workspaces/facility-operations?level=1&facilityId={SeedIds.FacilityA1}");
        large.EnsureSuccessStatusCode();

        Assert.Equal(smallCount, counter.SelectCount);
        Assert.InRange(counter.SelectCount, 1, FacilityWorkspaceQueryMax);
    }

    [IntegrationConnectionFact]
    public async Task Trends_returns_expected_counts_and_zero_empty_buckets()
    {
        await using var factory = new BaseeraApiFactory();
        await SeedDashboardAdminAsync(factory, "dash-trends-correctness-admin");

        var admin = factory.CreateAuthenticatedClient("dash-trends-correctness-admin");
        await SeedTrendSampleDataAsync(factory);

        var trends = await admin.GetFromJsonAsync<TrendsEnvelope>(
            "/api/v1/dashboard/operations/trends?periodDays=7");
        Assert.NotNull(trends);
        Assert.Equal("daily", trends!.Granularity);
        Assert.Contains(trends.Points, point => point.NotesCreated > 0);
        Assert.Contains(trends.Points, point =>
            point.NotesCreated == 0 &&
            point.NotesCompleted == 0 &&
            point.NotesBecameOverdue == 0 &&
            point.CorrectiveActionsCompleted == 0 &&
            point.RoutingSuccess == 0 &&
            point.RoutingFailure == 0);
    }

    [IntegrationConnectionFact]
    public async Task Breakdowns_severity_and_status_include_corrective_actions_overdue()
    {
        await using var factory = new BaseeraApiFactory();
        await SeedDashboardAdminAsync(factory, "dash-breakdown-ca-admin");

        var admin = factory.CreateAuthenticatedClient("dash-breakdown-ca-admin");
        await SeedBreakdownCorrectiveActionSampleAsync(factory, admin);

        var severity = await admin.GetFromJsonAsync<BreakdownEnvelope>(
            "/api/v1/dashboard/operations/breakdowns?breakdownBy=3");
        var status = await admin.GetFromJsonAsync<BreakdownEnvelope>(
            "/api/v1/dashboard/operations/breakdowns?breakdownBy=4");

        Assert.NotNull(severity);
        Assert.NotNull(status);
        Assert.Contains(severity!.Rows, row => row.CorrectiveActionsOverdue > 0);
        Assert.Contains(status!.Rows, row => row.CorrectiveActionsOverdue > 0);
    }

    private static int CountRoutingFailureAggregateQueries(
        IEnumerable<string> commandTexts) =>
        commandTexts.Count(text =>
            text.Contains(
                "Dashboard.RoutingFailureAggregate",
                StringComparison.Ordinal));

    private static async Task SeedDashboardAdminAsync(BaseeraApiFactory factory, string subject)
    {
        await factory.SeedUserAsync(subject, "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
    }

    private static async Task SeedFacilityDirectorAsync(BaseeraApiFactory factory, string subject)
    {
        await factory.SeedUserAsync(subject, "مدير سجن", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
    }

    private static async Task SeedOverdueNotesAsync(
        BaseeraApiFactory factory,
        string adminSubject,
        int count,
        int startIndex = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var reporter = await db.Users.FirstAsync(u => u.ExternalSubject == adminSubject);
        for (var i = 0; i < count; i++)
        {
            db.OperationalNotes.Add(new OperationalNote
            {
                ReferenceNumber = $"OBS-OVD-{startIndex + i:D4}",
                Title = $"متأخرة {startIndex + i}",
                Description = "وصف",
                NoteTypeId = SeedIds.NoteTypeOperational,
                Severity = NoteSeverity.Medium,
                Status = NoteStatus.Open,
                SourceType = NoteSourceType.Manual,
                Classification = ClassificationLevel.Internal,
                ScopeType = ScopeType.Facility,
                RegionId = SeedIds.RegionA,
                FacilityId = SeedIds.FacilityA1,
                ReportedByUserId = reporter.Id,
                ReportedAtUtc = DateTimeOffset.UtcNow.AddDays(-(i + 3)),
                SubmittedAtUtc = DateTimeOffset.UtcNow.AddDays(-(i + 2)),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-(i + 3)),
                DueAtUtc = DateTimeOffset.UtcNow.AddDays(-(i + 1))
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedTrendSampleDataAsync(BaseeraApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var reporter = await db.Users.FirstAsync(u => u.ExternalSubject == "dash-trends-count-admin" ||
                                                      u.ExternalSubject == "dash-trends-correctness-admin");
        var now = DateTimeOffset.UtcNow;
        db.OperationalNotes.Add(new OperationalNote
        {
            ReferenceNumber = "OBS-TREND-CREATE",
            Title = "إنشاء",
            Description = "اختبار",
            NoteTypeId = SeedIds.NoteTypeOperational,
            Severity = NoteSeverity.Medium,
            Status = NoteStatus.Open,
            SourceType = NoteSourceType.Manual,
            Classification = ClassificationLevel.Internal,
            ScopeType = ScopeType.Facility,
            RegionId = SeedIds.RegionA,
            FacilityId = SeedIds.FacilityA1,
            ReportedByUserId = reporter.Id,
            ReportedAtUtc = now.AddDays(-2),
            CreatedAtUtc = now.AddDays(-2),
            DueAtUtc = now.AddDays(3)
        });
        db.OperationalNotes.Add(new OperationalNote
        {
            ReferenceNumber = "OBS-TREND-CLOSE",
            Title = "إغلاق",
            Description = "اختبار",
            NoteTypeId = SeedIds.NoteTypeOperational,
            Severity = NoteSeverity.Medium,
            Status = NoteStatus.Closed,
            SourceType = NoteSourceType.Manual,
            Classification = ClassificationLevel.Internal,
            ScopeType = ScopeType.Facility,
            RegionId = SeedIds.RegionA,
            FacilityId = SeedIds.FacilityA1,
            ReportedByUserId = reporter.Id,
            ReportedAtUtc = now.AddDays(-3),
            CreatedAtUtc = now.AddDays(-3),
            ClosedAtUtc = now.AddDays(-1),
            DueAtUtc = now.AddDays(2)
        });
        db.OperationalNotes.Add(new OperationalNote
        {
            ReferenceNumber = "OBS-TREND-OVERDUE",
            Title = "تأخر",
            Description = "اختبار",
            NoteTypeId = SeedIds.NoteTypeOperational,
            Severity = NoteSeverity.Medium,
            Status = NoteStatus.Open,
            SourceType = NoteSourceType.Manual,
            Classification = ClassificationLevel.Internal,
            ScopeType = ScopeType.Facility,
            RegionId = SeedIds.RegionA,
            FacilityId = SeedIds.FacilityA1,
            ReportedByUserId = reporter.Id,
            ReportedAtUtc = now.AddDays(-4),
            CreatedAtUtc = now.AddDays(-4),
            DueAtUtc = now.AddDays(-1)
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedBreakdownCorrectiveActionSampleAsync(BaseeraApiFactory factory, HttpClient admin)
    {
        var high = await admin.PostAsJsonAsync("/api/v1/notes", new
        {
            title = "عالية",
            description = "وصف",
            noteTypeId = SeedIds.NoteTypeOperational,
            severity = NoteSeverity.High,
            sourceType = NoteSourceType.Manual,
            classification = ClassificationLevel.Internal,
            scopeType = ScopeType.Facility,
            regionId = SeedIds.RegionA,
            facilityId = SeedIds.FacilityA1,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        high.EnsureSuccessStatusCode();
        var highNote = await high.Content.ReadFromJsonAsync<NoteDetail>();

        var open = await admin.PostAsJsonAsync("/api/v1/notes", new
        {
            title = "مفتوحة",
            description = "وصف",
            noteTypeId = SeedIds.NoteTypeOperational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            classification = ClassificationLevel.Internal,
            scopeType = ScopeType.Facility,
            regionId = SeedIds.RegionA,
            facilityId = SeedIds.FacilityA1,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        open.EnsureSuccessStatusCode();
        var openNote = await open.Content.ReadFromJsonAsync<NoteDetail>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var reporterId = await db.Users
            .Where(u => u.ExternalSubject == "dash-breakdown-ca-admin")
            .Select(u => u.Id)
            .FirstAsync();
        db.CorrectiveActions.AddRange(
            new CorrectiveAction
            {
                ReferenceNumber = "CA-BRK-001",
                OperationalNoteId = highNote!.Id,
                Title = "إجراء عالي",
                Description = "اختبار",
                Status = CorrectiveActionStatus.InProgress,
                Priority = CorrectiveActionPriority.High,
                Classification = ClassificationLevel.Internal,
                DueAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
                CreatedByUserId = reporterId
            },
            new CorrectiveAction
            {
                ReferenceNumber = "CA-BRK-002",
                OperationalNoteId = openNote!.Id,
                Title = "إجراء مفتوح",
                Description = "اختبار",
                Status = CorrectiveActionStatus.Assigned,
                Priority = CorrectiveActionPriority.Medium,
                Classification = ClassificationLevel.Internal,
                DueAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedByUserId = reporterId
            });
        await db.SaveChangesAsync();
    }

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        public int SelectCount { get; private set; }

        public IReadOnlyList<string> CommandTexts => _commandTexts;

        private readonly List<string> _commandTexts = [];

        public void Reset()
        {
            SelectCount = 0;
            _commandTexts.Clear();
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Track(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Track(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void Track(DbCommand command)
        {
            var executableSql = StripLeadingSqlComments(
                command.CommandText);

            if (!executableSql.StartsWith(
                    "SELECT",
                    StringComparison.OrdinalIgnoreCase) &&
                !executableSql.StartsWith(
                    "WITH",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SelectCount++;
            _commandTexts.Add(command.CommandText);
        }

        private static string StripLeadingSqlComments(
            string commandText)
        {
            var remaining = commandText.AsSpan().TrimStart();

            while (remaining.StartsWith(
                       "--",
                       StringComparison.Ordinal))
            {
                var lineEnd = remaining.IndexOf('\n');

                if (lineEnd < 0)
                {
                    return string.Empty;
                }

                remaining = remaining[(lineEnd + 1)..]
                    .TrimStart();
            }

            return remaining.ToString();
        }
    }

    private sealed record TrendsEnvelope(
        IReadOnlyList<TrendPointEnvelope> Points,
        string Granularity);

    private sealed record TrendPointEnvelope(
        int NotesCreated,
        int NotesCompleted,
        int NotesBecameOverdue,
        int CorrectiveActionsCompleted,
        int RoutingSuccess,
        int RoutingFailure);

    private sealed record BreakdownEnvelope(IReadOnlyList<BreakdownRowEnvelope> Rows);

    private sealed record BreakdownRowEnvelope(int CorrectiveActionsOverdue);

    private sealed record NoteDetail(Guid Id, string RowVersion);
}
