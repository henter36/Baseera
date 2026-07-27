namespace Baseera.Domain.RiskManagement;

public enum RiskType
{
    Security = 0,
    Operational = 1,
    Safety = 2,
    Health = 3,
    Capacity = 4,
    Workforce = 5,
    Resource = 6,
    Technology = 7,
    InformationSecurity = 8,
    Compliance = 9,
    Project = 10,
    Financial = 11,
    Reputation = 12,
    Emergency = 13,
    Strategic = 14,
    Other = 15
}

public enum RiskStatus
{
    Draft = 0,
    UnderAssessment = 1,
    PendingReview = 2,
    Active = 3,
    UnderTreatment = 4,
    Monitoring = 5,
    PendingAcceptance = 6,
    Accepted = 7,
    PendingClosure = 8,
    Closed = 9,
    Reopened = 10,
    Archived = 11
}

public enum TreatmentStrategy
{
    Avoid = 0,
    Reduce = 1,
    Transfer = 2,
    Accept = 3,
    Contingency = 4,
    Monitor = 5
}

/// <summary>Where the RiskRecord itself originated, mirrors SensitiveCustodySourceType/ResourceSourceType shape.</summary>
public enum RiskOriginType
{
    Manual = 0,
    Import = 1,
    ExternalSystem = 2,
    Audit = 3,
    Reconciliation = 4,
    Other = 5
}

/// <summary>4-tier rating severity shown on a RiskRatingBand, matching NoteSeverity's ordering for cross-module dashboard consistency.</summary>
public enum RiskRatingSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum RiskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum MatrixStatus
{
    Draft = 0,
    PendingApproval = 1,
    Active = 2,
    Retired = 3
}

/// <summary>Deterministic, versioned score formulas. No free-form script/expression is ever accepted.</summary>
public enum ScoreFormulaType
{
    LikelihoodTimesMaximumImpact = 0,
    LikelihoodTimesWeightedImpact = 1
}

public enum AssessmentType
{
    Inherent = 0,
    Current = 1,
    Residual = 2,
    PostIncident = 3,
    PeriodicReview = 4,
    Closure = 5
}

public enum AssessmentStatus
{
    Draft = 0,
    PendingReview = 1,
    Reviewed = 2,
    Approved = 3,
    Rejected = 4,
    Superseded = 5
}

public enum RiskControlType
{
    Preventive = 0,
    Detective = 1,
    Corrective = 2,
    Deterrent = 3,
    Recovery = 4,
    Compensating = 5
}

public enum RiskControlStatus
{
    Proposed = 0,
    Implemented = 1,
    PartiallyImplemented = 2,
    Retired = 3
}

public enum ControlEffectiveness
{
    Effective = 0,
    PartiallyEffective = 1,
    Ineffective = 2,
    NotTested = 3,
    Unknown = 4
}

public enum TreatmentPlanStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    InProgress = 3,
    Blocked = 4,
    Overdue = 5,
    Completed = 6,
    Cancelled = 7,
    Rejected = 8
}

public enum RiskTreatmentActionStatus
{
    Draft = 0,
    Assigned = 1,
    InProgress = 2,
    Blocked = 3,
    PendingVerification = 4,
    Completed = 5,
    Cancelled = 6
}

public enum RiskApprovalStatus
{
    NotRequired = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>The kind of domain entity a RiskSourceLink points at. Typed, not a generic JSON blob.</summary>
public enum RiskSourceEntityType
{
    Note = 0,
    CorrectiveAction = 1,
    Escalation = 2,
    Occurrence = 3,
    OccupancyWarning = 4,
    ResourceAsset = 5,
    ResourceGap = 6,
    MaintenanceWorkOrder = 7,
    WorkforceCoverageGap = 8,
    WorkforceQualificationIssue = 9,
    SensitiveCustodyDiscrepancy = 10,
    Project = 11,
    EmergencyPlan = 12,
    FormResponse = 13,
    DataQualityIssue = 14,
    Decision = 15,
    RiskRecord = 16,
    Other = 17
}

public enum RiskSourceRelationshipType
{
    IdentifiedFrom = 0,
    Evidence = 1,
    ContributingFactor = 2,
    Consequence = 3,
    Control = 4,
    TreatmentDependency = 5,
    Trigger = 6,
    Related = 7
}

public enum RiskReviewType
{
    AssessmentReview = 0,
    TreatmentApproval = 1,
    RiskAcceptance = 2,
    ClosureApproval = 3,
    PeriodicReview = 4,
    ReopenReview = 5
}

public enum RiskReviewStatus
{
    Requested = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum RiskReviewDecision
{
    Approved = 0,
    ApprovedWithConditions = 1,
    Returned = 2,
    Rejected = 3
}

public enum RiskTrend
{
    Increasing = 0,
    Stable = 1,
    Decreasing = 2,
    Unknown = 3
}

public enum RiskImportKind
{
    RiskRecords = 0,
    Owners = 1,
    Assessments = 2,
    Controls = 3,
    TreatmentPlans = 4,
    TreatmentActions = 5,
    SourceReferences = 6
}

public static class RiskImportStatuses
{
    public const string Previewed = "Previewed";
    public const string Confirmed = "Confirmed";
}
