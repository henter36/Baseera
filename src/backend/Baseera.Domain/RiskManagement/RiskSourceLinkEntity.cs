namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;

/// <summary>
/// A typed link from a risk to an existing evidence/source entity elsewhere in the system.
/// Never hard-deleted (principle: evidence and sources are never historically erased) — removal is a
/// soft-delete with a mandatory RemovalReason, so the audit trail always shows what was once linked.
/// </summary>
public class RiskSourceLink : SoftDeletableEntity
{
    public Guid RiskRecordId { get; set; }
    public RiskRecord RiskRecord { get; set; } = null!;
    public RiskSourceEntityType SourceEntityType { get; set; }
    public Guid SourceEntityId { get; set; }
    public RiskSourceRelationshipType RelationshipType { get; set; }
    public DateTimeOffset AddedAtUtc { get; set; }
    public string AddedBy { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public string? RemovalReason { get; set; }
}
