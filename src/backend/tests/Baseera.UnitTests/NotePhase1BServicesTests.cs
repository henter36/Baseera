using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Application.Security;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Baseera.UnitTests;

/// <summary>
/// Phase 1B: triage gate / four-eyes decision approval / multi-part parts requirement / three-tier SLA.
/// </summary>
public sealed class NotePhase1BServicesTests : IDisposable
{
    private readonly BaseeraDbContext _db = NoteTestFixtures.CreateDb();

    public void Dispose() => _db.Dispose();

    // ===== Triage gate =====

    [Fact]
    public async Task DecideValid_marks_note_eligible_without_closing_it()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var triage = BuildTriage(actor.Id, PermissionCodes.NotesUpdate, PermissionCodes.NotesView);

        var result = await triage.DecideValidAsync(note.Id, new TriageValidRequest(RowVersionOf(note)));

        Assert.Equal(NoteTriageOutcome.Valid, result.TriageOutcome);
        Assert.Equal(NoteStatus.Open, result.Status);
    }

    [Fact]
    public async Task ProposeInvalid_requires_permission()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var triage = BuildTriage(actor.Id, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            triage.ProposeInvalidAsync(note.Id, new ProposeInvalidRequest("مبرر", RowVersionOf(note))));
    }

    [Fact]
    public async Task Proposer_of_invalid_cannot_approve_own_proposal()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var proposer = NoteTestFixtures.AddUser(_db, "proposer");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var triage = BuildTriage(proposer.Id, PermissionCodes.NotesProposeInvalid, PermissionCodes.NotesView);

        var approval = await triage.ProposeInvalidAsync(note.Id, new ProposeInvalidRequest("مبرر", RowVersionOf(note)));

        var approvals = BuildApprovals(proposer.Id, PermissionCodes.NotesApproveInvalid, PermissionCodes.NotesView);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvals.ApproveAsync(note.Id, approval.Id, new ApproveNoteDecisionRequest(null, approval.RowVersion)));
    }

    [Fact]
    public async Task Independent_reviewer_can_approve_invalid_and_note_closes_with_reason()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var proposer = NoteTestFixtures.AddUser(_db, "proposer");
        var reviewer = NoteTestFixtures.AddUser(_db, "reviewer");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var triage = BuildTriage(proposer.Id, PermissionCodes.NotesProposeInvalid, PermissionCodes.NotesView);
        var approval = await triage.ProposeInvalidAsync(note.Id, new ProposeInvalidRequest("مبرر واضح", RowVersionOf(note)));

        var approvals = BuildApprovals(reviewer.Id, PermissionCodes.NotesApproveInvalid, PermissionCodes.NotesView);
        var result = await approvals.ApproveAsync(note.Id, approval.Id, new ApproveNoteDecisionRequest(null, approval.RowVersion));

        Assert.Equal(NoteStatus.Closed, result.Status);
        Assert.Equal(NoteClosureReason.Invalid, result.ClosureReason);
    }

    [Fact]
    public async Task Returning_invalid_decision_requires_reason_and_reopens_triage_gate()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var proposer = NoteTestFixtures.AddUser(_db, "proposer");
        var reviewer = NoteTestFixtures.AddUser(_db, "reviewer");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var triage = BuildTriage(proposer.Id, PermissionCodes.NotesProposeInvalid, PermissionCodes.NotesView);
        var approval = await triage.ProposeInvalidAsync(note.Id, new ProposeInvalidRequest("مبرر", RowVersionOf(note)));

        var approvals = BuildApprovals(reviewer.Id, PermissionCodes.NotesApproveInvalid, PermissionCodes.NotesView);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvals.ReturnAsync(note.Id, approval.Id, new ReturnNoteDecisionRequest("", approval.RowVersion)));

        var result = await approvals.ReturnAsync(note.Id, approval.Id, new ReturnNoteDecisionRequest("غير مقنع", approval.RowVersion));
        Assert.Null(result.TriageOutcome);
        Assert.Equal(NoteStatus.Open, result.Status);
    }

    [Fact]
    public async Task Duplicate_cannot_link_to_self()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = SeedNote(NoteStatus.Open, reporter.Id);
        var triage = BuildTriage(actor.Id, PermissionCodes.NotesProposeDuplicate, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            triage.ProposeDuplicateAsync(note.Id, new ProposeDuplicateRequest(note.Id, "مبرر", RowVersionOf(note))));
    }

    [Fact]
    public async Task Duplicate_original_out_of_scope_is_rejected()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var facilityA = Guid.NewGuid();
        var facilityB = Guid.NewGuid();
        var note = SeedNote(NoteStatus.Open, reporter.Id, scopeType: ScopeType.Facility, facilityId: facilityA);
        var original = SeedNote(NoteStatus.Open, reporter.Id, scopeType: ScopeType.Facility, facilityId: facilityB, reference: "OBS-00000002");
        var triage = BuildTriage(actor.Id, PermissionCodes.NotesProposeDuplicate, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            triage.ProposeDuplicateAsync(note.Id, new ProposeDuplicateRequest(original.Id, "مبرر", RowVersionOf(note))));
    }

    [Fact]
    public async Task Duplicate_approval_links_note_and_leaves_original_status_untouched()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var proposer = NoteTestFixtures.AddUser(_db, "proposer");
        var reviewer = NoteTestFixtures.AddUser(_db, "reviewer");
        var facility = Guid.NewGuid();
        var note = SeedNote(NoteStatus.Open, reporter.Id, scopeType: ScopeType.Facility, facilityId: facility);
        var original = SeedNote(NoteStatus.InProgress, reporter.Id, scopeType: ScopeType.Facility, facilityId: facility, reference: "OBS-00000002");

        var triage = BuildTriage(proposer.Id, PermissionCodes.NotesProposeDuplicate, PermissionCodes.NotesView);
        var approval = await triage.ProposeDuplicateAsync(note.Id, new ProposeDuplicateRequest(original.Id, "نفس الملاحظة", RowVersionOf(note)));

        var approvals = BuildApprovals(reviewer.Id, PermissionCodes.NotesApproveDuplicate, PermissionCodes.NotesView);
        var result = await approvals.ApproveAsync(note.Id, approval.Id, new ApproveNoteDecisionRequest(null, approval.RowVersion));

        Assert.Equal(NoteStatus.Closed, result.Status);
        Assert.Equal(NoteClosureReason.Duplicate, result.ClosureReason);
        Assert.Equal(original.Id, result.DuplicateOfNoteId);
        var originalStored = _db.OperationalNotes.Single(n => n.Id == original.Id);
        Assert.Equal(NoteStatus.InProgress, originalStored.Status);
    }

    [Fact]
    public async Task NoAction_proposer_cannot_approve_own_proposal()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var proposer = NoteTestFixtures.AddUser(_db, "proposer");
        var note = SeedNote(NoteStatus.InProgress, reporter.Id);
        note.TriageOutcome = NoteTriageOutcome.Valid;
        _db.SaveChanges();

        var treatment = BuildTreatment(proposer.Id, PermissionCodes.NotesProposeNoAction, PermissionCodes.NotesView);
        var approval = await treatment.ProposeNoActionAsync(note.Id, new ProposeNoActionRequest("لا يوجد أثر فعلي", RowVersionOf(note)));

        var approvals = BuildApprovals(proposer.Id, PermissionCodes.NotesApproveNoAction, PermissionCodes.NotesView);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvals.ApproveAsync(note.Id, approval.Id, new ApproveNoteDecisionRequest(null, approval.RowVersion)));
    }

    [Fact]
    public async Task Treatment_result_blocked_until_triage_valid()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = SeedNote(NoteStatus.InProgress, reporter.Id);
        var treatment = BuildTreatment(actor.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            treatment.RecordTreatmentResultAsync(note.Id, new RecordTreatmentResultRequest("تم الإصلاح", NoteTreatmentExecutionType.Direct, RowVersionOf(note))));
    }

    // ===== Parts requirement multiplicity =====

    [Fact]
    public async Task Multiple_parts_can_be_added_and_progress_is_computed()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = SeedNote(NoteStatus.InProgress, reporter.Id);
        note.TriageOutcome = NoteTriageOutcome.Valid;
        note.TreatmentExecutionType = NoteTreatmentExecutionType.RequiresParts;
        _db.SaveChanges();

        var partsService = BuildParts(actor.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var partA = await partsService.AddAsync(note.Id, new AddPartsRequirementRequest("مضخة", "P-1", 2, "قطعة", null, null, null));
        var partB = await partsService.AddAsync(note.Id, new AddPartsRequirementRequest("خرطوم", "P-2", 1, "قطعة", null, null, null));

        var progress = await partsService.GetProgressAsync(note.Id);
        Assert.Equal(2, progress.Total);
        Assert.Equal(0, progress.Installed);
        Assert.False(progress.AllResolved);

        await partsService.UpdateStatusAsync(note.Id, partA.Id, new UpdatePartsRequirementStatusRequest(NotePartsRequirementStatus.Installed, partA.RowVersion));
        await partsService.CancelAsync(note.Id, partB.Id, new CancelPartsRequirementRequest("لم تعد لازمة", partB.RowVersion));

        var finalProgress = await partsService.GetProgressAsync(note.Id);
        Assert.True(finalProgress.AllResolved);
        Assert.Equal(1, finalProgress.Installed);
        Assert.Equal(1, finalProgress.Cancelled);
    }

    [Fact]
    public async Task Submit_for_verification_blocked_while_parts_incomplete()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = SeedNote(NoteStatus.InProgress, reporter.Id);
        note.TriageOutcome = NoteTriageOutcome.Valid;
        _db.SaveChanges();

        var treatment = BuildTreatment(actor.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        await treatment.RecordTreatmentResultAsync(note.Id, new RecordTreatmentResultRequest("تحتاج قطعة", NoteTreatmentExecutionType.RequiresParts, RowVersionOf(note)));

        var stored = _db.OperationalNotes.Single(n => n.Id == note.Id);
        var partsService = BuildParts(actor.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        await partsService.AddAsync(note.Id, new AddPartsRequirementRequest("صمام", null, 1, "قطعة", null, null, null));

        var workflow = BuildWorkflow(actor.Id, PermissionCodes.NotesSubmitForVerification, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SubmitForVerificationAsync(note.Id, new TransitionNoteRequest("جاهز", RowVersionOf(stored))));
    }

    // ===== SLA =====

    [Fact]
    public async Task Sla_pause_requires_documented_parts_request_and_supplier()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var actor = NoteTestFixtures.AddUser(_db, "actor");
        var note = SeedNote(NoteStatus.InProgress, reporter.Id);
        note.TriageOutcome = NoteTriageOutcome.Valid;
        note.TreatmentExecutionType = NoteTreatmentExecutionType.RequiresParts;
        note.WorkStartedAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        _db.SaveChanges();

        var partsService = BuildParts(actor.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var part = await partsService.AddAsync(note.Id, new AddPartsRequirementRequest("مضخة", "P-9", 1, "قطعة", null, null, null));

        var slaService = BuildSla(actor.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var stored = _db.OperationalNotes.Single(n => n.Id == note.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            slaService.RequestPauseAsync(note.Id, new RequestSlaPauseRequest("بانتظار التوريد", [part.Id], null, RowVersionOf(stored))));
    }

    [Fact]
    public async Task Sla_pause_approval_rejects_self_approval_and_starts_processing_pause()
    {
        var reporter = NoteTestFixtures.AddUser(_db, "reporter");
        var requester = NoteTestFixtures.AddUser(_db, "requester");
        var approver = NoteTestFixtures.AddUser(_db, "approver");
        var note = SeedNote(NoteStatus.InProgress, reporter.Id);
        note.TriageOutcome = NoteTriageOutcome.Valid;
        note.TreatmentExecutionType = NoteTreatmentExecutionType.RequiresParts;
        note.WorkStartedAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        _db.SaveChanges();

        var partsService = BuildParts(requester.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var part = await partsService.AddAsync(note.Id, new AddPartsRequirementRequest(
            "مضخة", "P-9", 1, "قطعة", "REQ-100", "المورد المعتمد", null));

        var slaRequester = BuildSla(requester.Id, PermissionCodes.NotesStartWork, PermissionCodes.NotesView);
        var stored = _db.OperationalNotes.Single(n => n.Id == note.Id);
        var state = await slaRequester.RequestPauseAsync(note.Id, new RequestSlaPauseRequest("بانتظار التوريد", [part.Id], null, RowVersionOf(stored)));
        Assert.False(state.IsProcessingSlaPaused);

        var pause = _db.NoteSlaPausePeriods.Single(p => p.OperationalNoteId == note.Id);
        var selfApproveSla = BuildSla(requester.Id, PermissionCodes.NotesApproveSlaPause, PermissionCodes.NotesView);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selfApproveSla.ApprovePauseAsync(note.Id, pause.Id, Convert.ToBase64String(pause.RowVersion)));

        var approverSla = BuildSla(approver.Id, PermissionCodes.NotesApproveSlaPause, PermissionCodes.NotesView);
        var approved = await approverSla.ApprovePauseAsync(note.Id, pause.Id, Convert.ToBase64String(pause.RowVersion));
        Assert.True(approved.IsProcessingSlaPaused);
    }

    [Fact]
    public void Sla_overall_age_keeps_running_while_processing_sla_is_paused()
    {
        var now = DateTimeOffset.UtcNow;
        var note = new OperationalNote
        {
            CreatedAtUtc = now.AddDays(-10),
            SubmittedAtUtc = now.AddDays(-10),
            WorkStartedAtUtc = now.AddDays(-8),
            Status = NoteStatus.InProgress
        };
        var pause = new NoteSlaPausePeriod
        {
            StartedAtUtc = now.AddDays(-4),
            EndedAtUtc = null,
            ApprovedByUserId = Guid.NewGuid(),
            Reason = "بانتظار التوريد",
            RequestedByUserId = Guid.NewGuid(),
            RequestedAtUtc = now.AddDays(-4)
        };

        var state = NoteSlaService.Compute(note, [pause], now);

        Assert.True(state.OverallAgeSeconds >= TimeSpan.FromDays(10).TotalSeconds - 5);
        Assert.True(state.IsProcessingSlaPaused);
        Assert.True(state.ExternalWaitDurationSeconds >= TimeSpan.FromDays(4).TotalSeconds - 5);
        // Processing SLA excludes the paused window: ~8 days elapsed minus ~4 days paused.
        Assert.InRange(state.ProcessingSlaSeconds, TimeSpan.FromDays(3.9).TotalSeconds, TimeSpan.FromDays(4.1).TotalSeconds);
    }

    // ===== Allowed actions (pure) =====

    [Fact]
    public void ComputeAllowedActions_exposes_triage_gate_tokens_only_before_triage()
    {
        var note = NoteDetailFixture(NoteStatus.Open, triageOutcome: null);
        var currentUser = FakeUser(Guid.NewGuid(), PermissionCodes.NotesUpdate, PermissionCodes.NotesProposeInvalid, PermissionCodes.NotesProposeDuplicate);

        var actions = NoteWorkspaceQueryService.ComputeAllowedActions(note, currentUser);

        Assert.Contains("TRIAGE_VALID", actions);
        Assert.Contains("TRIAGE_PROPOSE_INVALID", actions);
        Assert.Contains("TRIAGE_PROPOSE_DUPLICATE", actions);
        Assert.DoesNotContain("RECORD_TREATMENT", actions);
    }

    [Fact]
    public void ComputeAllowedActions_exposes_treatment_tokens_only_after_valid_triage()
    {
        var note = NoteDetailFixture(NoteStatus.InProgress, triageOutcome: NoteTriageOutcome.Valid);
        var currentUser = FakeUser(Guid.NewGuid(), PermissionCodes.NotesStartWork, PermissionCodes.NotesProposeNoAction);

        var actions = NoteWorkspaceQueryService.ComputeAllowedActions(note, currentUser);

        Assert.Contains("RECORD_TREATMENT", actions);
        Assert.Contains("PROPOSE_NO_ACTION", actions);
        Assert.DoesNotContain("TRIAGE_VALID", actions);
    }

    // ===== Builders =====

    private INoteTriageService BuildTriage(Guid userId, params string[] permissions)
    {
        var (scope, typeAccess, audit, queries, current) = BuildCommon(userId, permissions);
        return new NoteTriageService(_db, current, scope, NoteTestFixtures.FakeAttachments, audit, queries);
    }

    private INoteDecisionApprovalService BuildApprovals(Guid userId, params string[] permissions)
    {
        var (scope, typeAccess, audit, queries, current) = BuildCommon(userId, permissions);
        return new NoteDecisionApprovalService(_db, current, scope, audit, queries);
    }

    private INoteTreatmentService BuildTreatment(Guid userId, params string[] permissions)
    {
        var (scope, typeAccess, audit, queries, current) = BuildCommon(userId, permissions);
        return new NoteTreatmentService(_db, current, scope, typeAccess, NoteTestFixtures.FakeAttachments, audit, queries);
    }

    private INotePartsRequirementService BuildParts(Guid userId, params string[] permissions)
    {
        var (scope, typeAccess, audit, queries, current) = BuildCommon(userId, permissions);
        return new NotePartsRequirementService(_db, current, scope, typeAccess, audit, BuildSla(userId, permissions));
    }

    private INoteSlaService BuildSla(Guid userId, params string[] permissions)
    {
        var (scope, _, audit, _, current) = BuildCommon(userId, permissions);
        return new NoteSlaService(_db, current, scope, audit);
    }

    private INoteWorkflowService BuildWorkflow(Guid userId, params string[] permissions)
    {
        var (scope, typeAccess, audit, queries, current) = BuildCommon(userId, permissions);
        return new NoteWorkflowService(_db, current, scope, typeAccess, audit, queries, NoteTestFixtures.FakeAttachments);
    }

    private (NoteScopeService Scope, NoteTypeAccessService TypeAccess, AuditService Audit, NoteQueryService Queries, ICurrentUser Current) BuildCommon(
        Guid userId, string[] permissions)
    {
        NoteTestFixtures.GrantPermissions(_db, userId, $"Actor-{userId}", permissions);
        var current = FakeUser(userId, permissions);
        var scope = new NoteScopeService(new OrganizationalScopeService(current, _db), current, _db);
        var typeAccess = new NoteTypeAccessService(_db, current);
        var audit = new AuditService(_db, current, new OrganizationalScopeService(current, _db));
        var queries = new NoteQueryService(_db, current, scope, typeAccess, audit);
        return (scope, typeAccess, audit, queries, current);
    }

    private OperationalNote SeedNote(
        NoteStatus status,
        Guid reporterId,
        ScopeType scopeType = ScopeType.Global,
        Guid? facilityId = null,
        string reference = "OBS-00000001")
    {
        var note = NoteTestFixtures.NewNote(scopeType, reporterId, facilityId: facilityId, status: status, reference: reference);
        _db.OperationalNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private static string RowVersionOf(OperationalNote note) => Convert.ToBase64String(note.RowVersion);

    private static ICurrentUser FakeUser(Guid userId, params string[] permissions) =>
        new FakeCurrentUser(true, userId, userId.ToString(), "actor", permissions, [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);

    private static NoteDetailDto NoteDetailFixture(NoteStatus status, NoteTriageOutcome? triageOutcome) => new(
        Guid.NewGuid(), "OBS-00000001", "عنوان", "وصف", status, NoteDisplay.StatusAr(status),
        NoteSeverity.Medium, NoteDisplay.SeverityAr(NoteSeverity.Medium), Guid.NewGuid(), "OPERATIONAL", "تشغيلية", null, null, true,
        NoteSourceType.Manual, NoteDisplay.SourceAr(NoteSourceType.Manual), null, ClassificationLevel.Internal,
        ScopeType.Global, null, null, null, null, Guid.NewGuid(), null,
        DateTimeOffset.UtcNow, null, false, null, null, null, null, null, null,
        null, null, null, DateTimeOffset.UtcNow, Convert.ToBase64String([1, 2, 3, 4]), false,
        triageOutcome, null, null, null, null, null, null, null, null, null, null, null, null, null, true);
}
