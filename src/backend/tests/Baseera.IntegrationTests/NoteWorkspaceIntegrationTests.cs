using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Baseera.IntegrationTests;

/// <summary>
/// Phase 1A (Observation Workspace foundation): covers the endpoints and behaviors that
/// NotesCoreIntegrationTests/NotesAdditionalIntegrationTests do not already exercise —
/// GET /notes/workspace, GET /notes/{id}/workspace, facility+unit inheritance on create,
/// rejection of client-supplied scope tampering at the HTTP layer, the VERIFY_CLOSURE allowed-action
/// gap fix, and the bounded (removed placeholder fields / capped timeline) detail read model.
/// Generic RBAC/redaction/pagination coverage for the plain /notes endpoints is intentionally not
/// duplicated here.
/// </summary>
[Collection(OperationsIntegrationCollection.Name)]
public sealed class NoteWorkspaceIntegrationTests(OperationsIntegrationFixture fixture)
    : IntegrationTestBase<OperationsIntegrationFixture>(fixture)
{
    private readonly BaseeraApiFactory _factory = fixture.Factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [IntegrationConnectionFact]
    public async Task Create_without_unit_scopes_to_facility_and_is_visible_in_workspace_list()
    {
        await _factory.SeedUserAsync("ws-admin-1", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ws-admin-1");

        var note = await CreateNoteAsync(admin, SeedIds.FacilityA1, facilityUnitId: null, "ملاحظة بلا وحدة");

        var detail = await admin.GetFromJsonAsync<WorkspaceDetailResponse>($"/api/v1/notes/{note.Id}/workspace", JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(ScopeType.Facility, detail!.Note.ScopeType);
        Assert.Null(detail.Note.FacilityUnitId);

        var list = await admin.GetFromJsonAsync<WorkspaceListEnvelope>(
            $"/api/v1/notes/workspace?facilityId={SeedIds.FacilityA1}&pageSize=50", JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list!.Notes.Items, n => n.Id == note.Id);
    }

    [IntegrationConnectionFact]
    public async Task Create_with_facility_unit_inherits_unit_scope_and_filters_by_unit_in_workspace_list()
    {
        await _factory.SeedUserAsync("ws-admin-2", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ws-admin-2");

        var note = await CreateNoteAsync(admin, SeedIds.FacilityA1, SeedIds.FacilityA1UnitNorth, "ملاحظة عنبر الشمال");

        var detail = await admin.GetFromJsonAsync<WorkspaceDetailResponse>($"/api/v1/notes/{note.Id}/workspace", JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(ScopeType.FacilityUnit, detail!.Note.ScopeType);
        Assert.Equal(SeedIds.FacilityA1UnitNorth, detail.Note.FacilityUnitId);

        var matchingUnit = await admin.GetFromJsonAsync<WorkspaceListEnvelope>(
            $"/api/v1/notes/workspace?facilityUnitId={SeedIds.FacilityA1UnitNorth}&pageSize=50", JsonOptions);
        Assert.Contains(matchingUnit!.Notes.Items, n => n.Id == note.Id);

        var otherUnit = await admin.GetFromJsonAsync<WorkspaceListEnvelope>(
            $"/api/v1/notes/workspace?facilityUnitId={SeedIds.FacilityA1UnitSouth}&pageSize=50", JsonOptions);
        Assert.DoesNotContain(otherUnit!.Notes.Items, n => n.Id == note.Id);
    }

    [IntegrationConnectionFact]
    public async Task Create_rejects_client_supplied_facility_outside_the_callers_scope_over_http()
    {
        await _factory.SeedUserAsync(
            "ws-scoped-creator",
            "منسق سجن أ",
            [RoleCodes.FacilityCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var client = _factory.CreateAuthenticatedClient("ws-scoped-creator");

        // Caller is only authorized for Facility A1; asking the server to create the note under
        // Facility B1 must be rejected regardless of what the request body claims — the FacilityId
        // in the request is presentation state, not an authorization source.
        var response = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title = "محاولة تلاعب بالنطاق",
            description = "وصف كافٍ للاختبار",
            noteTypeId = SeedIds.NoteTypeOperational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = Baseera.Domain.Attachments.ClassificationLevel.Internal,
            scopeType = ScopeType.Facility,
            regionId = SeedIds.RegionB,
            facilityId = SeedIds.FacilityB1,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Create_rejects_a_unit_that_belongs_to_a_different_facility_over_http()
    {
        await _factory.SeedUserAsync("ws-admin-3", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ws-admin-3");

        Guid foreignUnitId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            foreignUnitId = Guid.NewGuid();
            db.FacilityUnits.Add(new Baseera.Domain.Organization.FacilityUnit
            {
                Id = foreignUnitId,
                FacilityId = SeedIds.FacilityB1,
                Code = "WS-FOREIGN",
                NameAr = "وحدة خارجية",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var response = await admin.PostAsJsonAsync("/api/v1/notes", new
        {
            title = "وحدة من سجن آخر",
            description = "وصف كافٍ للاختبار",
            noteTypeId = SeedIds.NoteTypeOperational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = Baseera.Domain.Attachments.ClassificationLevel.Internal,
            scopeType = ScopeType.Facility,
            regionId = SeedIds.RegionA,
            facilityId = SeedIds.FacilityA1,
            facilityUnitId = foreignUnitId,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task VerifyClosure_allowed_action_appears_only_from_pending_verification_and_disappears_after_close()
    {
        await _factory.SeedUserAsync("ws-vc-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserWithPermissionsAsync(
            "ws-vc-worker",
            "معالج",
            [RoleCodes.FacilityCoordinator],
            [PermissionCodes.NotesVerifyClosure],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync(
            "ws-vc-verifier",
            "معتمد",
            [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var admin = _factory.CreateAuthenticatedClient("ws-vc-admin");
        var worker = _factory.CreateAuthenticatedClient("ws-vc-worker");
        var verifier = _factory.CreateAuthenticatedClient("ws-vc-verifier");

        Guid workerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            workerId = await db.Users.Where(u => u.ExternalSubject == "ws-vc-worker").Select(u => u.Id).FirstAsync();
            await GrantCanReviewTypeAsync(db, "ws-vc-worker");
        }

        var note = await CreateNoteAsync(admin, SeedIds.FacilityA1, null, "جاهزة للتحقق");
        note = await TransitionAsync(admin, $"/api/v1/notes/{note.Id}/submit", note.RowVersion);
        note = await DecideTriageValidAsync(admin, note.Id, note.RowVersion);
        note = await AssignAsync(verifier, note.Id, workerId, note.RowVersion);

        var beforeVerification = await admin.GetFromJsonAsync<WorkspaceDetailResponse>($"/api/v1/notes/{note.Id}/workspace", JsonOptions);
        Assert.DoesNotContain("VERIFY_CLOSURE", beforeVerification!.AllowedActions);

        note = await TransitionAsync(worker, $"/api/v1/notes/{note.Id}/start-work", note.RowVersion);
        note = await RecordDirectTreatmentAsync(worker, note.Id, note.RowVersion);
        note = await TransitionAsync(worker, $"/api/v1/notes/{note.Id}/submit-for-verification", note.RowVersion);

        var pendingVerification = await verifier.GetFromJsonAsync<WorkspaceDetailResponse>($"/api/v1/notes/{note.Id}/workspace", JsonOptions);
        Assert.Contains("VERIFY_CLOSURE", pendingVerification!.AllowedActions);

        var closeResponse = await verifier.PostAsJsonAsync($"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "اعتماد الإغلاق",
            closureSummary = "تم التحقق من المعالجة",
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var afterClose = await verifier.GetFromJsonAsync<WorkspaceDetailResponse>($"/api/v1/notes/{note.Id}/workspace", JsonOptions);
        Assert.DoesNotContain("VERIFY_CLOSURE", afterClose!.AllowedActions);
        Assert.Equal(NoteStatus.Closed, afterClose.Note.Status);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_detail_has_no_resource_decision_or_link_fields_and_caps_timeline_at_thirty()
    {
        await _factory.SeedUserAsync("ws-timeline-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ws-timeline-admin");

        var note = await CreateNoteAsync(admin, SeedIds.FacilityA1, null, "ملاحظة تاريخ طويل");
        note = await TransitionAsync(admin, $"/api/v1/notes/{note.Id}/submit", note.RowVersion);
        note = await DecideTriageValidAsync(admin, note.Id, note.RowVersion);

        Guid workerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            workerId = await db.Users.Where(u => u.ExternalSubject == "ws-timeline-admin").Select(u => u.Id).FirstAsync();
        }
        note = await AssignAsync(admin, note.Id, workerId, note.RowVersion);
        note = await TransitionAsync(admin, $"/api/v1/notes/{note.Id}/start-work", note.RowVersion);
        note = await RecordDirectTreatmentAsync(admin, note.Id, note.RowVersion);

        // Bounce PendingVerification <-> InProgress repeatedly to accumulate more than
        // TimelinePreviewLimit (30) status-history rows via real, valid transitions.
        for (var i = 0; i < 20; i++)
        {
            note = await TransitionAsync(admin, $"/api/v1/notes/{note.Id}/submit-for-verification", note.RowVersion);
            note = await TransitionAsync(admin, $"/api/v1/notes/{note.Id}/return-for-rework", note.RowVersion);
        }

        var response = await admin.GetAsync($"/api/v1/notes/{note.Id}/workspace");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        using var document = JsonDocument.Parse(body);
        Assert.False(document.RootElement.TryGetProperty("resources", out _));
        Assert.False(document.RootElement.TryGetProperty("decisions", out _));
        Assert.False(document.RootElement.TryGetProperty("links", out _));

        var detail = JsonSerializer.Deserialize<WorkspaceDetailResponse>(body, JsonOptions);
        Assert.Equal(30, detail!.Timeline.Count);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_detail_returns_404_for_an_out_of_scope_note()
    {
        await _factory.SeedUserAsync("ws-oos-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync(
            "ws-oos-region-a",
            "منطقة أ",
            [RoleCodes.RegionalDirector],
            (ScopeType.Region, SeedIds.RegionA, null));

        var admin = _factory.CreateAuthenticatedClient("ws-oos-admin");
        var note = await CreateNoteAsync(admin, SeedIds.FacilityB1, null, "خارج نطاق منطقة أ");

        var regionA = _factory.CreateAuthenticatedClient("ws-oos-region-a");
        var response = await regionA.GetAsync($"/api/v1/notes/{note.Id}/workspace");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_list_supports_pagination_sorting_and_search()
    {
        await _factory.SeedUserAsync("ws-list-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ws-list-admin");

        var titles = new[] { "ألف بحث فريد", "باء بحث فريد", "جيم بحث فريد" };
        foreach (var title in titles)
        {
            await CreateNoteAsync(admin, SeedIds.FacilityA1, null, title);
        }

        var page1 = await admin.GetFromJsonAsync<WorkspaceListEnvelope>(
            "/api/v1/notes/workspace?facilityId=" + SeedIds.FacilityA1 + "&search=" + Uri.EscapeDataString("بحث فريد") + "&pageSize=2&page=1&sortBy=createdAtUtc&sortDesc=true",
            JsonOptions);
        Assert.NotNull(page1);
        Assert.Equal(2, page1!.Notes.Items.Count);
        Assert.Equal(3, page1.Notes.TotalCount);

        var page2 = await admin.GetFromJsonAsync<WorkspaceListEnvelope>(
            "/api/v1/notes/workspace?facilityId=" + SeedIds.FacilityA1 + "&search=" + Uri.EscapeDataString("بحث فريد") + "&pageSize=2&page=2&sortBy=createdAtUtc&sortDesc=true",
            JsonOptions);
        Assert.Single(page2!.Notes.Items);
        Assert.DoesNotContain(page2.Notes.Items, n => page1.Notes.Items.Any(p => p.Id == n.Id));
    }

    [IntegrationConnectionFact]
    public async Task Workspace_list_query_count_is_bounded_and_independent_of_note_volume()
    {
        var counter = new SqlCommandCounter();
        await using var factory = BaseeraApiFactory.WithInterceptor(counter);
        await factory.SeedUserAsync("ws-qc-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var client = factory.CreateAuthenticatedClient("ws-qc-admin");

        await CreateNoteAsync(client, SeedIds.FacilityA1, null, "حجم صغير");
        counter.Reset();
        var small = await client.GetAsync($"/api/v1/notes/workspace?facilityId={SeedIds.FacilityA1}&pageSize=20");
        small.EnsureSuccessStatusCode();
        var smallCount = counter.SelectCount;

        for (var i = 0; i < 25; i++)
        {
            await CreateNoteAsync(client, SeedIds.FacilityA1, null, $"حجم كبير {i}");
        }
        counter.Reset();
        var large = await client.GetAsync($"/api/v1/notes/workspace?facilityId={SeedIds.FacilityA1}&pageSize=20");
        large.EnsureSuccessStatusCode();

        Assert.Equal(smallCount, counter.SelectCount);
        Assert.InRange(counter.SelectCount, 1, 20);
    }

    [IntegrationConnectionFact]
    public async Task Workspace_detail_query_count_is_bounded_and_independent_of_related_row_volume()
    {
        var counter = new SqlCommandCounter();
        await using var factory = BaseeraApiFactory.WithInterceptor(counter);
        await factory.SeedUserAsync("ws-qc-detail-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var client = factory.CreateAuthenticatedClient("ws-qc-detail-admin");

        var smallNote = await CreateNoteAsync(client, SeedIds.FacilityA1, null, "تفاصيل قليلة");
        await TransitionAsync(client, $"/api/v1/notes/{smallNote.Id}/submit", smallNote.RowVersion);
        await CreateCorrectiveActionAsync(client, smallNote.Id, "إجراء واحد");
        await UploadAttachmentAsync(client, smallNote.Id, "one.txt");

        counter.Reset();
        var small = await client.GetAsync($"/api/v1/notes/{smallNote.Id}/workspace");
        small.EnsureSuccessStatusCode();
        var smallCount = counter.SelectCount;

        var largeNote = await CreateNoteAsync(client, SeedIds.FacilityA1, null, "تفاصيل كثيرة");
        await TransitionAsync(client, $"/api/v1/notes/{largeNote.Id}/submit", largeNote.RowVersion);
        for (var i = 0; i < 12; i++)
        {
            await CreateCorrectiveActionAsync(client, largeNote.Id, $"إجراء {i}");
            await UploadAttachmentAsync(client, largeNote.Id, $"file-{i}.txt");
        }

        counter.Reset();
        var large = await client.GetAsync($"/api/v1/notes/{largeNote.Id}/workspace");
        large.EnsureSuccessStatusCode();

        // Detail combines note + assignments + history + corrective actions + attachments + an
        // open-actions count — a fixed number of round trips regardless of related-row volume (the
        // Equal assertion below is what actually proves "no N+1"), never one-query-per-related-row.
        Assert.Equal(smallCount, counter.SelectCount);
        Assert.InRange(counter.SelectCount, 1, 40);
    }

    private static async Task CreateCorrectiveActionAsync(HttpClient client, Guid noteId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/notes/{noteId}/corrective-actions", new
        {
            title,
            description = "وصف إجراء تصحيحي كافٍ للاختبار",
            priority = CorrectiveActionPriority.Medium,
            classification = (ClassificationLevel?)null,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static async Task UploadAttachmentAsync(HttpClient client, Guid noteId, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("OperationalNote"), "entityType");
        content.Add(new StringContent(noteId.ToString()), "entityId");
        content.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("حمولة اختبار"))
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") }
        }, "file", fileName);

        var response = await client.PostAsync("/api/v1/attachments", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static async Task GrantCanReviewTypeAsync(BaseeraDbContext db, string subject)
    {
        var userId = await db.Users.Where(u => u.ExternalSubject == subject).Select(u => u.Id).FirstAsync();
        db.UserNoteTypeOverrides.Add(new UserNoteTypeOverride
        {
            UserId = userId,
            NoteTypeId = SeedIds.NoteTypeOperational,
            CanViewOverride = true,
            CanReviewOverride = true,
            IsActive = true,
            Reason = "اختبار مساحة العمل"
        });
        await db.SaveChangesAsync();
    }

    private static async Task<WorkspaceNote> CreateNoteAsync(HttpClient client, Guid facilityId, Guid? facilityUnitId, string title)
    {
        var regionId = facilityId == SeedIds.FacilityB1 ? SeedIds.RegionB : SeedIds.RegionA;
        var response = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "وصف تفصيلي كافٍ للاختبار",
            noteTypeId = SeedIds.NoteTypeOperational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = Baseera.Domain.Attachments.ClassificationLevel.Internal,
            scopeType = facilityUnitId.HasValue ? ScopeType.FacilityUnit : ScopeType.Facility,
            regionId,
            facilityId,
            facilityUnitId,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<WorkspaceNote>(body, JsonOptions)!;
    }

    private static async Task<WorkspaceNote> TransitionAsync(HttpClient client, string url, string rowVersion)
    {
        var response = await client.PostAsJsonAsync(url, new { reason = (string?)"اختبار", rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<WorkspaceNote>(body, JsonOptions)!;
    }

    /// <summary>Phase 1B mandatory pre-condition for SUBMIT_FOR_VERIFICATION — see DecideTriageValidAsync
    /// in NotesCoreIntegrationTests.cs for the same rationale.</summary>
    private static async Task<WorkspaceNote> DecideTriageValidAsync(HttpClient client, Guid noteId, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/notes/{noteId}/triage/valid", new { rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<WorkspaceNote>(body, JsonOptions)!;
    }

    private static async Task<WorkspaceNote> RecordDirectTreatmentAsync(HttpClient client, Guid noteId, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/notes/{noteId}/treatment/result", new
        {
            treatmentResultText = "تمت المعالجة المباشرة.",
            executionType = NoteTreatmentExecutionType.Direct,
            rowVersion
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<WorkspaceNote>(body, JsonOptions)!;
    }

    private static async Task<WorkspaceNote> AssignAsync(HttpClient client, Guid noteId, Guid userId, string rowVersion)
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
        return JsonSerializer.Deserialize<WorkspaceNote>(body, JsonOptions)!;
    }

    private sealed class SqlCommandCounter : DbCommandInterceptor
    {
        public int SelectCount { get; private set; }

        public void Reset() => SelectCount = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            CountIfSelect(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CountIfSelect(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void CountIfSelect(string? text)
        {
            if (!string.IsNullOrWhiteSpace(text) && text.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                SelectCount++;
            }
        }
    }
}

internal sealed record WorkspaceNote(Guid Id, string ReferenceNumber, NoteStatus Status, string RowVersion);

internal sealed record WorkspaceListEnvelope(WorkspacePagedNotes Notes);

internal sealed record WorkspacePagedNotes(IReadOnlyList<WorkspaceNote> Items, int Page, int PageSize, int TotalCount);

internal sealed record WorkspaceDetailResponse(
    WorkspaceNoteDetail Note,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<WorkspaceTimelineEntry> Timeline);

internal sealed record WorkspaceNoteDetail(
    Guid Id,
    NoteStatus Status,
    ScopeType ScopeType,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    string RowVersion);

internal sealed record WorkspaceTimelineEntry(Guid Id, string Type, DateTimeOffset OccurredAtUtc);
