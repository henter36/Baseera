namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;

/// <summary>
/// A review/approval request against some subject inside a risk (an assessment, a treatment plan, an
/// acceptance request, a closure request, a reopen request). SubjectReferenceType/Id is a typed pointer
/// so one review model serves every four-eyes workflow without a wide table of nullable FKs.
/// </summary>
public class RiskReview : SoftDeletableEntity
{
    public Guid RiskRecordId { get; set; }
    public RiskRecord RiskRecord { get; set; } = null!;
    public RiskReviewType ReviewType { get; set; }
    public string SubjectReferenceType { get; set; } = string.Empty;
    public Guid? SubjectReferenceId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public Guid? AssignedReviewerId { get; set; }
    public User? AssignedReviewer { get; set; }
    public RiskReviewStatus Status { get; set; } = RiskReviewStatus.Requested;
    public RiskReviewDecision? Decision { get; set; }
    public string? Comments { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Only populated when ReviewType = RiskAcceptance: the justification-required target date and review cadence requested alongside acceptance.</summary>
    public DateTimeOffset? RequestedAcceptedUntilUtc { get; set; }
    public int? RequestedReviewFrequencyDays { get; set; }
}
