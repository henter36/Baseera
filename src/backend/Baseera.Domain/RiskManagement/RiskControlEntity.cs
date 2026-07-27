namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;
using Baseera.Domain.Workforce;

/// <summary>A currently-existing control, separate from a future RiskTreatmentAction. Existence of a control is never treated as proof of effectiveness.</summary>
public class RiskControl : SoftDeletableEntity
{
    public Guid RiskRecordId { get; set; }
    public RiskRecord RiskRecord { get; set; } = null!;
    public RiskControlType ControlType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerWorkforceMemberId { get; set; }
    public WorkforceMember? OwnerWorkforceMember { get; set; }
    public RiskControlStatus ControlStatus { get; set; } = RiskControlStatus.Proposed;
    public ControlEffectiveness ControlEffectiveness { get; set; } = ControlEffectiveness.NotTested;
    public DateTimeOffset? ImplementedAtUtc { get; set; }
    public DateTimeOffset? LastTestedAtUtc { get; set; }
    public DateTimeOffset? NextTestDueAtUtc { get; set; }
    public bool EvidenceRequired { get; set; }
    public string? SourceReference { get; set; }
}
