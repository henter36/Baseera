namespace Baseera.Domain.RiskManagement;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Domain.Workforce;

/// <summary>
/// The enterprise risk register root entity. Inherent risk (first assessment) and residual risk
/// (latest approved Residual assessment) are always separate rows in RiskAssessment — the pointer
/// fields here are a denormalized read cache updated only when an assessment is approved, never the
/// source of truth (see docs/phase-d6-risk-scoring.md).
/// </summary>
public class RiskRecord : SoftDeletableEntity, IScopedEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string RiskCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid RiskCategoryId { get; set; }
    public RiskCategory RiskCategory { get; set; } = null!;
    public RiskType RiskType { get; set; } = RiskType.Other;

    /// <summary>Set once at creation by the command service; this phase only ever writes ScopeType.Facility.</summary>
    public ScopeType ScopeLevel { get; set; } = ScopeType.Facility;
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? HeadquartersOrganizationId { get; set; }
    public Organization? HeadquartersOrganization { get; set; }

    public Guid? OwnerWorkforceMemberId { get; set; }
    public WorkforceMember? OwnerWorkforceMember { get; set; }
    public Guid? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public RiskStatus Status { get; set; } = RiskStatus.Draft;
    public TreatmentStrategy? TreatmentStrategy { get; set; }
    public ClassificationLevel ConfidentialityLevel { get; set; } = ClassificationLevel.Internal;
    public RiskOriginType SourceType { get; set; } = RiskOriginType.Manual;
    public string? SourceReference { get; set; }

    public DateTimeOffset FirstIdentifiedAtUtc { get; set; }
    public DateTimeOffset? LastReviewedAtUtc { get; set; }
    public DateTimeOffset? NextReviewDueAtUtc { get; set; }
    public DateTimeOffset? AcceptedUntilUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosedBy { get; set; }
    public string? ClosureReason { get; set; }

    public int ReopenedCount { get; set; }
    public DateTimeOffset? LastReopenedAtUtc { get; set; }
    public string? LastReopenReason { get; set; }

    /// <summary>Category code + facility + normalized title, computed at write time to support recurrence detection without a fuzzy-matching engine.</summary>
    public string RecurrenceKey { get; set; } = string.Empty;

    // Denormalized read-cache of the latest *approved* assessments, refreshed transactionally on approval only.
    public Guid? CurrentInherentAssessmentId { get; set; }
    public RiskAssessment? CurrentInherentAssessment { get; set; }
    public Guid? CurrentAssessmentId { get; set; }
    public RiskAssessment? CurrentAssessment { get; set; }
    public Guid? CurrentResidualAssessmentId { get; set; }
    public RiskAssessment? CurrentResidualAssessment { get; set; }
    public decimal? CurrentScore { get; set; }
    public Guid? CurrentRatingBandId { get; set; }
    public RiskRatingBand? CurrentRatingBand { get; set; }
    public RiskTrend CurrentTrend { get; set; } = RiskTrend.Unknown;
    public string? CurrentTrendReasonAr { get; set; }
    public DateTimeOffset? DataFreshAsOfUtc { get; set; }

    public ICollection<RiskAssessment> Assessments { get; set; } = [];
    public ICollection<RiskControl> Controls { get; set; } = [];
    public ICollection<RiskTreatmentPlan> TreatmentPlans { get; set; } = [];
    public ICollection<RiskSourceLink> SourceLinks { get; set; } = [];
    public ICollection<RiskReview> Reviews { get; set; } = [];

    public ScopeType ScopeType => ScopeLevel;
    Guid? IScopedEntity.RegionId => FacilityId.HasValue ? Facility?.RegionId : RegionId;
    Guid? IScopedEntity.FacilityId => FacilityId;
    Guid? IScopedEntity.FacilityUnitId => FacilityUnitId;
}

/// <summary>Append-only lifecycle timeline, mirrors CorrectiveActionStatusHistory. Never updated or deleted.</summary>
public class RiskStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RiskRecordId { get; set; }
    public RiskRecord RiskRecord { get; set; } = null!;
    public RiskStatus FromStatus { get; set; }
    public RiskStatus ToStatus { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ChangedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
