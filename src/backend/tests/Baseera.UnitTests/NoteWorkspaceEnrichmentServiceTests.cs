using Baseera.Application.Notes;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;

namespace Baseera.UnitTests;

public sealed class NoteWorkspaceEnrichmentServiceTests
{
    [Fact]
    public async Task BuildAsync_calls_each_collaborator_once_and_forwards_the_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        var decisionApprovals = new FakeDecisionApprovalService();
        var parts = new FakePartsRequirementService();
        var sla = new FakeSlaService();
        var service = new NoteWorkspaceEnrichmentService(decisionApprovals, parts, sla);
        var note = NoteDetailFixture(NoteTreatmentExecutionType.RequiresParts);

        var result = await service.BuildAsync(note, cts.Token);

        Assert.Equal(1, decisionApprovals.ListCallCount);
        Assert.Equal(1, parts.ListCallCount);
        Assert.Equal(1, parts.ProgressCallCount);
        Assert.Equal(1, sla.ComputeCallCount);
        Assert.Equal(cts.Token, decisionApprovals.LastToken);
        Assert.Equal(cts.Token, parts.LastListToken);
        Assert.Equal(cts.Token, sla.LastToken);
        Assert.Same(decisionApprovals.Decisions, result.Decisions);
        Assert.Same(sla.SlaState, result.SlaState);
    }

    [Fact]
    public async Task BuildAsync_skips_parts_calls_when_execution_type_does_not_require_parts()
    {
        var decisionApprovals = new FakeDecisionApprovalService();
        var parts = new FakePartsRequirementService();
        var sla = new FakeSlaService();
        var service = new NoteWorkspaceEnrichmentService(decisionApprovals, parts, sla);
        var note = NoteDetailFixture(NoteTreatmentExecutionType.Direct);

        var result = await service.BuildAsync(note);

        Assert.Equal(0, parts.ListCallCount);
        Assert.Equal(0, parts.ProgressCallCount);
        Assert.Empty(result.PartsItems);
        Assert.Null(result.PartsProgress);
    }

    [Fact]
    public async Task BuildAsync_selects_the_pending_decision_from_the_full_decision_list()
    {
        var approved = new NoteDecisionApprovalDto(
            Guid.NewGuid(), Guid.NewGuid(), NoteDecisionApprovalType.Invalid, "اعتماد غير صحيحة",
            NoteDecisionApprovalStatus.Approved, "معتمد", null, null, null,
            Guid.NewGuid(), null, DateTimeOffset.UtcNow, Guid.NewGuid(), null, DateTimeOffset.UtcNow, null, "AQ==");
        var pending = approved with { Id = Guid.NewGuid(), Status = NoteDecisionApprovalStatus.Pending, StatusAr = "بانتظار الاعتماد" };
        var decisionApprovals = new FakeDecisionApprovalService { Decisions = [approved, pending] };
        var parts = new FakePartsRequirementService();
        var sla = new FakeSlaService();
        var service = new NoteWorkspaceEnrichmentService(decisionApprovals, parts, sla);
        var note = NoteDetailFixture(NoteTreatmentExecutionType.Direct);

        var result = await service.BuildAsync(note);

        Assert.Equal(pending.Id, result.PendingDecision?.Id);
    }

    private static NoteDetailDto NoteDetailFixture(NoteTreatmentExecutionType executionType) => new(
        Guid.NewGuid(), "OBS-00000001", "عنوان", "وصف", NoteStatus.InProgress, "قيد المعالجة",
        NoteSeverity.Medium, "متوسطة", Guid.NewGuid(), "OPERATIONAL", "تشغيلية", null, null, true,
        NoteSourceType.Manual, "يدوي", null, Domain.Attachments.ClassificationLevel.Internal,
        ScopeType.Global, null, null, null, null, Guid.NewGuid(), null,
        DateTimeOffset.UtcNow, null, false, null, null, null, null, null, null,
        null, null, null, DateTimeOffset.UtcNow, "AQIDBA==", false,
        NoteTriageOutcome.Valid, "صحيحة", null, null, null, null,
        null, null, executionType, null, null, null, null, null, true);

    private sealed class FakeDecisionApprovalService : INoteDecisionApprovalService
    {
        public IReadOnlyList<NoteDecisionApprovalDto> Decisions { get; set; } = [];
        public int ListCallCount { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<IReadOnlyList<NoteDecisionApprovalDto>> ListAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            ListCallCount++;
            LastToken = cancellationToken;
            return Task.FromResult(Decisions);
        }

        public Task<NoteDetailDto> ApproveAsync(Guid noteId, Guid approvalId, ApproveNoteDecisionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");

        public Task<NoteDetailDto> ReturnAsync(Guid noteId, Guid approvalId, ReturnNoteDecisionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");
    }

    private sealed class FakePartsRequirementService : INotePartsRequirementService
    {
        public int ListCallCount { get; private set; }
        public int ProgressCallCount { get; private set; }
        public CancellationToken LastListToken { get; private set; }

        public Task<IReadOnlyList<NotePartsRequirementDto>> ListAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            ListCallCount++;
            LastListToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<NotePartsRequirementDto>>([]);
        }

        public Task<NotePartsProgressDto> GetProgressAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            ProgressCallCount++;
            return Task.FromResult(new NotePartsProgressDto(0, 0, 0, 0, false));
        }

        public Task<NotePartsRequirementDto> AddAsync(Guid noteId, AddPartsRequirementRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");

        public Task<NotePartsRequirementDto> UpdateAsync(Guid noteId, Guid itemId, UpdatePartsRequirementRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");

        public Task DeleteAsync(Guid noteId, Guid itemId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");

        public Task<NotePartsRequirementDto> UpdateStatusAsync(Guid noteId, Guid itemId, UpdatePartsRequirementStatusRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");

        public Task<NotePartsRequirementDto> CancelAsync(Guid noteId, Guid itemId, CancelPartsRequirementRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");
    }

    private sealed class FakeSlaService : INoteSlaService
    {
        public NoteSlaStateDto SlaState { get; } = new(0, 0, 0, false, null, null, null, null);
        public int ComputeCallCount { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<NoteSlaStateDto> ComputeAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            ComputeCallCount++;
            LastToken = cancellationToken;
            return Task.FromResult(SlaState);
        }

        public Task<NoteSlaStateDto> RequestPauseAsync(Guid noteId, RequestSlaPauseRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");

        public Task<NoteSlaStateDto> ApprovePauseAsync(Guid noteId, Guid pauseId, string rowVersion, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");

        public Task EndPauseIfPartsResolvedAsync(Guid noteId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by this test.");
    }
}
