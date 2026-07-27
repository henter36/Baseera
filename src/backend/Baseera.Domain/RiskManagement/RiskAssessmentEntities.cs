namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;

/// <summary>
/// Append-only-by-policy: an Approved assessment is never mutated by the application layer.
/// A correction always creates a new RiskAssessment with SupersedesAssessmentId set, and the
/// superseded row transitions to AssessmentStatus.Superseded (see IRiskAssessmentService).
/// </summary>
public class RiskAssessment : SoftDeletableEntity
{
    public Guid RiskRecordId { get; set; }
    public RiskRecord RiskRecord { get; set; } = null!;
    public AssessmentType AssessmentType { get; set; }
    public Guid MatrixId { get; set; }
    public RiskAssessmentMatrix Matrix { get; set; } = null!;

    /// <summary>Denormalized copy of Matrix.Version at assessment time — the assessment keeps this even if the matrix is later re-versioned.</summary>
    public int MatrixVersion { get; set; }

    public Guid LikelihoodLevelId { get; set; }
    public LikelihoodLevel LikelihoodLevel { get; set; } = null!;
    public Guid? OverallImpactLevelId { get; set; }
    public ImpactLevel? OverallImpactLevel { get; set; }

    /// <summary>Always computed server-side from the matrix formula. Never accepted from the client.</summary>
    public decimal CalculatedScore { get; set; }
    public Guid RatingBandId { get; set; }
    public RiskRatingBand RatingBand { get; set; } = null!;

    public string? Rationale { get; set; }
    public DateTimeOffset AssessedAtUtc { get; set; }
    public string AssessedBy { get; set; } = string.Empty;
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Draft;
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SupersedesAssessmentId { get; set; }
    public RiskAssessment? SupersedesAssessment { get; set; }

    /// <summary>Required and populated only for AssessmentType.Closure: what changed since the previous approved assessment.</summary>
    public string? ClosureChangeSummary { get; set; }

    public ICollection<RiskAssessmentImpact> ImpactBreakdown { get; set; } = [];
}

public class RiskAssessmentImpact : SoftDeletableEntity
{
    public Guid RiskAssessmentId { get; set; }
    public RiskAssessment RiskAssessment { get; set; } = null!;
    public Guid ImpactDimensionId { get; set; }
    public ImpactDimension ImpactDimension { get; set; } = null!;
    public Guid ImpactLevelId { get; set; }
    public ImpactLevel ImpactLevel { get; set; } = null!;
    public string? RationaleAr { get; set; }
    public string? EvidenceReference { get; set; }
}
