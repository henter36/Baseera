namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Common;
using Baseera.Domain.Organization;

/// <summary>
/// A versioned likelihood x impact scoring matrix. Once Status = Active, its Likelihood/Impact/RatingBand
/// rows must never be edited (enforced in IRiskMatrixService) — a change always creates a new Draft matrix
/// with PreviousVersionMatrixId pointing back, mirroring FormVersion's BasedOnVersionId chain.
/// </summary>
public class RiskAssessmentMatrix : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public MatrixStatus Status { get; set; } = MatrixStatus.Draft;
    public ScoreFormulaType ScoreFormula { get; set; } = ScoreFormulaType.LikelihoodTimesMaximumImpact;

    /// <summary>Only used when ScoreFormula = LikelihoodTimesWeightedImpact: JSON map of ImpactDimensionId -> decimal weight. Data, never a script.</summary>
    public string? ImpactWeightingJson { get; set; }

    public DateTimeOffset EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public bool IsDefault { get; set; }
    public Guid? PreviousVersionMatrixId { get; set; }
    public RiskAssessmentMatrix? PreviousVersionMatrix { get; set; }

    public ICollection<LikelihoodLevel> LikelihoodLevels { get; set; } = [];
    public ICollection<ImpactLevel> ImpactLevels { get; set; } = [];
    public ICollection<RiskRatingBand> RatingBands { get; set; } = [];
}

public class LikelihoodLevel : SoftDeletableEntity
{
    public Guid MatrixId { get; set; }
    public RiskAssessmentMatrix Matrix { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int NumericValue { get; set; }
    public string? Description { get; set; }
    public string? Criteria { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Organization-level catalog of impact dimensions (Security, Safety, Operations, ...), reusable across matrix versions.</summary>
public class ImpactDimension : SoftDeletableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

/// <summary>The impact scale for one dimension within one specific matrix version.</summary>
public class ImpactLevel : SoftDeletableEntity
{
    public Guid MatrixId { get; set; }
    public RiskAssessmentMatrix Matrix { get; set; } = null!;
    public Guid ImpactDimensionId { get; set; }
    public ImpactDimension ImpactDimension { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int NumericValue { get; set; }
    public string? Description { get; set; }
    public string? Criteria { get; set; }
    public int DisplayOrder { get; set; }
}

public class RiskRatingBand : SoftDeletableEntity
{
    public Guid MatrixId { get; set; }
    public RiskAssessmentMatrix Matrix { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public decimal MinimumScore { get; set; }
    public decimal MaximumScore { get; set; }
    public RiskRatingSeverity Severity { get; set; } = RiskRatingSeverity.Medium;
    public int? ResponseTimeHours { get; set; }
    public bool EscalationRequired { get; set; }
    public int? ReviewFrequencyDays { get; set; }

    /// <summary>Semantic token only (e.g. "danger"/"warn"/"info") — never a literal hex/CSS value out of the domain.</summary>
    public string ColorToken { get; set; } = "neutral";
    public int DisplayOrder { get; set; }
}
