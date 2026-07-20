using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class CorrectiveActionsCoreIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CorrectiveActionsCoreIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Create_list_and_detail_are_scoped_to_parent_note()
    {
        await _factory.SeedUserAsync("ca-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("ca-region-b", "منطقة ب", [RoleCodes.RegionalDirector], (ScopeType.Region, SeedIds.RegionB, null));

        var admin = _factory.CreateAuthenticatedClient("ca-admin");
        var note = await CreateOpenNoteAsync(admin, ScopeType.Region, SeedIds.RegionA, null, "ملاحظة أ");

        var created = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
        {
            title = "إصلاح إجراء",
            description = "وصف إجراء تصحيحي",
            priority = CorrectiveActionPriority.High,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var action = await ReadObjectAsync(created);
        Assert.StartsWith("CA-", action.GetProperty("referenceNumber").GetString());

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{note.Id}/corrective-actions");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());

        var outOfScope = _factory.CreateAuthenticatedClient("ca-region-b");
        var detail = await outOfScope.GetAsync($"/api/v1/corrective-actions/{action.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Create_under_draft_note_is_rejected()
    {
        await _factory.SeedUserAsync("ca-draft-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-draft-admin");
        var note = await CreateDraftNoteAsync(admin, ScopeType.Region, SeedIds.RegionA, null, "مسودة");

        var response = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
        {
            title = "إجراء",
            description = "وصف",
            priority = CorrectiveActionPriority.Medium
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Note_closure_is_blocked_until_corrective_actions_are_completed_or_cancelled()
    {
        await _factory.SeedUserAsync("ca-guard-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-guard-admin");
        var note = await CreateOpenNoteAsync(admin, ScopeType.Region, SeedIds.RegionA, null, "ملاحظة حارس");

        var created = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
        {
            title = "إجراء مانع",
            description = "وصف",
            priority = CorrectiveActionPriority.Medium
        });
        var action = await ReadObjectAsync(created);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var guardedNote = await db.OperationalNotes.SingleAsync(n => n.Id == note.Id);
            guardedNote.Status = NoteStatus.PendingVerification;
            var entity = await db.CorrectiveActions.SingleAsync(a => a.Id == action.GetProperty("id").GetGuid());
            entity.Status = CorrectiveActionStatus.Open;
            await db.SaveChangesAsync();
        }

        var pending = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{note.Id}");
        var blocked = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "اعتماد",
            closureSummary = "ملخص",
            rowVersion = pending.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var problem = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("إجراء تصحيحي", problem.GetProperty("detail").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            var entity = await db.CorrectiveActions.SingleAsync(a => a.Id == action.GetProperty("id").GetGuid());
            entity.Status = CorrectiveActionStatus.Completed;
            await db.SaveChangesAsync();
        }

        var refreshed = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{note.Id}");
        var closed = await admin.PostAsJsonAsync($"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "اعتماد",
            closureSummary = "ملخص",
            rowVersion = refreshed.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Critical_action_all_processing_participants_are_blocked_and_independent_verifier_completes()
    {
        await _factory.SeedUserAsync("ca-sod-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserWithPermissionsAsync("ca-sod-a", "معالج أ", [RoleCodes.FacilityCoordinator], [PermissionCodes.CorrectiveActionsVerifyCompletion], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserWithPermissionsAsync("ca-sod-b", "معالج ب", [RoleCodes.FacilityCoordinator], [PermissionCodes.CorrectiveActionsVerifyCompletion], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync("ca-sod-c", "معتمد مستقل", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        var ids = await UserIdsAsync("ca-sod-a", "ca-sod-b");
        var admin = _factory.CreateAuthenticatedClient("ca-sod-admin");
        var action = await CreateOpenActionAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, CorrectiveActionPriority.Critical, "حرج");
        action = await AssignActionAsync(admin, action.Id, ids["ca-sod-a"], action.RowVersion);
        action = await PostActionTransitionAsync(_factory.CreateAuthenticatedClient("ca-sod-a"), action.Id, "start-work", action.RowVersion);
        action = await PostActionCompletionAsync(_factory.CreateAuthenticatedClient("ca-sod-b"), action.Id, "submit-for-verification", action.RowVersion, "أرسل ب");

        await AssertVerifyRejectedWithoutCompletionAsync(_factory.CreateAuthenticatedClient("ca-sod-a"), action.Id, action.RowVersion);
        await AssertVerifyRejectedWithoutCompletionAsync(_factory.CreateAuthenticatedClient("ca-sod-b"), action.Id, action.RowVersion);

        action = await PostActionTransitionAsync(_factory.CreateAuthenticatedClient("ca-sod-c"), action.Id, "return-for-rework", action.RowVersion);
        action = await PostActionCompletionAsync(admin, action.Id, "submit-for-verification", action.RowVersion, "أرسل المدير");
        await AssertVerifyRejectedWithoutCompletionAsync(admin, action.Id, action.RowVersion);

        var completed = await PostActionCompletionAsync(_factory.CreateAuthenticatedClient("ca-sod-c"), action.Id, "verify-completion", action.RowVersion, "اعتماد مستقل");
        Assert.Equal(CorrectiveActionStatus.Completed, completed.Status);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.Equal(1, await db.CorrectiveActionStatusHistories.CountAsync(h => h.CorrectiveActionId == action.Id && h.ToStatus == CorrectiveActionStatus.Completed));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == action.Id.ToString() && a.Action == "CorrectiveActionCompleted"));
    }

    [IntegrationConnectionFact]
    public async Task Out_of_scope_verifier_returns_404_without_completion_side_effects()
    {
        await _factory.SeedUserAsync("ca-out-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("ca-out-worker", "معالج", [RoleCodes.FacilityCoordinator], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync("ca-out-verifier", "خارج النطاق", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1));

        var workerId = (await UserIdsAsync("ca-out-worker"))["ca-out-worker"];
        var admin = _factory.CreateAuthenticatedClient("ca-out-admin");
        var action = await CreateOpenActionAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, CorrectiveActionPriority.High, "نطاق");
        action = await AssignActionAsync(admin, action.Id, workerId, action.RowVersion);
        action = await PostActionTransitionAsync(_factory.CreateAuthenticatedClient("ca-out-worker"), action.Id, "start-work", action.RowVersion);
        action = await PostActionCompletionAsync(_factory.CreateAuthenticatedClient("ca-out-worker"), action.Id, "submit-for-verification", action.RowVersion, "جاهز");

        var response = await _factory.CreateAuthenticatedClient("ca-out-verifier").PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/verify-completion", new
        {
            reason = "خارج النطاق",
            completionSummary = "لا يجب",
            rowVersion = action.RowVersion
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await AssertNoCompletionSideEffectsAsync(action.Id);
    }

    [IntegrationConnectionFact]
    public async Task Assignment_targets_reject_invalid_users_and_archived_department()
    {
        await _factory.SeedUserAsync("ca-invalid-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("ca-disabled", "معطل", [RoleCodes.FacilityCoordinator], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync("ca-deleted", "محذوف", [RoleCodes.FacilityCoordinator], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync("ca-pending", "معلق", [RoleCodes.FacilityCoordinator], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync("ca-out-scope-assignee", "خارج", [RoleCodes.FacilityCoordinator], (ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1));
        await _factory.SetUserProvisioningAsync("ca-disabled", false, UserProvisioningStatus.Active);
        await _factory.SetUserProvisioningAsync("ca-pending", true, UserProvisioningStatus.Pending);
        await _factory.ArchiveUserAsync("ca-deleted");

        var ids = await UserIdsIncludingDeletedAsync("ca-disabled", "ca-deleted", "ca-pending", "ca-out-scope-assignee");
        var archivedDepartmentId = await CreateArchivedDepartmentAsync();
        var admin = _factory.CreateAuthenticatedClient("ca-invalid-admin");

        foreach (var userId in ids.Values)
        {
            var action = await CreateOpenActionAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, CorrectiveActionPriority.Medium, "تكليف مستخدم");
            var response = await admin.PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/assign", AssignmentPayload(userId, null, action.RowVersion));
            Assert.True(response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound);
            await AssertNoCurrentAssignmentAndStatusAsync(action.Id, CorrectiveActionStatus.Open);
        }

        var deptAction = await CreateOpenActionAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, CorrectiveActionPriority.Medium, "تكليف إدارة");
        var departmentResponse = await admin.PostAsJsonAsync($"/api/v1/corrective-actions/{deptAction.Id}/assign", AssignmentPayload(null, archivedDepartmentId, deptAction.RowVersion));
        Assert.Equal(HttpStatusCode.NotFound, departmentResponse.StatusCode);
        await AssertNoCurrentAssignmentAndStatusAsync(deptAction.Id, CorrectiveActionStatus.Open);
    }

    [IntegrationConnectionFact]
    public async Task Concurrent_reference_generation_produces_unique_numbers()
    {
        await _factory.SeedUserAsync("ca-ref-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-ref-admin");
        var note = await CreateOpenNoteAsync(admin, ScopeType.Region, SeedIds.RegionA, null, "مرجع");

        var tasks = Enumerable.Range(0, 8).Select(i =>
        {
            var client = _factory.CreateAuthenticatedClient("ca-ref-admin");
            return client.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
            {
                title = $"إجراء متزامن {i}",
                description = "وصف متزامن",
                priority = CorrectiveActionPriority.Medium
            });
        });

        var responses = await Task.WhenAll(tasks);
        var refs = new List<string>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            refs.Add((await response.Content.ReadFromJsonAsync<ActionDetail>(JsonOptions))!.ReferenceNumber);
        }

        Assert.Equal(refs.Count, refs.Distinct().Count());
        Assert.All(refs, r => Assert.Matches("^CA-[0-9]{8,}$", r));
    }

    [IntegrationConnectionFact]
    public async Task Concurrent_first_assignment_returns_one_success_and_one_conflict()
    {
        await _factory.SeedUserAsync("ca-assign-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("ca-assign-worker", "عامل", [RoleCodes.FacilityCoordinator], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var workerId = (await UserIdsAsync("ca-assign-worker"))["ca-assign-worker"];
        var admin = _factory.CreateAuthenticatedClient("ca-assign-admin");
        var action = await CreateOpenActionAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, CorrectiveActionPriority.Medium, "تزامن تكليف");

        var payload = AssignmentPayload(workerId, null, action.RowVersion);
        var responses = await Task.WhenAll(
            _factory.CreateAuthenticatedClient("ca-assign-admin").PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/assign", payload),
            _factory.CreateAuthenticatedClient("ca-assign-admin").PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/assign", payload));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.Equal(1, await db.CorrectiveActionAssignments.CountAsync(a => a.CorrectiveActionId == action.Id && a.IsCurrent));
    }

    [IntegrationConnectionFact]
    public async Task Note_cancellation_is_blocked_without_side_effects()
    {
        await _factory.SeedUserAsync("ca-cancel-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-cancel-admin");
        var action = await CreateOpenActionAsync(admin, ScopeType.Region, SeedIds.RegionA, null, CorrectiveActionPriority.Medium, "حارس إلغاء");
        var note = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{action.OperationalNoteId}");

        var response = await admin.PostAsJsonAsync($"/api/v1/notes/{action.OperationalNoteId}/cancel", new { reason = "إلغاء", rowVersion = note.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var entity = await db.OperationalNotes.SingleAsync(n => n.Id == action.OperationalNoteId);
        Assert.Equal(NoteStatus.Open, entity.Status);
        Assert.Null(entity.ClosedAtUtc);
        Assert.Null(entity.ClosedByUserId);
        Assert.Equal(0, await db.NoteStatusHistories.CountAsync(h => h.OperationalNoteId == action.OperationalNoteId && h.ToStatus == NoteStatus.Cancelled));
        Assert.Equal(0, await db.AuditLogs.CountAsync(a => a.EntityId == action.OperationalNoteId.ToString() && a.Action == "NoteCancelled"));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.EntityId == action.OperationalNoteId.ToString() && a.Action == "NoteCancellationBlockedByCorrectiveActions"));
    }

    [IntegrationConnectionFact]
    public async Task Note_closure_succeeds_when_all_actions_are_cancelled()
    {
        await _factory.SeedUserAsync("ca-closed-cancel-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-closed-cancel-admin");
        var action = await CreateOpenActionAsync(admin, ScopeType.Region, SeedIds.RegionA, null, CorrectiveActionPriority.Medium, "ملغى");
        action = await PostActionTransitionAsync(admin, action.Id, "cancel", action.RowVersion);

        await SetNoteStatusAsync(action.OperationalNoteId, NoteStatus.PendingVerification);
        var note = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/notes/{action.OperationalNoteId}");
        var response = await admin.PostAsJsonAsync($"/api/v1/notes/{action.OperationalNoteId}/verify-closure", new
        {
            reason = "اعتماد",
            closureSummary = "كل الإجراءات ملغاة",
            rowVersion = note.GetProperty("rowVersion").GetString()
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Archived_action_is_hidden_and_authorized_restore_returns_it()
    {
        await _factory.SeedUserAsync("ca-archive-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        var admin = _factory.CreateAuthenticatedClient("ca-archive-admin");
        var action = await CreateOpenActionAsync(admin, ScopeType.Region, SeedIds.RegionA, null, CorrectiveActionPriority.Medium, "أرشفة");

        var archived = await admin.PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/archive", new { reason = "أرشفة", rowVersion = action.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/corrective-actions/{action.Id}")).StatusCode);

        var rowVersion = await GetActionRowVersionIgnoringFiltersAsync(action.Id);
        var restored = await admin.PostAsJsonAsync($"/api/v1/corrective-actions/{action.Id}/restore", new { reason = "استعادة", rowVersion });
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/corrective-actions/{action.Id}")).StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Sensitive_action_is_redacted_and_sensitive_view_is_audited()
    {
        await _factory.SeedUserAsync("ca-sensitive-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("ca-sensitive-viewer", "مشاهد", [RoleCodes.RegionalDirector], (ScopeType.Region, SeedIds.RegionA, null));
        var admin = _factory.CreateAuthenticatedClient("ca-sensitive-admin");
        var action = await CreateOpenActionAsync(admin, ScopeType.Region, SeedIds.RegionA, null, CorrectiveActionPriority.Medium, "سري", ClassificationLevel.Confidential);

        var list = await _factory.CreateAuthenticatedClient("ca-sensitive-viewer").GetFromJsonAsync<PagedEnvelope<ActionListItem>>("/api/v1/corrective-actions?page=1&pageSize=100");
        var listed = Assert.Single(list!.Items, i => i.Id == action.Id);
        Assert.True(listed.IsSensitiveRedacted);
        Assert.Equal("[محجوب]", listed.Title);

        var detail = await admin.GetAsync($"/api/v1/corrective-actions/{action.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == action.Id.ToString() && a.Action == "CorrectiveActionSensitiveViewed"));
    }

    [IntegrationConnectionFact]
    public async Task Attachment_download_enforces_scan_status_and_scope()
    {
        await _factory.SeedUserAsync("ca-att-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        await _factory.SeedUserAsync("ca-att-a1", "سجن أ", [RoleCodes.FacilityDirector], (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var admin = _factory.CreateAuthenticatedClient("ca-att-admin");
        var actionA = await CreateOpenActionAsync(admin, ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, CorrectiveActionPriority.Medium, "مرفق أ");
        var actionB = await CreateOpenActionAsync(admin, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, CorrectiveActionPriority.Medium, "مرفق ب");

        var attachments = new List<AttachmentItem>();
        foreach (var status in new[] { AttachmentScanStatus.PendingScan, AttachmentScanStatus.Quarantined, AttachmentScanStatus.Rejected, AttachmentScanStatus.Clean })
        {
            var uploaded = await UploadTextAttachmentAsync(admin, actionA.Id, $"scan-{status}.txt");
            await SetAttachmentScanStatusAsync(uploaded.Id, status);
            attachments.Add(uploaded);
        }

        foreach (var blocked in attachments.Where(a => a.OriginalFileName != "scan-Clean.txt"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/attachments/{blocked.Id}/download")).StatusCode);
        }

        var clean = Assert.Single(attachments, a => a.OriginalFileName == "scan-Clean.txt");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/attachments/{clean.Id}/download")).StatusCode);

        var crossScope = await UploadTextAttachmentAsync(admin, actionB.Id, "cross.txt");
        await SetAttachmentScanStatusAsync(crossScope.Id, AttachmentScanStatus.Clean);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateAuthenticatedClient("ca-att-a1").GetAsync($"/api/v1/attachments/{crossScope.Id}/download")).StatusCode);
    }

    private static async Task<(Guid Id, string RowVersion)> CreateOpenNoteAsync(HttpClient client, ScopeType scopeType, Guid? regionId, Guid? facilityId, string title)
    {
        var note = await CreateDraftNoteAsync(client, scopeType, regionId, facilityId, title);
        var opened = await client.PostAsJsonAsync($"/api/v1/notes/{note.Id}/submit", new { reason = "تقديم", rowVersion = note.RowVersion });
        var body = await ReadObjectAsync(opened);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("rowVersion").GetString()!);
    }

    private static async Task<(Guid Id, string RowVersion)> CreateDraftNoteAsync(HttpClient client, ScopeType scopeType, Guid? regionId, Guid? facilityId, string title)
    {
        var created = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "وصف تشغيلي كاف",
            category = NoteCategory.Operational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            classification = 1,
            scopeType,
            regionId,
            facilityId,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(5)
        });
        var body = await ReadObjectAsync(created);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("rowVersion").GetString()!);
    }

    private async Task<ActionDetail> CreateOpenActionAsync(
        HttpClient client,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        CorrectiveActionPriority priority,
        string title,
        ClassificationLevel classification = ClassificationLevel.Restricted)
    {
        var note = await CreateOpenNoteAsync(client, scopeType, regionId, facilityId, $"ملاحظة {title}");
        var created = await client.PostAsJsonAsync($"/api/v1/notes/{note.Id}/corrective-actions", new
        {
            title,
            description = "وصف إجراء تصحيحي كاف",
            priority,
            classification,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = (await created.Content.ReadFromJsonAsync<ActionDetail>(JsonOptions))!;
        return await PostActionTransitionAsync(client, draft.Id, "submit", draft.RowVersion);
    }

    private static async Task<ActionDetail> AssignActionAsync(HttpClient client, Guid actionId, Guid userId, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/corrective-actions/{actionId}/assign", AssignmentPayload(userId, null, rowVersion));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ActionDetail>(body, JsonOptions)!;
    }

    private static async Task<ActionDetail> PostActionTransitionAsync(HttpClient client, Guid actionId, string transition, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/corrective-actions/{actionId}/{transition}", new { reason = "سبب انتقال", rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ActionDetail>(body, JsonOptions)!;
    }

    private static async Task<ActionDetail> PostActionCompletionAsync(HttpClient client, Guid actionId, string transition, string rowVersion, string summary)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/corrective-actions/{actionId}/{transition}", new
        {
            reason = "سبب تحقق",
            completionSummary = summary,
            rowVersion
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ActionDetail>(body, JsonOptions)!;
    }

    private async Task AssertVerifyRejectedWithoutCompletionAsync(HttpClient client, Guid actionId, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/corrective-actions/{actionId}/verify-completion", new
        {
            reason = "رفض متوقع",
            completionSummary = "لا يجب أن يحفظ",
            rowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertNoCompletionSideEffectsAsync(actionId);
    }

    private async Task AssertNoCompletionSideEffectsAsync(Guid actionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var entity = await db.CorrectiveActions.SingleAsync(a => a.Id == actionId);
        Assert.Equal(CorrectiveActionStatus.PendingVerification, entity.Status);
        Assert.Null(entity.CompletedAtUtc);
        Assert.Null(entity.CompletedByUserId);
        Assert.Equal(0, await db.CorrectiveActionStatusHistories.CountAsync(h => h.CorrectiveActionId == actionId && h.ToStatus == CorrectiveActionStatus.Completed));
        Assert.Equal(0, await db.AuditLogs.CountAsync(a => a.EntityId == actionId.ToString() && a.Action == "CorrectiveActionCompleted"));
    }

    private async Task AssertNoCurrentAssignmentAndStatusAsync(Guid actionId, CorrectiveActionStatus expectedStatus)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var action = await db.CorrectiveActions.SingleAsync(a => a.Id == actionId);
        Assert.Equal(expectedStatus, action.Status);
        Assert.Equal(0, await db.CorrectiveActionAssignments.CountAsync(a => a.CorrectiveActionId == actionId && a.IsCurrent));
    }

    private async Task<Dictionary<string, Guid>> UserIdsAsync(params string[] subjects)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        return await db.Users
            .Where(u => subjects.Contains(u.ExternalSubject))
            .ToDictionaryAsync(u => u.ExternalSubject, u => u.Id);
    }

    private async Task<Dictionary<string, Guid>> UserIdsIncludingDeletedAsync(params string[] subjects)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        return await db.Users.IgnoreQueryFilters()
            .Where(u => subjects.Contains(u.ExternalSubject))
            .ToDictionaryAsync(u => u.ExternalSubject, u => u.Id);
    }

    private async Task<Guid> CreateArchivedDepartmentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var department = new Domain.Organization.Department
        {
            OrganizationId = SeedIds.Organization,
            Code = $"ARCH-{Guid.NewGuid():N}"[..20],
            NameAr = "إدارة مؤرشفة",
            IsActive = true,
            IsDeleted = true,
            DeletedAtUtc = DateTimeOffset.UtcNow
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return department.Id;
    }

    private async Task SetNoteStatusAsync(Guid noteId, NoteStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var note = await db.OperationalNotes.SingleAsync(n => n.Id == noteId);
        note.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<string> GetActionRowVersionIgnoringFiltersAsync(Guid actionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var rowVersion = await db.CorrectiveActions.IgnoreQueryFilters()
            .Where(a => a.Id == actionId)
            .Select(a => a.RowVersion)
            .SingleAsync();
        return Convert.ToBase64String(rowVersion);
    }

    private static object AssignmentPayload(Guid? userId, Guid? departmentId, string rowVersion) => new
    {
        assignedToUserId = userId,
        assignedToDepartmentId = departmentId,
        dueAtUtc = (DateTimeOffset?)null,
        reason = "سبب تكليف",
        rowVersion
    };

    private static async Task<AttachmentItem> UploadTextAttachmentAsync(HttpClient client, Guid actionId, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("CorrectiveAction"), "entityType");
        content.Add(new StringContent(actionId.ToString()), "entityId");
        content.Add(new StringContent("دليل"), "reason");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("attachment evidence"))
        {
            Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") }
        }, "file", fileName);
        var response = await client.PostAsync("/api/v1/attachments", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<AttachmentItem>(body, JsonOptions)!;
    }

    private async Task SetAttachmentScanStatusAsync(Guid attachmentId, AttachmentScanStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var attachment = await db.Attachments.SingleAsync(a => a.Id == attachmentId);
        attachment.ScanStatus = status;
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> ReadObjectAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}

internal sealed record ActionDetail(
    Guid Id,
    string ReferenceNumber,
    Guid OperationalNoteId,
    string Title,
    string Description,
    CorrectiveActionStatus Status,
    string RowVersion,
    DateTimeOffset? CompletedAtUtc,
    Guid? CompletedByUserId,
    bool IsSensitiveRedacted);

internal sealed record ActionListItem(Guid Id, string ReferenceNumber, string Title, bool IsSensitiveRedacted);
