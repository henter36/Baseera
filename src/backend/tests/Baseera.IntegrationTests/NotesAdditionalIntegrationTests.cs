using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

/// <summary>
/// Complements NotesCoreIntegrationTests.cs: facility/unit scope isolation, archive/restore,
/// sensitive list redaction, atomic audit logging, and assignment validation over HTTP.
/// </summary>
public sealed class NotesAdditionalIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NotesAdditionalIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task List_notes_binds_AsParameters_filters_and_defaults()
    {
        await _factory.SeedUserAsync("notes-list-bind-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("notes-list-bind-admin");

        async Task<NoteDetail> CreateWithSeverity(string title, NoteSeverity severity, Guid facilityId, DateTimeOffset? dueAtUtc = null)
        {
            var response = await admin.PostAsJsonAsync("/api/v1/notes", new
            {
                title,
                description = "وصف تفصيلي إضافي للاختبار",
                category = NoteCategory.Operational,
                severity,
                sourceType = NoteSourceType.Manual,
                sourceReference = (string?)null,
                classification = ClassificationLevel.Internal,
                scopeType = ScopeType.Facility,
                regionId = SeedIds.RegionA,
                facilityId,
                facilityUnitId = (Guid?)null,
                ownerDepartmentId = (Guid?)null,
                dueAtUtc = dueAtUtc ?? DateTimeOffset.UtcNow.AddDays(3)
            });
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, body);
            return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
        }

        var openHigh = await CreateWithSeverity("مفتوحة عالية", NoteSeverity.High, SeedIds.FacilityA1);
        await PostAsync(admin, $"/api/v1/notes/{openHigh.Id}/submit", openHigh.RowVersion, "تقديم");
        openHigh = (await admin.GetFromJsonAsync<NoteDetail>($"/api/v1/notes/{openHigh.Id}", JsonOptions))!;

        var draftLow = await CreateWithSeverity("مسودة منخفضة", NoteSeverity.Low, SeedIds.FacilityA2);

        var overdueDraft = await CreateWithSeverity("متأخرة", NoteSeverity.Medium, SeedIds.FacilityA1, DateTimeOffset.UtcNow.AddDays(-2));
        await PostAsync(admin, $"/api/v1/notes/{overdueDraft.Id}/submit", overdueDraft.RowVersion, "تقديم متأخرة");

        var defaults = await admin.GetFromJsonAsync<PagedEnvelope<NoteListItem>>("/api/v1/notes");
        Assert.NotNull(defaults);
        Assert.Equal(1, defaults!.Page);
        Assert.Equal(20, defaults.PageSize);

        var filtered = await admin.GetFromJsonAsync<PagedEnvelope<NoteListItem>>(
            $"/api/v1/notes?page=1&pageSize=10&status={(int)NoteStatus.Open}&severity={(int)NoteSeverity.High}&facilityId={SeedIds.FacilityA1}&overdueOnly=false&sortBy=severity&sortDesc=true&dueFrom={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-30).ToString("O"))}&dueTo={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(30).ToString("O"))}");
        Assert.NotNull(filtered);
        Assert.Equal(1, filtered!.Page);
        Assert.Equal(10, filtered.PageSize);
        Assert.Contains(filtered.Items, n => n.Id == openHigh.Id);
        Assert.DoesNotContain(filtered.Items, n => n.Id == draftLow.Id);

        var overdueOnly = await admin.GetFromJsonAsync<PagedEnvelope<NoteListItem>>(
            "/api/v1/notes?overdueOnly=true&page=1&pageSize=50");
        Assert.NotNull(overdueOnly);
        Assert.Contains(overdueOnly!.Items, n => n.Id == overdueDraft.Id);
    }

    [IntegrationConnectionFact]
    public async Task Facility_user_cannot_see_note_from_other_facility_in_same_region()
    {
        await _factory.SeedUserAsync("notes-fac-a1", "سجن أ1", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync("notes-fac-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var admin = _factory.CreateAuthenticatedClient("notes-fac-admin");
        var noteA2 = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA2, null, "ملاحظة سجن أ2");

        var facA1 = _factory.CreateAuthenticatedClient("notes-fac-a1");
        var detail = await facA1.GetAsync($"/api/v1/notes/{noteA2.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Region_user_can_see_all_facilities_in_own_region()
    {
        await _factory.SeedUserAsync("notes-region-a2", "منطقة أ", [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));
        await _factory.SeedUserAsync("notes-region-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var admin = _factory.CreateAuthenticatedClient("notes-region-admin");
        var noteA1 = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "ملاحظة سجن أ1-ب");
        var noteA2 = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA2, null, "ملاحظة سجن أ2-ب");

        var regionUser = _factory.CreateAuthenticatedClient("notes-region-a2");
        var okA1 = await regionUser.GetAsync($"/api/v1/notes/{noteA1.Id}");
        var okA2 = await regionUser.GetAsync($"/api/v1/notes/{noteA2.Id}");
        Assert.Equal(HttpStatusCode.OK, okA1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, okA2.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Global_scope_sees_notes_across_every_region_and_headquarters()
    {
        await _factory.SeedUserAsync("notes-global-user", "وطني", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));

        var admin = _factory.CreateAuthenticatedClient("notes-global-user");
        var hqNote = await CreateNoteAsync(admin, ScopeType.Headquarters, null, null, null, "ملاحظة رئيسية");
        var regionNote = await CreateNoteAsync(admin, ScopeType.Region, SeedIds.RegionB, null, null, "ملاحظة منطقة ب");

        var hqDetail = await admin.GetAsync($"/api/v1/notes/{hqNote.Id}");
        var regionDetail = await admin.GetAsync($"/api/v1/notes/{regionNote.Id}");
        Assert.Equal(HttpStatusCode.OK, hqDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, regionDetail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Archive_then_restore_round_trips_and_archived_note_is_hidden_from_list()
    {
        await _factory.SeedUserAsync("notes-archive-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("notes-archive-admin");
        var note = await CreateNoteAsync(client, ScopeType.Global, null, null, null, "قيد الأرشفة");

        var cancel = await PostAsync(client, $"/api/v1/notes/{note.Id}/submit", note.RowVersion, "تقديم");
        var cancelled = await PostAsync(client, $"/api/v1/notes/{note.Id}/cancel", cancel.RowVersion, "إلغاء تحضيرًا للأرشفة");

        var archiveResponse = await client.PostAsJsonAsync($"/api/v1/notes/{cancelled.Id}/archive", new { reason = "أرشفة", rowVersion = cancelled.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var afterArchive = await client.GetAsync($"/api/v1/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterArchive.StatusCode);

        var list = await client.GetFromJsonAsync<PagedEnvelope<NoteListItem>>("/api/v1/notes?page=1&pageSize=200");
        Assert.NotNull(list);
        Assert.DoesNotContain(list!.Items, n => n.Id == note.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var archived = await db.OperationalNotes.IgnoreQueryFilters().FirstAsync(n => n.Id == note.Id);
        var restoreResponse = await client.PostAsJsonAsync(
            $"/api/v1/notes/{note.Id}/restore",
            new { reason = "استعادة", rowVersion = Convert.ToBase64String(archived.RowVersion) });
        Assert.Equal(HttpStatusCode.NoContent, restoreResponse.StatusCode);

        var afterRestore = await client.GetAsync($"/api/v1/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.OK, afterRestore.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Archiving_without_permission_is_forbidden()
    {
        await _factory.SeedUserAsync("notes-archive-admin2", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("notes-no-archive", "بلا أرشفة", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var admin = _factory.CreateAuthenticatedClient("notes-archive-admin2");
        var note = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "بدون صلاحية أرشفة");

        var coordinator = _factory.CreateAuthenticatedClient("notes-no-archive");
        var response = await coordinator.PostAsJsonAsync($"/api/v1/notes/{note.Id}/archive", new { reason = "محاولة", rowVersion = note.RowVersion });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Sensitive_note_is_redacted_in_list_for_viewer_without_view_sensitive()
    {
        await _factory.SeedUserAsync("notes-sensitive-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("notes-sensitive-viewer", "مشاهد", [RoleCodes.ReadOnlyUser],
            (ScopeType.Global, null, null));

        var admin = _factory.CreateAuthenticatedClient("notes-sensitive-admin");
        var response = await admin.PostAsJsonAsync("/api/v1/notes", new
        {
            title = "معلومة سرية جدًا",
            description = "تفاصيل حساسة للغاية",
            category = NoteCategory.Security,
            severity = NoteSeverity.High,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = ClassificationLevel.Secret,
            scopeType = ScopeType.Global,
            regionId = (Guid?)null,
            facilityId = (Guid?)null,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null
        });
        Assert.True(response.IsSuccessStatusCode);
        var note = JsonSerializer.Deserialize<NoteDetail>(await response.Content.ReadAsStringAsync(), JsonOptions)!;

        var viewer = _factory.CreateAuthenticatedClient("notes-sensitive-viewer");
        var list = await viewer.GetFromJsonAsync<PagedEnvelope<SensitiveNoteListItem>>("/api/v1/notes?page=1&pageSize=200");
        Assert.NotNull(list);
        var item = Assert.Single(list!.Items, n => n.Id == note.Id);
        Assert.True(item.IsSensitiveRedacted);
        Assert.Equal("[محجوب]", item.Title);

        var detail = await viewer.GetFromJsonAsync<SensitiveNoteDetail>($"/api/v1/notes/{note.Id}");
        Assert.NotNull(detail);
        Assert.True(detail!.IsSensitiveRedacted);
        Assert.Equal("[محجوب]", detail.Title);
    }

    [IntegrationConnectionFact]
    public async Task Assigning_to_a_user_lacking_work_permission_is_rejected()
    {
        await _factory.SeedUserAsync("notes-assign-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("notes-assign-nowork", "بلا صلاحية عمل", [RoleCodes.ReadOnlyUser],
            (ScopeType.Global, null, null));

        var admin = _factory.CreateAuthenticatedClient("notes-assign-admin");
        var note = await CreateNoteAsync(admin, ScopeType.Global, null, null, null, "تكليف خاطئ");
        var submitted = await PostAsync(admin, $"/api/v1/notes/{note.Id}/submit", note.RowVersion, "تقديم");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var noWorkUserId = await db.Users.Where(u => u.ExternalSubject == "notes-assign-nowork").Select(u => u.Id).FirstAsync();

        var response = await admin.PostAsJsonAsync($"/api/v1/notes/{submitted.Id}/assign", new
        {
            assignedToUserId = noWorkUserId,
            assignedToDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            reason = "تكليف",
            rowVersion = submitted.RowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Assign_request_without_user_or_department_fails_validation()
    {
        await _factory.SeedUserAsync("notes-assign-xor", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("notes-assign-xor");
        var note = await CreateNoteAsync(admin, ScopeType.Global, null, null, null, "تكليف بلا هدف");
        var submitted = await PostAsync(admin, $"/api/v1/notes/{note.Id}/submit", note.RowVersion, "تقديم");

        var response = await admin.PostAsJsonAsync($"/api/v1/notes/{submitted.Id}/assign", new
        {
            assignedToUserId = (Guid?)null,
            assignedToDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            reason = "بلا هدف",
            rowVersion = submitted.RowVersion
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Reassignment_ends_previous_assignment_and_is_visible_in_assignments_history()
    {
        await _factory.SeedUserAsync("notes-reassign-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserWithPermissionsAsync(
            "notes-reassign-worker1", "معالج1", [RoleCodes.FacilityCoordinator], [],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserWithPermissionsAsync(
            "notes-reassign-worker2", "معالج2", [RoleCodes.FacilityCoordinator], [],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var admin = _factory.CreateAuthenticatedClient("notes-reassign-admin");
        var note = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "إعادة تكليف");
        var submitted = await PostAsync(admin, $"/api/v1/notes/{note.Id}/submit", note.RowVersion, "تقديم");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var worker1Id = await db.Users.Where(u => u.ExternalSubject == "notes-reassign-worker1").Select(u => u.Id).FirstAsync();
        var worker2Id = await db.Users.Where(u => u.ExternalSubject == "notes-reassign-worker2").Select(u => u.Id).FirstAsync();

        var firstAssign = await admin.PostAsJsonAsync($"/api/v1/notes/{submitted.Id}/assign", new
        {
            assignedToUserId = worker1Id,
            assignedToDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            reason = "تكليف أول",
            rowVersion = submitted.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, firstAssign.StatusCode);
        var firstNote = JsonSerializer.Deserialize<NoteDetail>(await firstAssign.Content.ReadAsStringAsync(), JsonOptions)!;

        var secondAssign = await admin.PostAsJsonAsync($"/api/v1/notes/{submitted.Id}/assign", new
        {
            assignedToUserId = worker2Id,
            assignedToDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            reason = "إعادة تكليف لمعالج آخر",
            rowVersion = firstNote.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, secondAssign.StatusCode);

        var assignments = await admin.GetFromJsonAsync<List<NoteAssignmentItem>>($"/api/v1/notes/{submitted.Id}/assignments");
        Assert.NotNull(assignments);
        Assert.Equal(2, assignments!.Count);
        Assert.Single(assignments, a => a.IsCurrent && a.AssignedToUserId == worker2Id);
        Assert.Single(assignments, a => !a.IsCurrent && a.AssignedToUserId == worker1Id);
    }

    [IntegrationConnectionFact]
    public async Task Note_creation_writes_an_atomic_audit_entry()
    {
        await _factory.SeedUserAsync("notes-audit-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("notes-audit-admin");
        var note = await CreateNoteAsync(client, ScopeType.Global, null, null, null, "تدقيق الإنشاء");

        var audits = await client.GetFromJsonAsync<PagedEnvelope<AuditItem>>("/api/v1/audit-logs?module=Notes&page=1&pageSize=50");
        Assert.NotNull(audits);
        Assert.Contains(audits!.Items, a => a.Action == "NoteCreated" && a.EntityId == note.Id.ToString());
    }

    [IntegrationConnectionFact]
    public async Task Note_status_history_records_every_transition_in_order()
    {
        await _factory.SeedUserAsync("notes-history-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("notes-history-admin");
        var note = await CreateNoteAsync(client, ScopeType.Global, null, null, null, "تسلسل الحالات");

        var submitted = await PostAsync(client, $"/api/v1/notes/{note.Id}/submit", note.RowVersion, "تقديم");
        await PostAsync(client, $"/api/v1/notes/{note.Id}/cancel", submitted.RowVersion, "إلغاء");

        var history = await client.GetFromJsonAsync<List<NoteHistoryItem>>($"/api/v1/notes/{note.Id}/history");
        Assert.NotNull(history);
        Assert.Equal(3, history!.Count);
        Assert.Equal(NoteStatus.Draft, history[0].ToStatus);
        Assert.Equal(NoteStatus.Open, history[1].ToStatus);
        Assert.Equal(NoteStatus.Cancelled, history[2].ToStatus);
    }

    [IntegrationConnectionFact]
    public async Task Classification_query_param_filters_the_list_server_side()
    {
        await _factory.SeedUserAsync("notes-classif-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("notes-classif-admin");

        var internalResponse = await admin.PostAsJsonAsync("/api/v1/notes", NoteCreateBody("تصنيف داخلي", ClassificationLevel.Internal));
        Assert.True(internalResponse.IsSuccessStatusCode);
        var internalNote = JsonSerializer.Deserialize<NoteDetail>(await internalResponse.Content.ReadAsStringAsync(), JsonOptions)!;

        var restrictedResponse = await admin.PostAsJsonAsync("/api/v1/notes", NoteCreateBody("تصنيف مقيّد", ClassificationLevel.Restricted));
        Assert.True(restrictedResponse.IsSuccessStatusCode);
        var restrictedNote = JsonSerializer.Deserialize<NoteDetail>(await restrictedResponse.Content.ReadAsStringAsync(), JsonOptions)!;

        var filtered = await admin.GetFromJsonAsync<PagedEnvelope<NoteListItem>>(
            $"/api/v1/notes?classification={(int)ClassificationLevel.Restricted}&page=1&pageSize=50");
        Assert.NotNull(filtered);
        Assert.Contains(filtered!.Items, n => n.Id == restrictedNote.Id);
        Assert.DoesNotContain(filtered.Items, n => n.Id == internalNote.Id);
    }

    [IntegrationConnectionFact]
    public async Task Note_attachments_endpoint_lists_metadata_for_in_scope_note()
    {
        await _factory.SeedUserAsync("notes-attlist-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("notes-attlist-admin");
        var note = await CreateNoteAsync(admin, ScopeType.Global, null, null, null, "قائمة المرفقات");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("OperationalNote"), "entityType");
        content.Add(new StringContent(note.Id.ToString()), "entityId");
        content.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("محتوى تجريبي"))
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") }
        }, "file", "evidence.txt");
        var upload = await admin.PostAsync("/api/v1/attachments", content);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var attachment = JsonSerializer.Deserialize<AttachmentItem>(await upload.Content.ReadAsStringAsync(), JsonOptions)!;

        var list = await admin.GetFromJsonAsync<List<NoteAttachmentItem>>($"/api/v1/notes/{note.Id}/attachments");
        Assert.NotNull(list);
        var listed = Assert.Single(list!, a => a.Id == attachment.Id);
        Assert.Equal("evidence.txt", listed.OriginalFileName);
        Assert.Equal(0, listed.ScanStatus); // PendingScan until marked clean.
        Assert.False(listed.IsSensitiveRedacted);
    }

    [IntegrationConnectionFact]
    public async Task Note_attachments_list_redacts_confidential_metadata_without_download_sensitive()
    {
        await _factory.SeedUserAsync("notes-att-redact-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("notes-att-redact-viewer", "عارض", [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var admin = _factory.CreateAuthenticatedClient("notes-att-redact-admin");
        var note = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null, "مرفق سري");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("OperationalNote"), "entityType");
        content.Add(new StringContent(note.Id.ToString()), "entityId");
        content.Add(new StringContent(ClassificationLevel.Confidential.ToString()), "classification");
        content.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("سرّي"))
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") }
        }, "file", "secret-plan.txt");
        var upload = await admin.PostAsync("/api/v1/attachments", content);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var attachment = JsonSerializer.Deserialize<AttachmentItem>(await upload.Content.ReadAsStringAsync(), JsonOptions)!;

        var viewer = _factory.CreateAuthenticatedClient("notes-att-redact-viewer");
        var list = await viewer.GetFromJsonAsync<List<NoteAttachmentItem>>($"/api/v1/notes/{note.Id}/attachments");
        Assert.NotNull(list);
        var listed = Assert.Single(list!, a => a.Id == attachment.Id);
        Assert.True(listed.IsSensitiveRedacted);
        Assert.Equal("[محجوب]", listed.OriginalFileName);
        Assert.True(string.IsNullOrEmpty(listed.Sha256));
        Assert.Equal(0, listed.SizeBytes);
    }

    [IntegrationConnectionFact]
    public async Task Note_attachments_endpoint_returns_404_for_out_of_scope_note()
    {
        await _factory.SeedUserAsync("notes-attlist-admin2", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("notes-attlist-outsider", "خارج النطاق", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var admin = _factory.CreateAuthenticatedClient("notes-attlist-admin2");
        var note = await CreateNoteAsync(admin, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, null, "خارج النطاق للمرفقات");

        var outsider = _factory.CreateAuthenticatedClient("notes-attlist-outsider");
        var response = await outsider.GetAsync($"/api/v1/notes/{note.Id}/attachments");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object NoteCreateBody(string title, ClassificationLevel classification) => new
    {
        title,
        description = "وصف تفصيلي إضافي للاختبار",
        category = NoteCategory.Operational,
        severity = NoteSeverity.Medium,
        sourceType = NoteSourceType.Manual,
        sourceReference = (string?)null,
        classification,
        scopeType = ScopeType.Global,
        regionId = (Guid?)null,
        facilityId = (Guid?)null,
        facilityUnitId = (Guid?)null,
        ownerDepartmentId = (Guid?)null,
        dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
    };

    [IntegrationConnectionFact]
    public async Task List_sort_by_disallowed_key_falls_back_without_error()
    {
        await _factory.SeedUserAsync("notes-sort-admin", "مسؤول", [RoleCodes.SystemAdministrator],
            (ScopeType.Global, null, null));
        var client = _factory.CreateAuthenticatedClient("notes-sort-admin");
        await CreateNoteAsync(client, ScopeType.Global, null, null, null, "ترتيب");

        var response = await client.GetAsync("/api/v1/notes?sortBy=NotARealColumn&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
            description = "وصف تفصيلي إضافي للاختبار",
            category = NoteCategory.Operational,
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

    private static async Task<NoteDetail> PostAsync(HttpClient client, string url, string rowVersion, string reason)
    {
        var response = await client.PostAsJsonAsync(url, new { reason, rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }
}

internal sealed record SensitiveNoteListItem(Guid Id, string Title, bool IsSensitiveRedacted);
internal sealed record SensitiveNoteDetail(Guid Id, string Title, bool IsSensitiveRedacted);
internal sealed record NoteAssignmentItem(Guid Id, Guid? AssignedToUserId, bool IsCurrent);
internal sealed record NoteAttachmentItem(
    Guid Id,
    string OriginalFileName,
    int ScanStatus,
    int Classification,
    string? Sha256 = null,
    long SizeBytes = 0,
    bool IsSensitiveRedacted = false);
