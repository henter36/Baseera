namespace Baseera.Application.RiskManagement;

/// <summary>
/// Twelve narrow service interfaces per Phase D.6 scope — deliberately not a single "RiskService" god object.
/// Each implementation shares scope/permission/audit/row-version plumbing via RiskServiceBase, not by
/// duplicating a monolithic class.
/// </summary>
public interface IRiskRegisterQueryService
{
    Task<RiskWorkspaceSummaryDto> GetSummaryAsync(Guid facilityId, CancellationToken cancellationToken = default);
    Task<RiskPagedResult<RiskListItemDto>> ListAsync(Guid facilityId, RiskListFilters filters, CancellationToken cancellationToken = default);
    Task<RiskDetailDto?> GetAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiskCategoryDto>> ListCategoriesAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

public interface IRiskCommandService
{
    Task<Guid> CreateAsync(Guid facilityId, RiskCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid facilityId, Guid riskId, RiskUpdateRequest request, CancellationToken cancellationToken = default);
    Task ExecuteCommandAsync(Guid facilityId, Guid riskId, RiskCommandRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateCategoryAsync(Guid organizationId, RiskCategoryUpsertRequest request, CancellationToken cancellationToken = default);
}

public interface IRiskAssessmentService
{
    Task<IReadOnlyList<RiskAssessmentDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Guid facilityId, Guid riskId, RiskAssessmentCreateRequest request, CancellationToken cancellationToken = default);
    Task SubmitAsync(Guid facilityId, Guid riskId, Guid assessmentId, RiskRowVersionRequest request, CancellationToken cancellationToken = default);
    Task ReviewAsync(Guid facilityId, Guid riskId, Guid assessmentId, RiskAssessmentReviewRequest request, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid facilityId, Guid riskId, Guid assessmentId, RiskAssessmentApproveRequest request, CancellationToken cancellationToken = default);
}

public interface IRiskMatrixService
{
    Task<IReadOnlyList<RiskMatrixDto>> ListAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Guid organizationId, RiskMatrixCreateRequest request, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid organizationId, Guid matrixId, RiskRowVersionRequest request, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid organizationId, Guid matrixId, RiskRowVersionRequest request, CancellationToken cancellationToken = default);
}

public interface IRiskControlService
{
    Task<IReadOnlyList<RiskControlDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Guid facilityId, Guid riskId, RiskControlCreateRequest request, CancellationToken cancellationToken = default);
    Task RecordTestAsync(Guid facilityId, Guid riskId, Guid controlId, RiskControlTestRequest request, CancellationToken cancellationToken = default);
}

public interface IRiskTreatmentService
{
    Task<IReadOnlyList<RiskTreatmentPlanDto>> ListPlansAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default);
    Task<Guid> CreatePlanAsync(Guid facilityId, Guid riskId, RiskTreatmentPlanCreateRequest request, CancellationToken cancellationToken = default);
    Task ExecutePlanCommandAsync(Guid facilityId, Guid riskId, Guid planId, RiskTreatmentPlanCommandRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateActionAsync(Guid facilityId, Guid riskId, Guid planId, RiskTreatmentActionCreateRequest request, CancellationToken cancellationToken = default);
    Task ExecuteActionCommandAsync(Guid facilityId, Guid riskId, Guid planId, Guid actionId, RiskTreatmentActionCommandRequest request, CancellationToken cancellationToken = default);
}

public interface IRiskReviewService
{
    Task<IReadOnlyList<RiskReviewDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default);
    Task<Guid> RequestAsync(Guid facilityId, Guid riskId, RiskReviewRequestDto request, CancellationToken cancellationToken = default);
    Task DecideAsync(Guid facilityId, Guid riskId, Guid reviewId, RiskReviewDecisionRequest request, CancellationToken cancellationToken = default);
}

public interface IRiskSourceLinkService
{
    Task<IReadOnlyList<RiskSourceLinkDto>> ListAsync(Guid facilityId, Guid riskId, CancellationToken cancellationToken = default);
    Task<Guid> AddAsync(Guid facilityId, Guid riskId, RiskSourceLinkCreateRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid facilityId, Guid riskId, Guid linkId, RiskSourceLinkRemoveRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Facility Workspace integration: summary rail, priority-queue intervention items, and drill-down counts.</summary>
public interface IRiskReadinessService
{
    Task<RiskWorkspaceSummaryDto> GetWorkspaceSummaryAsync(Guid facilityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiskInterventionItemDto>> GetInterventionsAsync(Guid facilityId, int limit, CancellationToken cancellationToken = default);
    Task<RiskWorkspacePayload> GetWorkspacePayloadAsync(Guid facilityId, CancellationToken cancellationToken = default);
}

public interface IRiskDataQualityService
{
    Task<RiskDataQualityPayload> GetDataQualityAsync(Guid facilityId, CancellationToken cancellationToken = default);
}

public interface IRiskImportService
{
    Task<RiskImportResult> PreviewAsync(Guid facilityId, RiskImportPreviewRequest request, CancellationToken cancellationToken = default);
    Task<RiskImportResult> ConfirmAsync(Guid facilityId, RiskImportPreviewRequest request, CancellationToken cancellationToken = default);
}

public interface IRiskReconciliationService
{
    Task<IReadOnlyList<RiskReconciliationItemDto>> ListAsync(Guid facilityId, CancellationToken cancellationToken = default);
    Task ResolveAsync(Guid facilityId, RiskReconciliationResolveRequest request, CancellationToken cancellationToken = default);
}
