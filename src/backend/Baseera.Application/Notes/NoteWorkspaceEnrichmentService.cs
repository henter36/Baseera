namespace Baseera.Application.Notes;

using Baseera.Domain.Notes;

/// <summary>
/// Groups the three collaborators that together compute the Phase 1B "enrichment" data for a note's
/// workspace read model (decision approvals, parts tracking, SLA state) behind one seam — keeps
/// <see cref="NoteWorkspaceQueryService"/>'s constructor small without resorting to a behaviorless
/// parameter bag or a service locator.
/// </summary>
public sealed record NoteWorkspaceEnrichment(
    IReadOnlyList<NoteDecisionApprovalDto> Decisions,
    NoteDecisionApprovalDto? PendingDecision,
    IReadOnlyList<NotePartsRequirementDto> PartsItems,
    NotePartsProgressDto? PartsProgress,
    NoteSlaStateDto SlaState);

public interface INoteWorkspaceEnrichmentService
{
    Task<NoteWorkspaceEnrichment> BuildAsync(NoteDetailDto note, CancellationToken cancellationToken = default);
}

public sealed class NoteWorkspaceEnrichmentService(
    INoteDecisionApprovalService decisionApprovals,
    INotePartsRequirementService parts,
    INoteSlaService sla) : INoteWorkspaceEnrichmentService
{
    public async Task<NoteWorkspaceEnrichment> BuildAsync(NoteDetailDto note, CancellationToken cancellationToken = default)
    {
        var decisions = await decisionApprovals.ListAsync(note.Id, cancellationToken);
        var pendingDecision = decisions.FirstOrDefault(d => d.Status == NoteDecisionApprovalStatus.Pending);

        var requiresParts = note.TreatmentExecutionType == NoteTreatmentExecutionType.RequiresParts;
        var partsItems = requiresParts
            ? await parts.ListAsync(note.Id, cancellationToken)
            : Array.Empty<NotePartsRequirementDto>();
        var partsProgress = requiresParts
            ? await parts.GetProgressAsync(note.Id, cancellationToken)
            : null;

        var slaState = await sla.ComputeAsync(note.Id, cancellationToken);

        return new NoteWorkspaceEnrichment(decisions, pendingDecision, partsItems, partsProgress, slaState);
    }
}
