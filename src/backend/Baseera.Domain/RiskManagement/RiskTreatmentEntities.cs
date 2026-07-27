namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Domain.Workforce;

public class RiskTreatmentPlan : SoftDeletableEntity
{
    public Guid RiskRecordId { get; set; }
    public RiskRecord RiskRecord { get; set; } = null!;
    public TreatmentStrategy Strategy { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public Guid? OwnerWorkforceMemberId { get; set; }
    public WorkforceMember? OwnerWorkforceMember { get; set; }
    public TreatmentPlanStatus Status { get; set; } = TreatmentPlanStatus.Draft;
    public RiskPriority Priority { get; set; } = RiskPriority.Medium;
    public DateTimeOffset? PlannedStartAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Guid? TargetLikelihoodLevelId { get; set; }
    public LikelihoodLevel? TargetLikelihoodLevel { get; set; }
    public Guid? TargetImpactLevelId { get; set; }
    public ImpactLevel? TargetImpactLevel { get; set; }
    public decimal? TargetScore { get; set; }
    public RiskApprovalStatus ApprovalStatus { get; set; } = RiskApprovalStatus.Pending;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<RiskTreatmentAction> Actions { get; set; } = [];
}

public class RiskTreatmentAction : SoftDeletableEntity
{
    public Guid TreatmentPlanId { get; set; }
    public RiskTreatmentPlan TreatmentPlan { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssignedToWorkforceMemberId { get; set; }
    public WorkforceMember? AssignedToWorkforceMember { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }
    public Guid? AssignedOrganizationId { get; set; }
    public Organization? AssignedOrganization { get; set; }
    public Guid? AssignedFacilityUnitId { get; set; }
    public FacilityUnit? AssignedFacilityUnit { get; set; }
    public RiskTreatmentActionStatus Status { get; set; } = RiskTreatmentActionStatus.Draft;
    public RiskPriority Priority { get; set; } = RiskPriority.Medium;
    public DateTimeOffset? StartAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public bool CompletionEvidenceRequired { get; set; }
    public string? CompletionSummary { get; set; }
    public string? BlockedReason { get; set; }
    public Guid? DependencyActionId { get; set; }
    public RiskTreatmentAction? DependencyAction { get; set; }
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public string? VerifiedBy { get; set; }
    public string? CancellationReason { get; set; }
}
