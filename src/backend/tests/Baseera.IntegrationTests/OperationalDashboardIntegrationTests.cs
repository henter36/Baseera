using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

/// <summary>
/// Integration coverage for operational dashboard KPIs, scope isolation, permission gates,
/// sensitive/type filtering, and priority queue limits.
/// </summary>
public sealed class OperationalDashboardIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OperationalDashboardIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Summary_overdue_count_matches_notes_overdue_only_total_count_for_scoped_user()
    {
        await _factory.SeedUserAsync("dash-overdue-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("dash-overdue-region", "منطقة أ", [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));

        var admin = _factory.CreateAuthenticatedClient("dash-overdue-admin");
        var regionUser = _factory.CreateAuthenticatedClient("dash-overdue-region");

        var overdueBefore = await GetOverdueSummaryAsync(regionUser);
        var notesBefore = await GetOverdueNotesTotalAsync(regionUser);

        var overdueA1 = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "متأخرة أ1");
        await BackdateAndSubmitAsync(admin, overdueA1.Id, daysAgo: 2);

        var overdueA2 = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA2, null, "متأخرة أ2");
        await BackdateAndSubmitAsync(admin, overdueA2.Id, daysAgo: 3);

        var overdueB1 = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, null, "متأخرة ب1");
        await BackdateAndSubmitAsync(admin, overdueB1.Id, daysAgo: 1);

        var overdueAfter = await GetOverdueSummaryAsync(regionUser);
        var notesAfter = await GetOverdueNotesTotalAsync(regionUser);

        Assert.Equal(2, overdueAfter - overdueBefore);
        Assert.Equal(overdueAfter - overdueBefore, notesAfter - notesBefore);
    }

    [IntegrationConnectionFact]
    public async Task Summary_excludes_adjacent_region_overdue_notes()
    {
        await _factory.SeedUserAsync("dash-region-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("dash-region-a", "منطقة أ", [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));

        var admin = _factory.CreateAuthenticatedClient("dash-region-admin");
        var regionA = _factory.CreateAuthenticatedClient("dash-region-a");

        var overdueBefore = await GetOverdueSummaryAsync(regionA);
        var notesBefore = await GetOverdueNotesTotalAsync(regionA);

        var regionANote = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "متأخرة منطقة أ");
        await BackdateAndSubmitAsync(admin, regionANote.Id, daysAgo: 2);

        var regionBNote = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, null, "متأخرة منطقة ب");
        await BackdateAndSubmitAsync(admin, regionBNote.Id, daysAgo: 2);

        var overdueAfter = await GetOverdueSummaryAsync(regionA);
        var notes = await regionA.GetFromJsonAsync<PagedEnvelope<NoteListItem>>(
            "/api/v1/notes?overdueOnly=true&page=1&pageSize=200", JsonOptions);
        Assert.NotNull(notes);
        Assert.Contains(notes!.Items, n => n.Id == regionANote.Id);
        Assert.DoesNotContain(notes.Items, n => n.Id == regionBNote.Id);
        Assert.Equal(1, overdueAfter - overdueBefore);
        Assert.Equal(overdueAfter - overdueBefore, notes.TotalCount - notesBefore);
    }

    [IntegrationConnectionFact]
    public async Task Priority_queue_excludes_adjacent_region_overdue_notes()
    {
        await _factory.SeedUserAsync("dash-pq-region-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("dash-pq-region-a", "منطقة أ", [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));

        var admin = _factory.CreateAuthenticatedClient("dash-pq-region-admin");
        var regionA = _factory.CreateAuthenticatedClient("dash-pq-region-a");

        var regionANote = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "قائمة أ");
        await BackdateAndSubmitAsync(admin, regionANote.Id, daysAgo: 4);

        var regionBNote = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, null, "قائمة ب");
        await BackdateAndSubmitAsync(admin, regionBNote.Id, daysAgo: 1);

        var queues = await regionA.GetFromJsonAsync<DashboardPriorityQueues>(
            "/api/v1/dashboard/operations/priority-queues?queue=0", JsonOptions);
        Assert.NotNull(queues);
        Assert.NotNull(queues!.MostOverdueNotes);
        Assert.Contains(queues.MostOverdueNotes!, n => n.Id == regionANote.Id);
        Assert.DoesNotContain(queues.MostOverdueNotes!, n => n.Id == regionBNote.Id);
    }

    [IntegrationConnectionFact]
    public async Task Summary_excludes_notes_from_other_facility_unit()
    {
        var (unitEastId, unitWestId) = await SeedFacilityUnitsAsync("dash-unit-east", "dash-unit-west");
        await SeedFacilityUnitOnlyUserAsync("dash-unit-east-user", unitEastId);

        await _factory.SeedUserAsync("dash-unit-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("dash-unit-admin");
        var eastUser = _factory.CreateAuthenticatedClient("dash-unit-east-user");

        var overdueBefore = await GetOverdueSummaryAsync(eastUser);
        var notesBefore = await GetOverdueNotesTotalAsync(eastUser);

        var eastNote = await SeedOverdueFacilityUnitNoteAsync("dash-unit-admin", unitEastId, "OBS-DASH-EAST", daysAgo: 2);
        var westNote = await SeedOverdueFacilityUnitNoteAsync("dash-unit-admin", unitWestId, "OBS-DASH-WEST", daysAgo: 2);

        var overdueAfter = await GetOverdueSummaryAsync(eastUser);
        var notes = await eastUser.GetFromJsonAsync<PagedEnvelope<NoteListItem>>(
            "/api/v1/notes?overdueOnly=true&page=1&pageSize=200", JsonOptions);
        Assert.NotNull(notes);
        Assert.Equal(1, overdueAfter - overdueBefore);
        Assert.Equal(overdueAfter - overdueBefore, notes!.TotalCount - notesBefore);
        Assert.Contains(notes.Items, n => n.Id == eastNote.Id);
        Assert.DoesNotContain(notes.Items, n => n.Id == westNote.Id);
    }

    [IntegrationConnectionFact]
    public async Task Priority_queue_excludes_notes_from_other_facility_unit()
    {
        var (unitEastId, unitWestId) = await SeedFacilityUnitsAsync("dash-pq-east", "dash-pq-west");
        await SeedFacilityUnitOnlyUserAsync("dash-pq-east-user", unitEastId);

        await _factory.SeedUserAsync("dash-pq-unit-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("dash-pq-unit-admin");
        var eastUser = _factory.CreateAuthenticatedClient("dash-pq-east-user");

        var eastNote = await SeedOverdueFacilityUnitNoteAsync("dash-pq-unit-admin", unitEastId, "OBS-DASH-PQ-EAST", daysAgo: 5);
        var westNote = await SeedOverdueFacilityUnitNoteAsync("dash-pq-unit-admin", unitWestId, "OBS-DASH-PQ-WEST", daysAgo: 1);

        var queues = await eastUser.GetFromJsonAsync<DashboardPriorityQueues>(
            "/api/v1/dashboard/operations/priority-queues?queue=0", JsonOptions);
        Assert.NotNull(queues);
        Assert.NotNull(queues!.MostOverdueNotes);
        Assert.Contains(queues.MostOverdueNotes!, n => n.Id == eastNote.Id);
        Assert.DoesNotContain(queues.MostOverdueNotes!, n => n.Id == westNote.Id);
    }

    [IntegrationConnectionFact]
    public async Task Sensitive_notes_are_excluded_from_dashboard_without_view_sensitive()
    {
        await _factory.SeedUserAsync("dash-sensitive-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("dash-sensitive-viewer", "منطقة", [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));

        var admin = _factory.CreateAuthenticatedClient("dash-sensitive-admin");
        var viewer = _factory.CreateAuthenticatedClient("dash-sensitive-viewer");

        var viewerOverdueBefore = await GetOverdueSummaryAsync(viewer);
        var viewerNotesBefore = await GetOverdueNotesTotalAsync(viewer);

        var sensitive = await CreateSensitiveOverdueNoteAsync(admin, "سرية متأخرة");
        var normal = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "عادية متأخرة");
        await BackdateAndSubmitAsync(admin, normal.Id, daysAgo: 2);

        var adminSummary = await admin.GetFromJsonAsync<DashboardSummary>("/api/v1/dashboard/operations/summary", JsonOptions);
        Assert.NotNull(adminSummary);
        Assert.NotNull(adminSummary!.Risk);
        Assert.True(adminSummary.Risk!.Overdue >= 2);

        var viewerOverdueAfter = await GetOverdueSummaryAsync(viewer);
        Assert.Equal(1, viewerOverdueAfter - viewerOverdueBefore);

        var viewerNotes = await viewer.GetFromJsonAsync<PagedEnvelope<SensitiveNoteListItem>>(
            "/api/v1/notes?overdueOnly=true&page=1&pageSize=200", JsonOptions);
        Assert.NotNull(viewerNotes);
        Assert.Equal(2, viewerNotes!.TotalCount - viewerNotesBefore);
        Assert.Contains(viewerNotes.Items, n => n.Id == sensitive.Id && n.IsSensitiveRedacted);

        var adminQueues = await admin.GetFromJsonAsync<DashboardPriorityQueues>(
            "/api/v1/dashboard/operations/priority-queues?queue=0", JsonOptions);
        var viewerQueues = await viewer.GetFromJsonAsync<DashboardPriorityQueues>(
            "/api/v1/dashboard/operations/priority-queues?queue=0", JsonOptions);
        Assert.NotNull(adminQueues?.MostOverdueNotes);
        Assert.NotNull(viewerQueues?.MostOverdueNotes);
        Assert.Contains(adminQueues!.MostOverdueNotes!, n => n.Id == sensitive.Id);
        Assert.DoesNotContain(viewerQueues!.MostOverdueNotes!, n => n.Id == sensitive.Id);
    }

    [IntegrationConnectionFact]
    public async Task Note_type_deny_override_excludes_note_from_dashboard_workload()
    {
        await _factory.SeedUserAsync("dash-type-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var viewerSubject = $"dash-type-viewer-{Guid.NewGuid():N}";
        await _factory.SeedUserAsync(viewerSubject, "مشاهد نوع", [RoleCodes.ReadOnlyUser],
            (ScopeType.Global, null, null));
        await DenyNoteTypeViewAsync(viewerSubject, SeedIds.NoteTypeSecurity);

        var admin = _factory.CreateAuthenticatedClient("dash-type-admin");
        var viewer = _factory.CreateAuthenticatedClient(viewerSubject);

        var adminOpenBefore = await GetOpenWorkloadAsync(admin);
        var viewerOpenBefore = await GetOpenWorkloadAsync(viewer);

        await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "تشغيلية");
        var security = await CreateNoteWithTypeAsync(admin, SeedIds.NoteTypeSecurity, "أمنية");

        var adminOpenAfter = await GetOpenWorkloadAsync(admin);
        var viewerOpenAfter = await GetOpenWorkloadAsync(viewer);
        var adminDelta = adminOpenAfter - adminOpenBefore;
        var viewerDelta = viewerOpenAfter - viewerOpenBefore;
        Assert.True(adminDelta >= 1);
        Assert.True(viewerDelta >= 1);
        Assert.Equal(1, adminDelta - viewerDelta);

        var adminNotes = await admin.GetFromJsonAsync<PagedEnvelope<NoteListItem>>("/api/v1/notes?page=1&pageSize=200", JsonOptions);
        var viewerNotes = await viewer.GetFromJsonAsync<PagedEnvelope<NoteListItem>>("/api/v1/notes?page=1&pageSize=200", JsonOptions);
        Assert.Contains(adminNotes!.Items, n => n.Id == security.Id);
        Assert.DoesNotContain(viewerNotes!.Items, n => n.Id == security.Id);
    }

    [IntegrationConnectionFact]
    public async Task Note_type_deny_override_excludes_note_from_priority_queue()
    {
        await _factory.SeedUserAsync("dash-type-pq-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var viewerSubject = $"dash-type-pq-viewer-{Guid.NewGuid():N}";
        await _factory.SeedUserAsync(viewerSubject, "مشاهد نوع", [RoleCodes.ReadOnlyUser],
            (ScopeType.Global, null, null));
        await DenyNoteTypeViewAsync(viewerSubject, SeedIds.NoteTypeSecurity);

        var admin = _factory.CreateAuthenticatedClient("dash-type-pq-admin");
        var viewer = _factory.CreateAuthenticatedClient(viewerSubject);

        var security = await CreateNoteWithTypeAsync(admin, SeedIds.NoteTypeSecurity, "أمنية متأخرة");
        await BackdateAndSubmitAsync(admin, security.Id, daysAgo: 400);

        var operational = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "تشغيلية متأخرة");
        await BackdateAndSubmitAsync(admin, operational.Id, daysAgo: 395);

        var viewerQueues = await viewer.GetFromJsonAsync<DashboardPriorityQueues>(
            "/api/v1/dashboard/operations/priority-queues?queue=0", JsonOptions);
        Assert.NotNull(viewerQueues?.MostOverdueNotes);
        Assert.Contains(viewerQueues!.MostOverdueNotes!, n => n.Id == operational.Id);
        Assert.DoesNotContain(viewerQueues.MostOverdueNotes!, n => n.Id == security.Id);
    }

    [IntegrationConnectionFact]
    public async Task Priority_queue_length_is_at_most_ten()
    {
        await _factory.SeedUserAsync("dash-limit-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("dash-limit-admin");

        for (var i = 0; i < 12; i++)
        {
            var note = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, $"متأخرة {i}");
            await BackdateAndSubmitAsync(admin, note.Id, daysAgo: i + 1);
        }

        var queues = await admin.GetFromJsonAsync<DashboardPriorityQueues>(
            "/api/v1/dashboard/operations/priority-queues?queue=0", JsonOptions);
        Assert.NotNull(queues);
        Assert.Equal(10, queues!.Limit);
        Assert.NotNull(queues.MostOverdueNotes);
        Assert.True(queues.MostOverdueNotes!.Count <= 10);
    }

    [IntegrationConnectionFact]
    public async Task Summary_returns_403_without_dashboard_permissions()
    {
        await _factory.SeedUserAsync("dash-no-perm", "بلا لوحة", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var client = _factory.CreateAuthenticatedClient("dash-no-perm");
        var response = await client.GetAsync("/api/v1/dashboard/operations/summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Priority_queues_returns_403_without_dashboard_permissions()
    {
        await _factory.SeedUserAsync("dash-no-perm-pq", "بلا لوحة", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var client = _factory.CreateAuthenticatedClient("dash-no-perm-pq");
        var response = await client.GetAsync("/api/v1/dashboard/operations/priority-queues");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Trends_returns_403_without_dashboard_permissions()
    {
        await _factory.SeedUserAsync("dash-no-perm-trends", "بلا لوحة", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var client = _factory.CreateAuthenticatedClient("dash-no-perm-trends");
        var response = await client.GetAsync("/api/v1/dashboard/operations/trends");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Breakdowns_returns_403_without_dashboard_permissions()
    {
        await _factory.SeedUserAsync("dash-no-perm-breakdowns", "بلا لوحة", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var client = _factory.CreateAuthenticatedClient("dash-no-perm-breakdowns");
        var response = await client.GetAsync("/api/v1/dashboard/operations/breakdowns?breakdownBy=1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Readonly_dashboard_user_gets_workload_without_risk_section()
    {
        await _factory.SeedUserAsync("dash-readonly", "مشاهد", [RoleCodes.ReadOnlyUser],
            (ScopeType.Global, null, null));

        var client = _factory.CreateAuthenticatedClient("dash-readonly");
        var response = await client.GetAsync("/api/v1/dashboard/operations/summary");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var summary = JsonSerializer.Deserialize<DashboardSummary>(body, JsonOptions);
        Assert.NotNull(summary);
        Assert.NotNull(summary!.Workload);
        Assert.Null(summary.Risk);
        Assert.Null(summary.CorrectiveActions);
        Assert.Null(summary.Routing);
    }

    private async Task<OperationalNote> SeedOverdueFacilityUnitNoteAsync(
        string reporterSubject,
        Guid facilityUnitId,
        string referenceNumber,
        int daysAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var reporter = await db.Users.FirstAsync(u => u.ExternalSubject == reporterSubject);
        var note = new OperationalNote
        {
            ReferenceNumber = referenceNumber,
            Title = referenceNumber,
            Description = "ملاحظة وحدة للوحة التشغيل",
            NoteTypeId = SeedIds.NoteTypeOperational,
            Severity = NoteSeverity.Medium,
            Status = NoteStatus.Open,
            SourceType = NoteSourceType.Manual,
            Classification = ClassificationLevel.Internal,
            ScopeType = ScopeType.FacilityUnit,
            RegionId = SeedIds.RegionA,
            FacilityId = SeedIds.FacilityA1,
            FacilityUnitId = facilityUnitId,
            ReportedByUserId = reporter.Id,
            ReportedAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo - 1),
            SubmittedAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo),
            DueAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo)
        };
        db.OperationalNotes.Add(note);
        await db.SaveChangesAsync();
        return note;
    }

    private static async Task<int> GetOverdueSummaryAsync(HttpClient client)
    {
        var summary = await client.GetFromJsonAsync<DashboardSummary>("/api/v1/dashboard/operations/summary", JsonOptions);
        Assert.NotNull(summary);
        Assert.NotNull(summary!.Risk);
        return summary.Risk!.Overdue;
    }

    private static async Task<int> GetOverdueNotesTotalAsync(HttpClient client)
    {
        var notes = await client.GetFromJsonAsync<PagedEnvelope<NoteListItem>>(
            "/api/v1/notes?overdueOnly=true&page=1&pageSize=200", JsonOptions);
        Assert.NotNull(notes);
        return notes!.TotalCount;
    }

    private static async Task<int> GetOpenWorkloadAsync(HttpClient client)
    {
        var summary = await client.GetFromJsonAsync<DashboardSummary>("/api/v1/dashboard/operations/summary", JsonOptions);
        Assert.NotNull(summary);
        Assert.NotNull(summary!.Workload);
        return summary.Workload!.OpenTotal;
    }

    private async Task<NoteDetail> BackdateAndSubmitAsync(HttpClient adminClient, Guid noteId, int daysAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var entity = await db.OperationalNotes.SingleAsync(n => n.Id == noteId);
        entity.DueAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        await db.SaveChangesAsync();

        var detail = await adminClient.GetFromJsonAsync<NoteDetail>($"/api/v1/notes/{noteId}", JsonOptions);
        Assert.NotNull(detail);
        return await PostSubmitAsync(adminClient, noteId, detail!.RowVersion);
    }

    private async Task<(Guid EastId, Guid WestId)> SeedFacilityUnitsAsync(string eastCode, string westCode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var east = new FacilityUnit
        {
            FacilityId = SeedIds.FacilityA1,
            Code = eastCode,
            NameAr = "وحدة شرق"
        };
        var west = new FacilityUnit
        {
            FacilityId = SeedIds.FacilityA1,
            Code = westCode,
            NameAr = "وحدة غرب"
        };
        db.FacilityUnits.AddRange(east, west);
        await db.SaveChangesAsync();
        return (east.Id, west.Id);
    }

    private async Task SeedFacilityUnitOnlyUserAsync(string subject, Guid unitId)
    {
        await _factory.SeedUserAsync(subject, "مستخدم وحدة", [RoleCodes.FacilityDirector]);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var user = await db.Users.FirstAsync(u => u.ExternalSubject == subject);
        var existingScopes = await db.UserScopes.Where(s => s.UserId == user.Id).ToListAsync();
        db.UserScopes.RemoveRange(existingScopes);
        db.UserScopes.Add(new UserScope
        {
            UserId = user.Id,
            ScopeType = ScopeType.FacilityUnit,
            RegionId = SeedIds.RegionA,
            FacilityId = SeedIds.FacilityA1,
            FacilityUnitId = unitId,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private async Task DenyNoteTypeViewAsync(string subject, Guid noteTypeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var userId = await db.Users.Where(u => u.ExternalSubject == subject).Select(u => u.Id).FirstAsync();
        db.UserNoteTypeOverrides.Add(new UserNoteTypeOverride
        {
            UserId = userId,
            NoteTypeId = noteTypeId,
            CanViewOverride = false,
            IsActive = true,
            Reason = "اختبار لوحة التشغيل"
        });
        await db.SaveChangesAsync();
    }

    private static async Task<NoteDetail> CreateNoteAsync(
        HttpClient client,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "وصف تفصيلي للوحة التشغيل",
            noteTypeId = SeedIds.NoteTypeOperational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = ClassificationLevel.Internal,
            scopeType,
            regionId,
            facilityId,
            facilityUnitId,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }

    private static async Task<NoteDetail> CreateNoteWithTypeAsync(HttpClient client, Guid noteTypeId, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "وصف نوع محدد",
            noteTypeId,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = ClassificationLevel.Internal,
            scopeType = ScopeType.Facility,
            regionId = SeedIds.RegionA,
            facilityId = SeedIds.FacilityA1,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }

    private async Task<NoteDetail> CreateSensitiveOverdueNoteAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "تفاصيل سرية",
            noteTypeId = SeedIds.NoteTypeSecurity,
            severity = NoteSeverity.High,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = ClassificationLevel.Confidential,
            scopeType = ScopeType.Facility,
            regionId = SeedIds.RegionA,
            facilityId = SeedIds.FacilityA1,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var note = JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
        return await BackdateAndSubmitAsync(client, note.Id, daysAgo: 2);
    }

    private static async Task<NoteDetail> PostSubmitAsync(HttpClient client, Guid noteId, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/notes/{noteId}/submit", new { reason = "تقديم", rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }
}

internal sealed record DashboardSummary(
    DashboardWorkload? Workload,
    DashboardRisk? Risk,
    DashboardCorrectiveActions? CorrectiveActions,
    DashboardRouting? Routing);

internal sealed record DashboardWorkload(int OpenTotal);
internal sealed record DashboardRisk(int Overdue);
internal sealed record DashboardCorrectiveActions(int Active);
internal sealed record DashboardRouting(int RequiresRouting);

internal sealed record DashboardPriorityQueues(
    IReadOnlyList<DashboardQueueNote>? MostOverdueNotes,
    int Limit);

internal sealed record DashboardQueueNote(Guid Id, Guid? FacilityId);
