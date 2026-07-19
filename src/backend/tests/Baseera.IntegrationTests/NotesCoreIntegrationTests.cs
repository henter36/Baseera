using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class NotesCoreIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NotesCoreIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Region_user_cannot_see_other_region_note()
    {
        await _factory.SeedUserAsync("notes-region-a", "منطقة أ", [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserAsync("notes-admin-create", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var admin = _factory.CreateAuthenticatedClient("notes-admin-create");
        var createdB = await CreateNoteAsync(admin, ScopeType.Region, SeedIds.RegionB, null, null, "ملاحظة منطقة ب");

        var regionClient = _factory.CreateAuthenticatedClient("notes-region-a");
        var detail = await regionClient.GetAsync($"/api/v1/notes/{createdB.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var list = await regionClient.GetFromJsonAsync<PagedEnvelope<NoteListItem>>("/api/v1/notes?page=1&pageSize=50");
        Assert.NotNull(list);
        Assert.DoesNotContain(list!.Items, n => n.Id == createdB.Id);
    }

    [IntegrationConnectionFact]
    public async Task Headquarters_cannot_see_facility_notes_like_global()
    {
        await _factory.SeedUserAsync("notes-hq", "رئيسي", [RoleCodes.HeadquartersExecutive],
            (ScopeType.Headquarters, null, null));
        await _factory.SeedUserAsync("notes-admin-hq", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var admin = _factory.CreateAuthenticatedClient("notes-admin-hq");
        var facilityNote = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "ملاحظة سجن");

        var hq = _factory.CreateAuthenticatedClient("notes-hq");
        var detail = await hq.GetAsync($"/api/v1/notes/{facilityNote.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Happy_path_transitions_and_sod()
    {
        await _factory.SeedUserAsync("notes-creator", "منشئ", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserWithPermissionsAsync(
            "notes-worker",
            "معالج",
            [RoleCodes.FacilityCoordinator],
            [PermissionCodes.NotesVerifyClosure],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync("notes-verifier", "معتمد", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        Guid workerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            workerId = await db.Users.Where(u => u.ExternalSubject == "notes-worker").Select(u => u.Id).FirstAsync();
        }

        var creator = _factory.CreateAuthenticatedClient("notes-creator");
        var note = await CreateNoteAsync(creator, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "ملاحظة حرجة", NoteSeverity.Critical);
        note = await PostTransitionAsync(creator, $"/api/v1/notes/{note.Id}/submit", note.RowVersion, "تقديم");

        var assigner = _factory.CreateAuthenticatedClient("notes-verifier");
        note = await AssignAsync(assigner, note.Id, workerId, note.RowVersion);

        var worker = _factory.CreateAuthenticatedClient("notes-worker");
        note = await PostWorkflowAsync(worker, $"/api/v1/notes/{note.Id}/start-work", note.RowVersion);
        note = await PostWorkflowAsync(worker, $"/api/v1/notes/{note.Id}/submit-for-verification", note.RowVersion);

        var sodDenied = await worker.PostAsJsonAsync($"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "محاولة إغلاق ذاتي",
            closureSummary = "لا يجب أن ينجح",
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, sodDenied.StatusCode);

        var closed = await assigner.PostAsJsonAsync($"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "اعتماد إغلاق",
            closureSummary = "تم التحقق من المعالجة",
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var closedBody = await closed.Content.ReadFromJsonAsync<NoteDetail>(JsonOptions);
        Assert.Equal(NoteStatus.Closed, closedBody!.Status);

        var history = await assigner.GetFromJsonAsync<List<NoteHistoryItem>>($"/api/v1/notes/{note.Id}/history");
        Assert.NotNull(history);
        Assert.Contains(history!, h => h.ToStatus == NoteStatus.Closed);
    }

    [IntegrationConnectionFact]
    public async Task Stale_rowversion_returns_409()
    {
        await _factory.SeedUserAsync("notes-conc", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("notes-conc");
        var note = await CreateNoteAsync(client, ScopeType.Global, null, null, null, "تعارض");
        var stale = note.RowVersion;

        var first = await client.PutAsJsonAsync($"/api/v1/notes/{note.Id}", new
        {
            title = "تحديث أول",
            description = note.Description,
            category = note.Category,
            severity = note.Severity,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = ClassificationLevel.Internal,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PutAsJsonAsync($"/api/v1/notes/{note.Id}", new
        {
            title = "تحديث متأخر",
            description = "وصف",
            category = NoteCategory.Operational,
            severity = NoteSeverity.Low,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = ClassificationLevel.Internal,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            rowVersion = stale
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Invalid_transition_returns_409()
    {
        await _factory.SeedUserAsync("notes-bad-tr", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("notes-bad-tr");
        var note = await CreateNoteAsync(client, ScopeType.Global, null, null, null, "انتقال باطل");

        var response = await client.PostAsJsonAsync($"/api/v1/notes/{note.Id}/start-work", new
        {
            reason = "لا يجوز",
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Attachment_cross_scope_and_pending_scan_blocked()
    {
        await _factory.SeedUserAsync("notes-att-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("notes-att-fac", "سجن", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var admin = _factory.CreateAuthenticatedClient("notes-att-admin");
        var note = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, null, "مرفق نطاق");

        var fac = _factory.CreateAuthenticatedClient("notes-att-fac");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("OperationalNote"), "entityType");
        content.Add(new StringContent(note.Id.ToString()), "entityId");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("hello attachment"))
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") }
        }, "file", "note.txt");

        var upload = await fac.PostAsync("/api/v1/attachments", content);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);

        using var okContent = new MultipartFormDataContent();
        okContent.Add(new StringContent("OperationalNote"), "entityType");
        okContent.Add(new StringContent(note.Id.ToString()), "entityId");
        okContent.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("hello attachment"))
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") }
        }, "file", "note.txt");
        var okUpload = await admin.PostAsync("/api/v1/attachments", okContent);
        Assert.Equal(HttpStatusCode.Created, okUpload.StatusCode);
        var attachment = await okUpload.Content.ReadFromJsonAsync<AttachmentItem>(JsonOptions);
        Assert.NotNull(attachment);

        var pendingDownload = await admin.GetAsync($"/api/v1/attachments/{attachment!.Id}/download");
        Assert.Equal(HttpStatusCode.Forbidden, pendingDownload.StatusCode);

        await _factory.MarkAttachmentCleanAsync(attachment.Id);
        var cleanDownload = await admin.GetAsync($"/api/v1/attachments/{attachment.Id}/download");
        Assert.Equal(HttpStatusCode.OK, cleanDownload.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Status_history_append_only_enforced()
    {
        await _factory.SeedUserAsync("notes-hist", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("notes-hist");
        var note = await CreateNoteAsync(client, ScopeType.Global, null, null, null, "تاريخ");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var history = await db.NoteStatusHistories.FirstAsync(h => h.OperationalNoteId == note.Id);
        history.Reason = "تلاعب";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [IntegrationConnectionFact]
    public async Task Concurrent_reference_numbers_are_unique()
    {
        await _factory.SeedUserAsync("notes-ref", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var client = _factory.CreateAuthenticatedClient("notes-ref");
            return await CreateNoteAsync(client, ScopeType.Global, null, null, null, $"متزامن {i}");
        });

        var notes = await Task.WhenAll(tasks);
        Assert.Equal(5, notes.Select(n => n.ReferenceNumber).Distinct().Count());
        Assert.All(notes, n => Assert.StartsWith("OBS-", n.ReferenceNumber));
    }

    private static async Task<NoteDetail> CreateNoteAsync(
        HttpClient client,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        string title,
        NoteSeverity severity = NoteSeverity.Medium)
    {
        var response = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "وصف تفصيلي للملاحظة التشغيلية",
            category = NoteCategory.Operational,
            severity,
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

    private static async Task<NoteDetail> PostTransitionAsync(HttpClient client, string url, string rowVersion, string reason)
    {
        var response = await client.PostAsJsonAsync(url, new { reason, rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }

    private static async Task<NoteDetail> PostWorkflowAsync(HttpClient client, string url, string rowVersion)
    {
        var response = await client.PostAsJsonAsync(url, new { reason = (string?)null, rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }

    private static async Task<NoteDetail> AssignAsync(HttpClient client, Guid noteId, Guid userId, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/notes/{noteId}/assign", new
        {
            assignedToUserId = userId,
            assignedToDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            reason = "تكليف للمعالجة",
            rowVersion
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }
}

internal sealed record NoteListItem(Guid Id, string ReferenceNumber, string Title, NoteStatus Status);
internal sealed record NoteDetail(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    NoteStatus Status,
    NoteSeverity Severity,
    NoteCategory Category,
    string RowVersion);
internal sealed record NoteHistoryItem(NoteStatus ToStatus, string? Reason);
internal sealed record AttachmentItem(Guid Id, string OriginalFileName);
