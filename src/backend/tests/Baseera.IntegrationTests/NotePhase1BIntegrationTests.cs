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
/// Phase 1B: triage gate / treatment result / four-eyes decision approval / multi-part parts
/// requirement / three-tier SLA — full HTTP round-trip against a real SQL Server, matching the
/// golden paths in docs/ux-rescue/phase1b-observation-state-mapping.md.
/// </summary>
[Collection(OperationsIntegrationCollection.Name)]
public sealed class NotePhase1BIntegrationTests(OperationsIntegrationFixture fixture) : IntegrationTestBase<OperationsIntegrationFixture>(fixture)
{
    private readonly BaseeraApiFactory _factory = fixture.Factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [IntegrationConnectionFact]
    public async Task Valid_triage_then_direct_treatment_then_independent_verify_closes_as_treated()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-1");
        var note = await CreateNoteAsync(admin, "صحيحة ثم معالجة مباشرة");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        note = await PostAsync(admin, $"/api/v1/notes/{note.Id}/triage/valid", new { rowVersion = note.RowVersion });
        Assert.Equal(NoteTriageOutcome.Valid, note.TriageOutcome);

        note = await AssignAsync(approver, note.Id, await UserIdAsync("p1b-1-proposer"), note.RowVersion);
        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/start-work", new { reason = (string?)null, rowVersion = note.RowVersion });
        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/treatment/result", new
        {
            treatmentResultText = "تم إصلاح العطل مباشرة.",
            executionType = NoteTreatmentExecutionType.Direct,
            rowVersion = note.RowVersion
        });
        Assert.Equal(NoteTreatmentResultType.Treated, note.TreatmentResultType);

        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/submit-for-verification", new { reason = (string?)null, rowVersion = note.RowVersion });
        Assert.Equal(NoteStatus.PendingVerification, note.Status);

        var closed = await PostAsync(approver, $"/api/v1/notes/{note.Id}/verify-closure", new
        {
            reason = "اعتماد الإغلاق",
            closureSummary = "تم التحقق من المعالجة المباشرة",
            rowVersion = note.RowVersion
        });
        Assert.Equal(NoteStatus.Closed, closed.Status);
        Assert.Equal(NoteClosureReason.Treated, closed.ClosureReason);
    }

    [IntegrationConnectionFact]
    public async Task Invalid_decision_requires_independent_approver_and_closes_note()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-2");
        var note = await CreateNoteAsync(admin, "غير صحيحة");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);

        var proposeResponse = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/triage/propose-invalid", new
        {
            justificationAr = "لا يوجد ما يثبت الواقعة.",
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);
        var approvalId = await LatestPendingApprovalIdAsync(note.Id, NoteDecisionApprovalType.Invalid);
        var approvalRowVersion = await ApprovalRowVersionAsync(note.Id, approvalId);

        // Proposer cannot approve their own proposal (RowVersion check passes, self-check fails — no mutation).
        var proposerSelfApprove = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = approvalRowVersion });
        Assert.Equal(HttpStatusCode.Conflict, proposerSelfApprove.StatusCode);

        var approveResponse = await approver.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = approvalRowVersion });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var closed = await approveResponse.Content.ReadFromJsonAsync<NoteDetail>(JsonOptions);
        Assert.Equal(NoteStatus.Closed, closed!.Status);
        Assert.Equal(NoteClosureReason.Invalid, closed.ClosureReason);
    }

    [IntegrationConnectionFact]
    public async Task Returning_invalid_decision_requires_reason_and_note_stays_open_and_visible()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-3");
        var note = await CreateNoteAsync(admin, "غير صحيحة معادة");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        var proposeResponse = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/triage/propose-invalid", new { justificationAr = "مبرر أولي", rowVersion = note.RowVersion });
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);
        var approvalId = await LatestPendingApprovalIdAsync(note.Id, NoteDecisionApprovalType.Invalid);

        var noReason = await approver.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/return", new { reviewReason = "", rowVersion = await ApprovalRowVersionAsync(note.Id, approvalId) });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var approvalRowVersion = await ApprovalRowVersionAsync(note.Id, approvalId);
        var returned = await approver.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/return", new { reviewReason = "الدليل غير كافٍ", rowVersion = approvalRowVersion });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);

        var afterReturn = await GetNoteAsync(admin, note.Id);
        Assert.Null(afterReturn.TriageOutcome);
        Assert.Equal(NoteStatus.Open, afterReturn.Status);

        // Still fully visible/searchable — not hidden from follow-up before final approval.
        var stillVisible = await admin.GetAsync($"/api/v1/notes/{note.Id}");
        Assert.Equal(HttpStatusCode.OK, stillVisible.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Duplicate_decision_links_original_without_changing_its_status()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-4");
        var original = await CreateNoteAsync(admin, "الملاحظة الأصلية");
        original = await SubmitAsync(admin, original.Id, original.RowVersion);
        var originalWorkerId = await UserIdAsync("p1b-4-proposer");
        original = await AssignAsync(approver, original.Id, originalWorkerId, original.RowVersion);
        original = await PostAsync(admin, $"/api/v1/notes/{original.Id}/triage/valid", new { rowVersion = original.RowVersion });
        original = await PostAsync(proposer, $"/api/v1/notes/{original.Id}/start-work", new { reason = (string?)null, rowVersion = original.RowVersion });

        var duplicate = await CreateNoteAsync(admin, "ملاحظة مكررة لنفس العطل");
        duplicate = await SubmitAsync(admin, duplicate.Id, duplicate.RowVersion);

        var proposeResponse = await proposer.PostAsJsonAsync($"/api/v1/notes/{duplicate.Id}/triage/propose-duplicate", new
        {
            originalNoteId = original.Id,
            justificationAr = "نفس العطل والموقع بالضبط.",
            rowVersion = duplicate.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);
        var approvalId = await LatestPendingApprovalIdAsync(duplicate.Id, NoteDecisionApprovalType.Duplicate);
        var approvalRowVersion = await ApprovalRowVersionAsync(duplicate.Id, approvalId);

        var approveResponse = await approver.PostAsJsonAsync($"/api/v1/notes/{duplicate.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = approvalRowVersion });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var closedDuplicate = await approveResponse.Content.ReadFromJsonAsync<NoteDetail>(JsonOptions);
        Assert.Equal(NoteStatus.Closed, closedDuplicate!.Status);
        Assert.Equal(NoteClosureReason.Duplicate, closedDuplicate.ClosureReason);
        Assert.Equal(original.Id, closedDuplicate.DuplicateOfNoteId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var originalEntity = await db.OperationalNotes.SingleAsync(n => n.Id == original.Id);
        Assert.Equal(NoteStatus.InProgress, originalEntity.Status);
    }

    [IntegrationConnectionFact]
    public async Task NoAction_decision_requires_independent_approval_and_closes_note()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-5");
        var note = await CreateNoteAsync(admin, "لا تتطلب إجراء");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        note = await PostAsync(admin, $"/api/v1/notes/{note.Id}/triage/valid", new { rowVersion = note.RowVersion });
        note = await AssignAsync(approver, note.Id, await UserIdAsync("p1b-5-proposer"), note.RowVersion);
        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/start-work", new { reason = (string?)null, rowVersion = note.RowVersion });

        var proposeResponse = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/treatment/propose-no-action", new
        {
            justificationAr = "تم فحصه ولا يوجد أثر يستدعي إجراء.",
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);
        var approvalId = await LatestPendingApprovalIdAsync(note.Id, NoteDecisionApprovalType.NoAction);

        var proposerSelfApprove = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = await ApprovalRowVersionAsync(note.Id, approvalId) });
        Assert.Equal(HttpStatusCode.Conflict, proposerSelfApprove.StatusCode);

        var approveResponse = await approver.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = await ApprovalRowVersionAsync(note.Id, approvalId) });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var closed = await approveResponse.Content.ReadFromJsonAsync<NoteDetail>(JsonOptions);
        Assert.Equal(NoteStatus.Closed, closed!.Status);
        Assert.Equal(NoteClosureReason.NoActionRequired, closed.ClosureReason);
    }

    [IntegrationConnectionFact]
    public async Task Multiple_parts_block_verification_until_all_installed_then_allow_it()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-6");
        var note = await CreateNoteAsync(admin, "تحتاج قطعتين");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        note = await PostAsync(admin, $"/api/v1/notes/{note.Id}/triage/valid", new { rowVersion = note.RowVersion });
        note = await AssignAsync(approver, note.Id, await UserIdAsync("p1b-6-proposer"), note.RowVersion);
        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/start-work", new { reason = (string?)null, rowVersion = note.RowVersion });
        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/treatment/result", new
        {
            treatmentResultText = "تحتاج قطعتين للاستبدال.",
            executionType = NoteTreatmentExecutionType.RequiresParts,
            rowVersion = note.RowVersion
        });

        var partA = await AddPartAsync(proposer, note.Id, "مضخة مياه", "PMP-1");
        var partB = await AddPartAsync(proposer, note.Id, "خرطوم توصيل", "HOS-1");

        var blocked = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/submit-for-verification", new { reason = (string?)null, rowVersion = note.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        await SetPartStatusAsync(proposer, note.Id, partA.Id, NotePartsRequirementStatus.Installed, partA.RowVersion);
        var stillBlocked = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/submit-for-verification", new { reason = (string?)null, rowVersion = note.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, stillBlocked.StatusCode);

        var cancelResponse = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/parts/{partB.Id}/cancel", new { reason = "توفرت بديل مدمج", rowVersion = partB.RowVersion });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var latest = await GetNoteAsync(admin, note.Id);
        var allowed = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/submit-for-verification", new { reason = (string?)null, rowVersion = latest.RowVersion });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Sla_pause_request_and_approval_pauses_processing_clock_but_not_overall_age()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-7");
        var note = await CreateNoteAsync(admin, "بانتظار قطع مع SLA");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        note = await PostAsync(admin, $"/api/v1/notes/{note.Id}/triage/valid", new { rowVersion = note.RowVersion });
        note = await AssignAsync(approver, note.Id, await UserIdAsync("p1b-7-proposer"), note.RowVersion);
        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/start-work", new { reason = (string?)null, rowVersion = note.RowVersion });
        note = await PostAsync(proposer, $"/api/v1/notes/{note.Id}/treatment/result", new
        {
            treatmentResultText = "بانتظار توريد قطعة.",
            executionType = NoteTreatmentExecutionType.RequiresParts,
            rowVersion = note.RowVersion
        });

        var addResponse = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/parts", new
        {
            itemName = "مضخة مستوردة",
            itemCode = "PMP-IMP-1",
            quantity = 1,
            unit = "قطعة",
            requestNumber = "REQ-2026-001",
            supplierOrSource = "المورد المعتمد المركزي",
            notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        var part = await addResponse.Content.ReadFromJsonAsync<PartItem>(JsonOptions);

        var pauseRequest = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/sla/request-pause", new
        {
            reason = "بانتظار توريد من المورد المركزي",
            relatedPartsRequirementIds = new[] { part!.Id },
            reviewDueAtUtc = (DateTimeOffset?)null,
            rowVersion = note.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, pauseRequest.StatusCode);
        var slaAfterRequest = await pauseRequest.Content.ReadFromJsonAsync<SlaStateResponse>(JsonOptions);
        Assert.False(slaAfterRequest!.IsProcessingSlaPaused);

        Guid pauseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            pauseId = await db.NoteSlaPausePeriods.Where(p => p.OperationalNoteId == note.Id).Select(p => p.Id).SingleAsync();
        }

        var pauseRowVersion = await PauseRowVersionAsync(note.Id, pauseId);
        var selfApprovePause = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/sla/pauses/{pauseId}/approve", new { reason = (string?)null, rowVersion = pauseRowVersion });
        Assert.Equal(HttpStatusCode.Conflict, selfApprovePause.StatusCode);

        var approvePause = await approver.PostAsJsonAsync($"/api/v1/notes/{note.Id}/sla/pauses/{pauseId}/approve", new { reason = (string?)null, rowVersion = pauseRowVersion });
        Assert.Equal(HttpStatusCode.OK, approvePause.StatusCode);
        var slaAfterApproval = await approvePause.Content.ReadFromJsonAsync<SlaStateResponse>(JsonOptions);
        Assert.True(slaAfterApproval!.IsProcessingSlaPaused);
        Assert.True(slaAfterApproval.OverallAgeSeconds >= 0);
    }

    [IntegrationConnectionFact]
    public async Task Approve_decision_out_of_scope_returns_404()
    {
        var (admin, proposer, _) = await SeedTriadAsync("p1b-8");
        var note = await CreateNoteAsync(admin, "خارج النطاق للاعتماد");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        await PostAsync(proposer, $"/api/v1/notes/{note.Id}/triage/propose-invalid", new { justificationAr = "مبرر", rowVersion = note.RowVersion });
        var approvalId = await LatestPendingApprovalIdAsync(note.Id, NoteDecisionApprovalType.Invalid);

        await _factory.SeedUserAsync("p1b-8-outsider", "خارج النطاق", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1));
        var outsider = _factory.CreateAuthenticatedClient("p1b-8-outsider");
        var response = await outsider.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = "AA==" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Approve_decision_with_stale_row_version_returns_409()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-9");
        var note = await CreateNoteAsync(admin, "تعارض RowVersion");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        await PostAsync(proposer, $"/api/v1/notes/{note.Id}/triage/propose-invalid", new { justificationAr = "مبرر", rowVersion = note.RowVersion });
        var approvalId = await LatestPendingApprovalIdAsync(note.Id, NoteDecisionApprovalType.Invalid);

        var response = await approver.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = "AAAAAAAAAAA=" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Missing_approve_permission_returns_403()
    {
        var (admin, proposer, _) = await SeedTriadAsync("p1b-10");
        var note = await CreateNoteAsync(admin, "بلا صلاحية اعتماد");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        var proposeResponse = await proposer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/triage/propose-invalid", new { justificationAr = "مبرر", rowVersion = note.RowVersion });
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);
        var approvalId = await LatestPendingApprovalIdAsync(note.Id, NoteDecisionApprovalType.Invalid);

        // RegionalCoordinator has Notes.Propose* by base role design but never Notes.Approve* — and,
        // unlike FacilityCoordinator, this suite never widens it via SeedUserWithPermissionsAsync (which
        // grants at the shared ROLE level, not per-user), so it stays a reliable "no approve permission" probe.
        await _factory.SeedUserAsync(
            "p1b-10-viewer", "مشاهد بلا اعتماد", [RoleCodes.RegionalCoordinator],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        var viewer = _factory.CreateAuthenticatedClient("p1b-10-viewer");

        var approvalRowVersion = await ApprovalRowVersionAsync(note.Id, approvalId);
        var response = await viewer.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = approvalRowVersion });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IntegrationConnectionFact]
    public async Task Decision_approval_and_timeline_are_fully_audited()
    {
        var (admin, proposer, approver) = await SeedTriadAsync("p1b-11");
        var note = await CreateNoteAsync(admin, "تدقيق كامل");
        note = await SubmitAsync(admin, note.Id, note.RowVersion);
        await PostAsync(proposer, $"/api/v1/notes/{note.Id}/triage/propose-invalid", new { justificationAr = "مبرر", rowVersion = note.RowVersion });
        var approvalId = await LatestPendingApprovalIdAsync(note.Id, NoteDecisionApprovalType.Invalid);
        var approvalRowVersion = await ApprovalRowVersionAsync(note.Id, approvalId);
        await approver.PostAsJsonAsync($"/api/v1/notes/{note.Id}/decisions/{approvalId}/approve", new { reviewReason = (string?)null, rowVersion = approvalRowVersion });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        Assert.True(await db.AuditLogs.CountAsync(a => a.EntityId == note.Id.ToString() && a.Action == "NoteInvalidProposed") == 1);
        Assert.True(await db.AuditLogs.CountAsync(a => a.EntityId == note.Id.ToString() && a.Action == "NoteInvalidApproved") == 1);
        Assert.True(await db.NoteStatusHistories.CountAsync(h => h.OperationalNoteId == note.Id && h.ToStatus == NoteStatus.Closed) == 1);
    }

    // ===== Helpers =====

    private async Task<(HttpClient Admin, HttpClient Proposer, HttpClient Approver)> SeedTriadAsync(string prefix)
    {
        await _factory.SeedUserAsync($"{prefix}-admin", "مسؤول", [RoleCodes.SystemAdministrator], (ScopeType.Global, null, null));
        // Proposer also holds the Approve* permissions here on purpose: four-eyes in this system is
        // enforced per-instance (proposer != reviewer on THIS decision), not by role separation — see
        // docs/permissions-matrix.md §Critical SoD. This lets self-approve attempts below exercise the
        // real service-level rejection (409) instead of merely tripping the endpoint's permission gate (403).
        await _factory.SeedUserWithPermissionsAsync(
            $"{prefix}-proposer",
            "معالج مقترح",
            [RoleCodes.FacilityCoordinator],
            [
                PermissionCodes.NotesProposeInvalid, PermissionCodes.NotesProposeDuplicate, PermissionCodes.NotesProposeNoAction,
                PermissionCodes.NotesApproveInvalid, PermissionCodes.NotesApproveDuplicate, PermissionCodes.NotesApproveNoAction,
                PermissionCodes.NotesApproveSlaPause
            ],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));
        await _factory.SeedUserAsync($"{prefix}-approver", "معتمد مستقل", [RoleCodes.FacilityDirector],
            (ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1));

        return (
            _factory.CreateAuthenticatedClient($"{prefix}-admin"),
            _factory.CreateAuthenticatedClient($"{prefix}-proposer"),
            _factory.CreateAuthenticatedClient($"{prefix}-approver"));
    }

    private async Task<Guid> UserIdAsync(string subject)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        return await db.Users.Where(u => u.ExternalSubject == subject).Select(u => u.Id).FirstAsync();
    }

    private async Task<Guid> LatestPendingApprovalIdAsync(Guid noteId, NoteDecisionApprovalType type)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        return await db.NoteDecisionApprovals
            .Where(a => a.OperationalNoteId == noteId && a.DecisionType == type && a.Status == NoteDecisionApprovalStatus.Pending)
            .OrderByDescending(a => a.ProposedAtUtc)
            .Select(a => a.Id)
            .FirstAsync();
    }

    private async Task<string> ApprovalRowVersionAsync(Guid noteId, Guid approvalId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var approval = await db.NoteDecisionApprovals.SingleAsync(a => a.Id == approvalId && a.OperationalNoteId == noteId);
        return Convert.ToBase64String(approval.RowVersion);
    }

    private async Task<string> PauseRowVersionAsync(Guid noteId, Guid pauseId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var pause = await db.NoteSlaPausePeriods.SingleAsync(p => p.Id == pauseId && p.OperationalNoteId == noteId);
        return Convert.ToBase64String(pause.RowVersion);
    }

    private static async Task<NoteDetail> CreateNoteAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/notes", new
        {
            title,
            description = "وصف تفصيلي كافٍ لاختبار Phase 1B.",
            noteTypeId = SeedIds.NoteTypeOperational,
            severity = NoteSeverity.Medium,
            sourceType = NoteSourceType.Manual,
            sourceReference = (string?)null,
            classification = ClassificationLevel.Internal,
            scopeType = ScopeType.Facility,
            regionId = SeedIds.RegionA,
            facilityId = SeedIds.FacilityA1,
            facilityUnitId = (Guid?)null,
            ownerDepartmentId = (Guid?)null,
            dueAtUtc = DateTimeOffset.UtcNow.AddDays(5)
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }

    private static async Task<NoteDetail> SubmitAsync(HttpClient client, Guid noteId, string rowVersion) =>
        await PostAsync(client, $"/api/v1/notes/{noteId}/submit", new { reason = "تقديم", rowVersion });

    private static async Task<NoteDetail> AssignAsync(HttpClient client, Guid noteId, Guid userId, string rowVersion) =>
        await PostAsync(client, $"/api/v1/notes/{noteId}/assign", new
        {
            assignedToUserId = userId,
            assignedToDepartmentId = (Guid?)null,
            dueAtUtc = (DateTimeOffset?)null,
            reason = "تكليف",
            rowVersion
        });

    private static async Task<NoteDetail> GetNoteAsync(HttpClient client, Guid noteId)
    {
        var response = await client.GetAsync($"/api/v1/notes/{noteId}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }

    private static async Task<NoteDetail> PostAsync(HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<NoteDetail>(body, JsonOptions)!;
    }

    private static async Task<PartItem> AddPartAsync(HttpClient client, Guid noteId, string itemName, string itemCode)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/notes/{noteId}/parts", new
        {
            itemName,
            itemCode,
            quantity = 1,
            unit = "قطعة",
            requestNumber = (string?)null,
            supplierOrSource = (string?)null,
            notes = (string?)null
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<PartItem>(body, JsonOptions)!;
    }

    private static async Task SetPartStatusAsync(HttpClient client, Guid noteId, Guid itemId, NotePartsRequirementStatus status, string rowVersion)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/notes/{noteId}/parts/{itemId}/status", new { status, rowVersion });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private sealed record NoteDetail(
        Guid Id,
        NoteStatus Status,
        NoteTriageOutcome? TriageOutcome,
        NoteTreatmentResultType? TreatmentResultType,
        NoteClosureReason? ClosureReason,
        Guid? DuplicateOfNoteId,
        string RowVersion,
        string? DecisionRowVersion);

    private sealed record PartItem(Guid Id, string RowVersion);

    private sealed record SlaStateResponse(
        double OverallAgeSeconds,
        double ProcessingSlaSeconds,
        double ExternalWaitDurationSeconds,
        bool IsProcessingSlaPaused);
}
