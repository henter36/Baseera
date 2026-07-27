namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;
using Baseera.Domain.Organization;

/// <summary>Mirrors SensitiveCustodyImportBatch: preview/confirm pipeline with an idempotency key of (facility, source, hash).</summary>
public class RiskImportBatch : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public RiskImportKind ImportKind { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string Status { get; set; } = RiskImportStatuses.Previewed;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RejectedRows { get; set; }
    public int DuplicateRows { get; set; }
    public int AppliedRows { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
}

/// <summary>Mirrors SensitiveCustodyReconciliationResolution: records how a detected conflict/discrepancy was resolved.</summary>
public class RiskReconciliationRecord : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public string ItemKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ResolvedBy { get; set; } = string.Empty;
    public DateTimeOffset ResolvedAtUtc { get; set; }
}
