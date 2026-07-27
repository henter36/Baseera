namespace Baseera.Application.RiskManagement;

/// <summary>Plain PascalCase "{Entity}{PastTenseVerb}" action names, matching the codebase-wide audit convention (no enum).</summary>
public static class RiskAuditActions
{
    public const string RiskCreated = "RiskCreated";
    public const string RiskUpdated = "RiskUpdated";
    public const string RiskOwnerAssigned = "RiskOwnerAssigned";
    public const string RiskStatusChanged = "RiskStatusChanged";
    public const string RiskEscalated = "RiskEscalated";
    public const string RiskSourceLinked = "RiskSourceLinked";
    public const string RiskSourceUnlinked = "RiskSourceUnlinked";
    public const string RiskCategoryCreated = "RiskCategoryCreated";

    public const string RiskAssessmentCreated = "RiskAssessmentCreated";
    public const string RiskAssessmentSubmitted = "RiskAssessmentSubmitted";
    public const string RiskAssessmentReviewed = "RiskAssessmentReviewed";
    public const string RiskAssessmentApproved = "RiskAssessmentApproved";
    public const string RiskAssessmentRejected = "RiskAssessmentRejected";

    public const string RiskControlCreated = "RiskControlCreated";
    public const string RiskControlTested = "RiskControlTested";

    public const string RiskTreatmentCreated = "RiskTreatmentCreated";
    public const string RiskTreatmentApproved = "RiskTreatmentApproved";
    public const string RiskTreatmentActionChanged = "RiskTreatmentActionChanged";

    public const string RiskAcceptanceRequested = "RiskAcceptanceRequested";
    public const string RiskAccepted = "RiskAccepted";
    public const string RiskClosureRequested = "RiskClosureRequested";
    public const string RiskClosed = "RiskClosed";
    public const string RiskReopened = "RiskReopened";
    public const string RiskReviewCompleted = "RiskReviewCompleted";

    public const string RiskMatrixCreated = "RiskMatrixCreated";
    public const string RiskMatrixApproved = "RiskMatrixApproved";
    public const string RiskMatrixActivated = "RiskMatrixActivated";

    public const string RiskImportPreviewed = "RiskImportPreviewed";
    public const string RiskImportConfirmed = "RiskImportConfirmed";
    public const string RiskExported = "RiskExported";
    public const string RiskReconciled = "RiskReconciled";
}

/// <summary>Stable intervention type codes surfaced in the Facility Workspace priority queue / Intervention Queue.</summary>
public static class RiskInterventionTypes
{
    public const string CriticalRiskActive = "CriticalRiskActive";
    public const string HighRiskIncreasing = "HighRiskIncreasing";
    public const string RiskWithoutOwner = "RiskWithoutOwner";
    public const string RiskWithoutCurrentAssessment = "RiskWithoutCurrentAssessment";
    public const string RiskReviewOverdue = "RiskReviewOverdue";
    public const string RiskWithoutTreatment = "RiskWithoutTreatment";
    public const string TreatmentPlanOverdue = "TreatmentPlanOverdue";
    public const string TreatmentActionOverdue = "TreatmentActionOverdue";
    public const string TreatmentActionBlocked = "TreatmentActionBlocked";
    public const string ResidualRiskAboveTarget = "ResidualRiskAboveTarget";
    public const string AcceptedRiskReviewDue = "AcceptedRiskReviewDue";
    public const string AcceptedRiskExpired = "AcceptedRiskExpired";
    public const string ControlNotTested = "ControlNotTested";
    public const string ControlIneffective = "ControlIneffective";
    public const string RepeatedRiskPattern = "RepeatedRiskPattern";
    public const string PotentialDuplicateRisk = "PotentialDuplicateRisk";
    public const string RiskDataStale = "RiskDataStale";
    public const string ClosureAwaitingApproval = "ClosureAwaitingApproval";
    public const string AcceptanceAwaitingApproval = "AcceptanceAwaitingApproval";
}

/// <summary>Stable data-quality catalog codes, mirroring SensitiveCustodyOperationalCatalog.DataQuality.</summary>
public static class RiskDataQualityCodes
{
    public const string MissingCategory = "RISK_MISSING_CATEGORY";
    public const string MissingOwner = "RISK_MISSING_OWNER";
    public const string MissingCurrentAssessment = "RISK_MISSING_CURRENT_ASSESSMENT";
    public const string AssessmentWithoutRationale = "RISK_ASSESSMENT_NO_RATIONALE";
    public const string MissingReviewDate = "RISK_MISSING_REVIEW_DATE";
    public const string ReviewOverdue = "RISK_REVIEW_OVERDUE";
    public const string ActiveWithoutTreatment = "RISK_ACTIVE_NO_TREATMENT";
    public const string TreatmentWithoutOwner = "RISK_TREATMENT_NO_OWNER";
    public const string OverdueTreatmentAction = "RISK_TREATMENT_ACTION_OVERDUE";
    public const string CompletedActionWithoutEvidence = "RISK_ACTION_NO_EVIDENCE";
    public const string AcceptedWithoutExpiry = "RISK_ACCEPTED_NO_EXPIRY";
    public const string ClosedWithoutResidualAssessment = "RISK_CLOSED_NO_RESIDUAL";
    public const string ClosedWithoutReason = "RISK_CLOSED_NO_REASON";
    public const string ControlNotTested = "RISK_CONTROL_NOT_TESTED";
    public const string IneffectiveControlWithoutTreatment = "RISK_CONTROL_INEFFECTIVE_NO_TREATMENT";
    public const string PotentialDuplicate = "RISK_POTENTIAL_DUPLICATE";
    public const string StaleData = "RISK_STALE_DATA";
}
