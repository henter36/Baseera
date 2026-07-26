namespace Baseera.Domain.Workforce;

using Baseera.Domain.Common;
using Baseera.Domain.Organization;

/// <summary>
/// Records that a computed reconciliation item key was resolved for a facility.
/// </summary>
public class WorkforceReconciliationResolution : EntityBase
{
    private Facility? facility;

    public Guid FacilityId { get; set; }
    public Facility Facility
    {
        get => facility ?? throw new InvalidOperationException("Facility navigation has not been loaded.");
        set => facility = value;
    }

    public string ItemKey { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string ResolutionAction { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset ResolvedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ResolvedBy { get; set; }
}
